using ReleaseShip.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public interface IBinaryStorageService
    {
        Task<BinaryRelease?> GetBinaryReleaseAsync(string projectId, string tag, string platformId, CancellationToken token);

        Task<BinaryRelease?> GetBinaryReleaseAsync(int binaryReleaseId, CancellationToken token);

        Task<IReadOnlyList<BinaryRelease>> GetBinariesAsync(string projectId, DateTimeOffset sinceUtc, CancellationToken token);

        Task<int> StoreAsync(string projectId, BinaryUpload model, Stream fileData, CancellationToken token);

        Task<Stream?> RetrieveAsync(int binaryReleaseId, CancellationToken token);
    }
}
