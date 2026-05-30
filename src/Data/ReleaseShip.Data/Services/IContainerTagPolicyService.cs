using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    public interface IContainerTagPolicyService
    {
        Task<bool> CanUpdateTagAsync(ContainerRepositoryDetail repository, string tagName, CancellationToken token);
    }
}
