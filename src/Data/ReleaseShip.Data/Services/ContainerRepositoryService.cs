using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;

namespace ReleaseShip.Data.Services
{
    internal sealed class ContainerRepositoryService : IContainerRepositoryService
    {
        private readonly IDatabaseContext ctx;

        public ContainerRepositoryService(IDatabaseContext ctx)
        {
            this.ctx = ctx;
        }

        public async Task<ContainerRepository?> GetRepositoryAsync(string fullName, CancellationToken token)
        {
            string normalized = NormalizeFullName(fullName);
            using var conn = await this.ctx.GetConnection(token);
            return await conn.QuerySingleOrDefaultAsync<ContainerRepository?>(
                @"select *
                  from registry_repositories
                  where full_name = @FullName",
                new
                {
                    FullName = normalized,
                });
        }

        public async Task<ContainerRepositoryDetail?> GetRepositoryDetailAsync(string fullName, CancellationToken token)
        {
            var repository = await GetRepositoryAsync(fullName, token);
            if (repository == null)
            {
                return null;
            }

            using var conn = await this.ctx.GetConnection(token);
            var rules = await conn.QueryAsync<ContainerTagRule>(
                @"select *
                  from registry_tag_rules
                  where repository_id = @RepositoryId
                  order by pattern",
                new
                {
                    RepositoryId = repository.Id,
                });

            return new ContainerRepositoryDetail()
            {
                Repository = repository,
                TagRules = rules.ToList(),
            };
        }

        public async Task<IReadOnlyList<ContainerRepository>> GetRepositoriesAsync(string? namespaceName, bool publicOnly, CancellationToken token)
        {
            string? normalizedNamespace = string.IsNullOrWhiteSpace(namespaceName)
                ? null
                : NormalizeNamespace(namespaceName);

            using var conn = await this.ctx.GetConnection(token);
            var rows = await conn.QueryAsync<ContainerRepository>(
                @"select *
                  from registry_repositories
                  where (@NamespaceName is null or namespace = @NamespaceName)
                    and (@PublicOnly = 0 or allow_anonymous_pull = 1)
                  order by full_name",
                new
                {
                    NamespaceName = normalizedNamespace,
                    PublicOnly = publicOnly,
                });

            return rows.ToList();
        }

        public async Task<IReadOnlyList<string>> GetNamespacesAsync(bool publicOnly, CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            var rows = await conn.QueryAsync<string>(
                @"select distinct namespace
                  from registry_repositories
                  where namespace != ''
                    and (@PublicOnly = 0 or allow_anonymous_pull = 1)
                  order by namespace",
                new
                {
                    PublicOnly = publicOnly,
                });

            return rows.ToList();
        }

        public async Task<int> PutRepositoryAsync(ContainerRepository repository, CancellationToken token)
        {
            return await PutRepositoryAsync(repository, [], token);
        }

        public async Task<int> PutRepositoryAsync(ContainerRepository repository, IReadOnlyList<string> protectedTagPatterns, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(repository);

            var normalized = NormalizeRepository(repository);
            var normalizedPatterns = NormalizePatterns(protectedTagPatterns);
            using var conn = await this.ctx.GetConnection(token);

            int? existingId = await conn.ExecuteScalarAsync<int?>(
                @"select id
                  from registry_repositories
                  where full_name = @FullName",
                new
                {
                    normalized.FullName,
                });

            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int repositoryId;
            if (existingId.HasValue)
            {
                await conn.ExecuteAsync(
                    @"update registry_repositories
                      set namespace = @Namespace,
                          name = @Name,
                          description = @Description,
                          allow_anonymous_pull = @AllowAnonymousPull,
                          allow_delete = @AllowDelete,
                          default_tag_mutability = @DefaultTagMutability,
                          updated_date_utc = @UpdatedDateUtc
                      where id = @Id",
                    new
                    {
                        Id = existingId.Value,
                        normalized.Namespace,
                        normalized.Name,
                        normalized.Description,
                        normalized.AllowAnonymousPull,
                        normalized.AllowDelete,
                        normalized.DefaultTagMutability,
                        UpdatedDateUtc = nowUtc,
                    });
                repositoryId = existingId.Value;
            }
            else
            {
                repositoryId = await conn.ExecuteScalarAsync<int>(
                    @"insert into registry_repositories (
                      namespace,
                      name,
                      full_name,
                      description,
                      allow_anonymous_pull,
                      allow_delete,
                      default_tag_mutability,
                      created_date_utc,
                      updated_date_utc)
                  values (
                      @Namespace,
                      @Name,
                      @FullName,
                      @Description,
                      @AllowAnonymousPull,
                      @AllowDelete,
                      @DefaultTagMutability,
                      @CreatedDateUtc,
                      @UpdatedDateUtc);
                  select last_insert_rowid();",
                new
                {
                    normalized.Namespace,
                    normalized.Name,
                    normalized.FullName,
                    normalized.Description,
                    normalized.AllowAnonymousPull,
                    normalized.AllowDelete,
                    normalized.DefaultTagMutability,
                    CreatedDateUtc = nowUtc,
                    UpdatedDateUtc = nowUtc,
                    });
            }

            await conn.ExecuteAsync(
                @"delete from registry_tag_rules
                  where repository_id = @RepositoryId",
                new
                {
                    RepositoryId = repositoryId,
                });

            foreach (string pattern in normalizedPatterns)
            {
                await conn.ExecuteAsync(
                    @"insert into registry_tag_rules (repository_id, pattern, is_immutable, created_date_utc)
                      values (@RepositoryId, @Pattern, 1, @CreatedDateUtc)",
                    new
                    {
                        RepositoryId = repositoryId,
                        Pattern = pattern,
                        CreatedDateUtc = nowUtc,
                    });
            }

            return repositoryId;
        }

        private static ContainerRepository NormalizeRepository(ContainerRepository repository)
        {
            string fullName = string.IsNullOrWhiteSpace(repository.FullName)
                ? CombinePath(repository.Namespace, repository.Name)
                : NormalizeFullName(repository.FullName);

            string[] segments = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                throw new ArgumentException("Repository name is required.", nameof(repository));
            }

            if (segments.Any(segment => segment.Any(char.IsWhiteSpace)))
            {
                throw new ArgumentException("Repository names cannot contain whitespace.", nameof(repository));
            }

            string name = segments[^1];
            string namespaceName = segments.Length == 1
                ? string.Empty
                : string.Join('/', segments.Take(segments.Length - 1));
            string defaultTagMutability = NormalizeTagMutability(repository.DefaultTagMutability);

            return new ContainerRepository()
            {
                Id = repository.Id,
                Namespace = namespaceName,
                Name = name,
                FullName = fullName,
                Description = string.IsNullOrWhiteSpace(repository.Description) ? null : repository.Description.Trim(),
                AllowAnonymousPull = repository.AllowAnonymousPull,
                AllowDelete = repository.AllowDelete,
                DefaultTagMutability = defaultTagMutability,
                CreatedDateUtc = repository.CreatedDateUtc,
                UpdatedDateUtc = repository.UpdatedDateUtc,
            };
        }

        private static string NormalizeFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("Repository name is required.", nameof(fullName));
            }

            string normalized = fullName
                .Replace('\\', '/')
                .Trim()
                .Trim('/')
                .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Repository name is required.", nameof(fullName));
            }

            return normalized;
        }

        private static string NormalizeNamespace(string namespaceName)
        {
            string normalized = namespaceName
                .Replace('\\', '/')
                .Trim()
                .Trim('/')
                .ToLowerInvariant();

            return normalized;
        }

        private static string CombinePath(string namespaceName, string name)
        {
            string normalizedName = NormalizeFullName(name);
            string normalizedNamespace = NormalizeNamespace(namespaceName);

            if (string.IsNullOrWhiteSpace(normalizedNamespace))
            {
                return normalizedName;
            }

            return $"{normalizedNamespace}/{normalizedName}";
        }

        private static string NormalizeTagMutability(string? value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? ContainerTagMutabilityModes.Mutable
                : value.Trim().ToLowerInvariant();

            return normalized switch
            {
                ContainerTagMutabilityModes.Mutable => ContainerTagMutabilityModes.Mutable,
                ContainerTagMutabilityModes.Immutable => ContainerTagMutabilityModes.Immutable,
                _ => throw new ArgumentException($"Unsupported tag mutability mode '{value}'.", nameof(value)),
            };
        }

        private static List<string> NormalizePatterns(IReadOnlyList<string>? patterns)
        {
            if (patterns == null || patterns.Count == 0)
            {
                return [];
            }

            return patterns
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(pattern => pattern.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
