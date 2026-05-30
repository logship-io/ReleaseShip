using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    public interface IContainerAuthenticationService
    {
        Task EnsureBootstrapAdminAsync(string username, string password, CancellationToken token);
        Task<ContainerAuthenticatedPrincipal?> AuthenticateAsync(string username, string secret, CancellationToken token);
        Task<ContainerIssuedToken> IssueTokenAsync(ContainerTokenIssueRequest request, CancellationToken token);
    }
}
