using Dapper;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    internal sealed class ProjectService : IProjectStorageService
    {
        private readonly IDatabaseContext ctx;

        public ProjectService(IDatabaseContext ctx)
        {
            this.ctx = ctx;
        }

        public async Task<Project?> GetProjectAsync(string projectId, CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            return await conn.QueryFirstOrDefaultAsync<Project?>("select * from projects where id = @ProjectId", new
            {
                ProjectId = projectId,
            });
        }

        public async Task<IEnumerable<Project>> GetProjectsAsync(CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            return await conn.QueryAsync<Project>("select * from projects");
        }

        public async Task<int> PutProjectAsync(Project project, CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            return await conn.ExecuteAsync("INSERT OR REPLACE INTO projects (id, name) VALUES (@Id, @Name)", new
            {
                Id = project.Id,
                Name = project.Name,
            });
        }
    }
}
