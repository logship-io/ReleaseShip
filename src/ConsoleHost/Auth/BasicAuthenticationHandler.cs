using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ReleaseShip.Data.Services;

namespace ReleaseShip.ConsoleHost.Auth
{
    public sealed partial class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
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
            string requestPath = Request.Path.Value ?? string.Empty;
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                LogNoAuthorizationHeader(Logger, Request.Method, requestPath);
                return AuthenticateResult.NoResult();
            }

            if (!AuthenticationHeaderValue.TryParse(headerValues, out var header) || !string.Equals(header.Scheme, BasicAuthenticationDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            {
                LogInvalidAuthorizationHeader(Logger, Request.Method, requestPath);
                return AuthenticateResult.Fail("Invalid Authorization header.");
            }

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            }
            catch (FormatException)
            {
                LogMalformedPayload(Logger, Request.Method, requestPath);
                return AuthenticateResult.Fail("Invalid Basic authentication payload.");
            }

            int separator = decoded.IndexOf(':');
            if (separator <= 0)
            {
                LogMissingUsernameSeparator(Logger, Request.Method, requestPath);
                return AuthenticateResult.Fail("Invalid Basic authentication payload.");
            }

            string username = decoded[..separator];
            string secret = decoded[(separator + 1)..];
            var principal = await this.authenticationService.AuthenticateAsync(username, secret, Context.RequestAborted);
            if (principal == null)
            {
                LogAuthenticationFailed(Logger, username, Request.Method, requestPath);
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
            LogAuthenticatedPrincipal(Logger, principal.Username, principal.AuthenticationMode, Request.Method, requestPath);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            string requestPath = Request.Path.Value ?? string.Empty;
            SetRegistryHeaders();
            LogChallenge(Logger, Request.Method, requestPath);
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

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            string requestPath = Request.Path.Value ?? string.Empty;
            SetRegistryHeaders();
            LogForbidden(Logger, Context.User.Identity?.Name ?? "(unknown)", Request.Method, requestPath);
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Response.WriteAsJsonAsync(new
            {
                errors = new[]
                {
                    new
                    {
                        code = "DENIED",
                        message = "Requested access to the resource is denied.",
                    }
                }
            });
        }

        private void SetRegistryHeaders()
        {
            if (Request.Path.StartsWithSegments("/v2"))
            {
                Response.Headers["Docker-Distribution-Api-Version"] = "registry/2.0";
            }
        }

        [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "No Authorization header present for {Method} {Path}.")]
        private static partial void LogNoAuthorizationHeader(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Received an invalid Basic Authorization header for {Method} {Path}.")]
        private static partial void LogInvalidAuthorizationHeader(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Received a malformed Basic authentication payload for {Method} {Path}.")]
        private static partial void LogMalformedPayload(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Received a Basic authentication payload without a username separator for {Method} {Path}.")]
        private static partial void LogMissingUsernameSeparator(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Basic authentication failed for principal {Username} on {Method} {Path}.")]
        private static partial void LogAuthenticationFailed(ILogger logger, string username, string method, string path);

        [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "Authenticated principal {Username} using {Mode} for {Method} {Path}.")]
        private static partial void LogAuthenticatedPrincipal(ILogger logger, string username, string mode, string method, string path);

        [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Issuing authentication challenge for {Method} {Path}.")]
        private static partial void LogChallenge(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Rejecting authenticated principal {Username} for {Method} {Path}.")]
        private static partial void LogForbidden(ILogger logger, string username, string method, string path);
    }
}
