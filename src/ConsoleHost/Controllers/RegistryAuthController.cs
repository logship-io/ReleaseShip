using Microsoft.AspNetCore.Mvc;
using ReleaseShip.ConsoleHost.Models;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Services;

namespace ReleaseShip.Controllers
{
    [ApiController]
    public class RegistryAuthController : ControllerBase
    {
        private readonly IContainerAuthenticationService authenticationService;

        public RegistryAuthController(IContainerAuthenticationService authenticationService)
        {
            this.authenticationService = authenticationService;
        }

        [HttpGet("/api/admin/auth/me")]
        public IActionResult GetMe()
        {
            if (!this.User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            if (!this.User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return Ok(new
            {
                username = this.User.Identity!.Name,
                isAdmin = true,
            });
        }

        [HttpPost("/api/admin/auth/tokens")]
        [Produces("application/json", "application/xml", Type = typeof(RegistryIssuedTokenModel))]
        public async Task<IActionResult> IssueToken(RegistryTokenIssueModel model, CancellationToken token)
        {
            if (!this.User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            if (!this.User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var issued = await this.authenticationService.IssueTokenAsync(new ContainerTokenIssueRequest()
            {
                PrincipalUsername = string.IsNullOrWhiteSpace(model.PrincipalUsername) ? this.User.Identity!.Name! : model.PrincipalUsername,
                Name = model.Name,
                ScopeType = model.ScopeType,
                ScopeValue = model.ScopeValue,
                CanPull = model.CanPull,
                CanPush = model.CanPush,
                CanDelete = model.CanDelete,
            }, token);

            return Ok(new RegistryIssuedTokenModel()
            {
                Id = issued.Id,
                PrincipalUsername = issued.PrincipalUsername,
                Name = issued.Name,
                Secret = issued.Secret,
                ScopeType = issued.ScopeType,
                ScopeValue = issued.ScopeValue,
                CanPull = issued.CanPull,
                CanPush = issued.CanPush,
                CanDelete = issued.CanDelete,
            });
        }
    }
}
