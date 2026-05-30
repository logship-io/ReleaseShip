using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    public interface IContainerRegistryService
    {
        Task<ContainerRepositoryDetail?> GetRepositoryDetailAsync(string repositoryName, CancellationToken token);
        Task<ContainerManifestReference?> GetManifestAsync(string repositoryName, string reference, CancellationToken token);
        Task<IReadOnlyList<string>> GetTagsAsync(string repositoryName, CancellationToken token);
        Task<bool> BlobExistsAsync(string repositoryName, string digest, CancellationToken token);
        Task<Stream?> OpenBlobReadAsync(string repositoryName, string digest, CancellationToken token);
        Task<ContainerUpload> StartUploadAsync(string repositoryName, CancellationToken token);
        Task<ContainerUpload?> GetUploadAsync(string repositoryName, string uploadId, CancellationToken token);
        Task<ContainerUpload> AppendUploadAsync(string repositoryName, string uploadId, Stream content, CancellationToken token);
        Task<ContainerBlob> CompleteUploadAsync(string repositoryName, string uploadId, string digest, string? mediaType, CancellationToken token);
        Task<ContainerManifestReference> PutManifestAsync(ContainerManifestPutRequest request, CancellationToken token);
        Task DeleteUploadAsync(string repositoryName, string uploadId, CancellationToken token);
        Task DeleteManifestAsync(string repositoryName, string digest, CancellationToken token);
        Task DeleteTagAsync(string repositoryName, string tagName, CancellationToken token);
    }
}
