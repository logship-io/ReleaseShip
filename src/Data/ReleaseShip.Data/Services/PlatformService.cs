using Dapper;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Relational;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public class PlatformService : IPlatformService
    {
        private readonly IDatabaseContext ctx;
        private readonly ILogger<PlatformService> logger;

        public PlatformService(IDatabaseContext ctx, ILogger<PlatformService> logger)
        {
            this.ctx = ctx;
            this.logger = logger;
        }

        public async Task AddPlatform(string platformId, CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            await conn.ExecuteAsync("insert or ignore into platforms (id) values (@Id)", new
            {
                Id = platformId,
            });
        }
    }
}
