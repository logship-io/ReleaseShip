using ReleaseShip.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public interface IProjectStorageService
    {
        Task<IEnumerable<Project>> GetProjectsAsync(CancellationToken token);
        Task<Project?> GetProjectAsync(string projectId, CancellationToken token);
        Task<int> PutProjectAsync(Project project, CancellationToken token);
    }
}
