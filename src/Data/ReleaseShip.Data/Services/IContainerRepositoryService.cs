using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    public interface IContainerRepositoryService
    {
        Task<ContainerRepository?> GetRepositoryAsync(string fullName, CancellationToken token);

        Task<ContainerRepositoryDetail?> GetRepositoryDetailAsync(string fullName, CancellationToken token);

        Task<IReadOnlyList<ContainerRepository>> GetRepositoriesAsync(string? namespaceName, bool publicOnly, CancellationToken token);

        Task<IReadOnlyList<string>> GetNamespacesAsync(bool publicOnly, CancellationToken token);

        Task<int> PutRepositoryAsync(ContainerRepository repository, CancellationToken token);

        Task<int> PutRepositoryAsync(ContainerRepository repository, IReadOnlyList<string> protectedTagPatterns, CancellationToken token);
    }
}
