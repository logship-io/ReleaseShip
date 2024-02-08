using Dapper;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Models;
using ReleaseShip.Data.Relational;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    internal abstract class BaseBinaryStorageService : IBinaryStorageService
    {
        private readonly IDatabaseContext ctx;
        private readonly ILogger<BaseBinaryStorageService> logger;

        public BaseBinaryStorageService(IDatabaseContext context, ILogger<BaseBinaryStorageService> logger)
        {
            this.ctx = context;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<BinaryRelease>> GetBinariesAsync(string projectId, DateTimeOffset sinceUtc, CancellationToken token)
        {
            using var conn = await ctx.GetConnection(token);
            var results = await conn.QueryAsync<BinaryRelease>("select * from bin_release where project_id = @ProjectId", new
            {
                ProjectId = projectId,
            });

            return results.ToList();
        }

        public async Task<BinaryRelease?> GetBinaryReleaseAsync(string projectId, string tag, string platformId, CancellationToken token)
        {
            const string sql = @"SELECT br.*
                FROM bin_release br
                JOIN bin_release_tags brt ON br.id = brt.bin_release_id
                WHERE br.project_id = @ProjectId
                  AND br.platform_id = @PlatformId
                  AND brt.id = @BinReleaseTagId
                ORDER BY br.publish_date_utc DESC
                LIMIT 1;";
            using var conn = await this.ctx.GetConnection(token);
            var result = await conn.QuerySingleOrDefaultAsync<BinaryRelease?>(sql, new
            {
                ProjectId = projectId,
                PlatformId = platformId,
                BinReleaseTagId = tag,
            });

            return result;
        }

        public async Task<BinaryRelease?> GetBinaryReleaseAsync(int binaryReleaseId, CancellationToken token)
        {
            const string sql = "SELECT * FROM bin_release where id = @Id";
            using var conn = await this.ctx.GetConnection(token);
            var result = await conn.QuerySingleOrDefaultAsync<BinaryRelease?>(sql, new
            {
                Id = binaryReleaseId,
            });

            return result;
        }

        protected async Task<int> StoreBinaryReleaseAsync(string projectId, BinaryUpload model, string path, CancellationToken token)
        {
            const string sql = @"INSERT INTO bin_release (project_id, platform_id, path, publish_date_utc)
                VALUES (@ProjectId, @PlatformId, @Path, @PublishDateUtc);
                SELECT last_insert_rowid();";
            using var conn = await this.ctx.GetConnection(token);
            var result = await conn.ExecuteScalarAsync<int>(sql, new
            {
                ProjectId = projectId,
                PlatformId = model.PlatformId,
                Path = path,
                PublishDateUtc = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds,
            });

            return result;
        }

        public abstract Task<Stream?> RetrieveAsync(int binaryReleaseId, CancellationToken token);
        public abstract Task<int> StoreAsync(string projectId, BinaryUpload model, Stream fileData, CancellationToken token);

        
    }
}
