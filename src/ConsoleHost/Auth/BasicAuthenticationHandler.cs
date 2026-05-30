using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ReleaseShip.Data.Services;

namespace ReleaseShip.ConsoleHost.Auth
{
    public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IContainerAuthenticationService authenticationService;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IContainerAuthenticationService authenticationService)
            : base(options, logger, encoder)
        {
            this.authenticationService = authenticationService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                return AuthenticateResult.NoResult();
            }

            if (!AuthenticationHeaderValue.TryParse(headerValues, out var header) || !string.Equals(header.Scheme, BasicAuthenticationDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            {
                return AuthenticateResult.Fail("Invalid Authorization header.");
            }

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            }
            catch (FormatException)
            {
                return AuthenticateResult.Fail("Invalid Basic authentication payload.");
            }

            int separator = decoded.IndexOf(':');
            if (separator <= 0)
            {
                return AuthenticateResult.Fail("Invalid Basic authentication payload.");
            }

            string username = decoded[..separator];
            string secret = decoded[(separator + 1)..];
            var principal = await this.authenticationService.AuthenticateAsync(username, secret, Context.RequestAborted);
            if (principal == null)
            {
                return AuthenticateResult.Fail("Invalid credentials.");
            }

            var claims = new List<Claim>()
            {
                new(ClaimTypes.Name, principal.Username),
                new("registry:auth_mode", principal.AuthenticationMode),
            };

            if (principal.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                claims.Add(new Claim("registry:pull", "*"));
                claims.Add(new Claim("registry:push", "*"));
                claims.Add(new Claim("registry:delete", "*"));
            }
            else
            {
                string scope = principal.ScopeType == "global" || string.IsNullOrWhiteSpace(principal.ScopeValue)
                    ? "*"
                    : principal.ScopeValue.Trim('/');

                if (principal.CanPull)
                {
                    claims.Add(new Claim("registry:pull", scope));
                }

                if (principal.CanPush)
                {
                    claims.Add(new Claim("registry:push", scope));
                }

                if (principal.CanDelete)
                {
                    claims.Add(new Claim("registry:delete", scope));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.WWWAuthenticate = $"Basic realm=\"{BasicAuthenticationDefaults.Realm}\"";
            return Response.WriteAsJsonAsync(new
            {
                errors = new[]
                {
                    new
                    {
                        code = "UNAUTHORIZED",
                        message = "Authentication required.",
                    }
                }
            });
        }
    }
}
