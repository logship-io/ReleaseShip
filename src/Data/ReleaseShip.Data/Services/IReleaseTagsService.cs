using ReleaseShip.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public interface IReleaseTagsService
    {
        Task<IReadOnlyList<BinaryReleaseTag>> GetTagsAsync(string projectId, CancellationToken token, DateTime? sinceUtc = null);
        Task PutTagsAsync(int binaryReleaseId, IEnumerable<string> tags, CancellationToken token);
    }
}
