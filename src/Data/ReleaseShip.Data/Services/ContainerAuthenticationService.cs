using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;

namespace ReleaseShip.Data.Services
{
    internal sealed class ContainerAuthenticationService : IContainerAuthenticationService
    {
        private const int Iterations = 100_000;

        private readonly IDatabaseContext ctx;

        public ContainerAuthenticationService(IDatabaseContext ctx)
        {
            this.ctx = ctx;
        }

        public async Task EnsureBootstrapAdminAsync(string username, string password, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            string normalizedUsername = username.Trim();
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync(
                @"insert or ignore into registry_principals (username, password_hash, is_bootstrap_admin, created_date_utc)
                  values (@Username, @PasswordHash, 1, @CreatedDateUtc);
                  update registry_principals
                  set password_hash = @PasswordHash,
                      is_bootstrap_admin = 1,
                      disabled_date_utc = null
                  where username = @Username;",
                new
                {
                    Username = normalizedUsername,
                    PasswordHash = HashSecret(password),
                    CreatedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
        }

        public async Task<ContainerAuthenticatedPrincipal?> AuthenticateAsync(string username, string secret, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(secret))
            {
                return null;
            }

            string normalizedUsername = username.Trim();
            using var conn = await this.ctx.GetConnection(token);

            var principal = await conn.QuerySingleOrDefaultAsync<ContainerPrincipal?>(
                @"select *
                  from registry_principals
                  where username = @Username
                    and disabled_date_utc is null",
                new { Username = normalizedUsername });
            if (principal == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(principal.PasswordHash) && VerifySecret(principal.PasswordHash!, secret))
            {
                return new ContainerAuthenticatedPrincipal()
                {
                    PrincipalId = principal.Id,
                    Username = principal.Username,
                    IsAdmin = principal.IsBootstrapAdmin,
                    CanPull = true,
                    CanPush = principal.IsBootstrapAdmin,
                    CanDelete = principal.IsBootstrapAdmin,
                    ScopeType = "global",
                    ScopeValue = string.Empty,
                    AuthenticationMode = "password",
                };
            }

            var tokens = await conn.QueryAsync<ContainerToken>(
                @"select *
                  from registry_tokens
                  where principal_id = @PrincipalId
                    and revoked_date_utc is null",
                new { PrincipalId = principal.Id });

            foreach (var item in tokens)
            {
                if (!VerifySecret(item.HashedSecret, secret))
                {
                    continue;
                }

                await conn.ExecuteAsync(
                    @"update registry_tokens
                      set last_used_date_utc = @LastUsedDateUtc
                      where id = @Id",
                    new
                    {
                        Id = item.Id,
                        LastUsedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    });

                return new ContainerAuthenticatedPrincipal()
                {
                    PrincipalId = principal.Id,
                    Username = principal.Username,
                    IsAdmin = false,
                    CanPull = item.CanPull,
                    CanPush = item.CanPush,
                    CanDelete = item.CanDelete,
                    ScopeType = item.ScopeType,
                    ScopeValue = item.ScopeValue,
                    AuthenticationMode = "token",
                };
            }

            return null;
        }

        public async Task<ContainerIssuedToken> IssueTokenAsync(ContainerTokenIssueRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);

            string principalUsername = request.PrincipalUsername.Trim();
            if (string.IsNullOrWhiteSpace(principalUsername))
            {
                throw new ArgumentException("Principal username is required.", nameof(request));
            }

            string tokenName = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(tokenName))
            {
                throw new ArgumentException("Token name is required.", nameof(request));
            }

            string scopeType = string.IsNullOrWhiteSpace(request.ScopeType) ? "global" : request.ScopeType.Trim().ToLowerInvariant();
            string scopeValue = request.ScopeValue.Trim().Trim('/');
            if (scopeType != "global" && scopeType != "repository")
            {
                throw new ArgumentException($"Unsupported scope type '{request.ScopeType}'.", nameof(request));
            }

            if (scopeType == "global")
            {
                scopeValue = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(scopeValue))
            {
                throw new ArgumentException("Repository scope tokens require a scope value.", nameof(request));
            }

            using var conn = await this.ctx.GetConnection(token);
            var principal = await conn.QuerySingleOrDefaultAsync<ContainerPrincipal?>(
                @"select *
                  from registry_principals
                  where username = @Username
                    and disabled_date_utc is null",
                new { Username = principalUsername });
            if (principal == null)
            {
                throw new InvalidOperationException($"Principal '{principalUsername}' was not found.");
            }

            string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            int id = await conn.ExecuteScalarAsync<int>(
                @"insert into registry_tokens (
                        principal_id,
                        name,
                        hashed_secret,
                        scope_type,
                        scope_value,
                        can_pull,
                        can_push,
                        can_delete,
                        created_date_utc)
                  values (
                        @PrincipalId,
                        @Name,
                        @HashedSecret,
                        @ScopeType,
                        @ScopeValue,
                        @CanPull,
                        @CanPush,
                        @CanDelete,
                        @CreatedDateUtc);
                  select last_insert_rowid();",
                new
                {
                    PrincipalId = principal.Id,
                    Name = tokenName,
                    HashedSecret = HashSecret(secret),
                    ScopeType = scopeType,
                    ScopeValue = scopeValue,
                    request.CanPull,
                    request.CanPush,
                    request.CanDelete,
                    CreatedDateUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });

            return new ContainerIssuedToken()
            {
                Id = id,
                PrincipalUsername = principal.Username,
                Name = tokenName,
                Secret = secret,
                ScopeType = scopeType,
                ScopeValue = scopeValue,
                CanPull = request.CanPull,
                CanPush = request.CanPush,
                CanDelete = request.CanDelete,
            };
        }

        private static string HashSecret(string secret)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return $"pbkdf2-sha256${Iterations}${Convert.ToHexString(salt).ToLowerInvariant()}${Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        private static bool VerifySecret(string storedHash, string secret)
        {
            string[] parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int iterations))
            {
                return false;
            }

            byte[] salt = Convert.FromHexString(parts[2]);
            byte[] expected = Convert.FromHexString(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
