using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    public interface IContainerStorageService
    {
        Task<bool> BlobExistsAsync(string digest, CancellationToken token);

        Task<ContainerUpload> CreateUploadAsync(string repositoryName, CancellationToken token);

        Task DeleteBlobAsync(string digest, CancellationToken token);

        Task DeleteUploadAsync(string uploadId, CancellationToken token);

        Task<ContainerUpload?> GetUploadAsync(string uploadId, CancellationToken token);

        Task<Stream?> OpenBlobReadAsync(string digest, CancellationToken token);

        Task<ContainerUpload> AppendUploadAsync(string uploadId, Stream content, CancellationToken token);

        Task<ContainerBlob> CompleteUploadAsync(string uploadId, string digest, string? mediaType, CancellationToken token);
    }
}
