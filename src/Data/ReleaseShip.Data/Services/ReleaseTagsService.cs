using Dapper;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public class ReleaseTagsService : IReleaseTagsService
    {
        private readonly IDatabaseContext ctx;
        private readonly ILogger<ReleaseTagsService> logger;

        public ReleaseTagsService(IDatabaseContext context, ILogger<ReleaseTagsService> logger)
        {
            this.ctx = context;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<BinaryReleaseTag>> GetTagsAsync(string projectId, CancellationToken token, DateTime? sinceUtc = null)
        {
            string sql = "select brt.*, b.publish_date_utc, b.platform_id from bin_release_tags brt join bin_release b on b.id = brt.bin_release_id";
            if (sinceUtc != null)
            {
                sql += " where b.publish_date_utc >= @SinceUtc";
            }

            using var conn = await this.ctx.GetConnection(token);
            var since = (long)((sinceUtc ??  DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds;
            
            var results = await conn.QueryAsync<BinaryReleaseTag>(sql, new
            {
                ProjectId = projectId,
                SinceUtc =  since,
            });

            return results.ToList();
        }

        public async Task PutTagsAsync(int binaryReleaseId, IEnumerable<string> tags, CancellationToken token)
        {
            using var conn = await this.ctx.GetConnection(token);
            conn.Open();
            using var tx = conn.BeginTransaction();
            foreach (var tag in tags)
            {
                var cmd = new CommandDefinition("insert or ignore into bin_release_tags (id, bin_release_id) values (@Id, @ReleaseId)", new
                {
                    Id = tag,
                    ReleaseId = binaryReleaseId,
                }, transaction: tx, cancellationToken: token);
                await conn.ExecuteAsync(cmd);
            }

            tx.Commit();
        }
    }
}
