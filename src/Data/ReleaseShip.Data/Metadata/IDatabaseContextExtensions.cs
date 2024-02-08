using Dapper;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Relational;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Metadata
{
    public static partial class IDatabaseContextExtensions
    {
        [LoggerMessage(LogLevel.Information, "Created {Total} Tables for database initialization.")]
        internal static partial void log_TablesInitialized(ILogger logger, int total);

        public static async Task InitializeDatabaseAsync(this IDatabaseContext ctx, ILogger logger, CancellationToken token)
        {
            await ctx.InitializeAsync(token);
            using var conn = await ctx.GetConnection(token);
            int total = await conn.ExecuteAsync(
                @"CREATE TABLE IF NOT EXISTS projects (id TEXT PRIMARY KEY, name TEXT);
                CREATE TABLE IF NOT EXISTS platforms (id TEXT PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS bin_release (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    project_id TEXT, 
                    platform_id TEXT, 
                    publish_date_utc INTEGER,
                    path TEXT,
                    FOREIGN KEY (project_id) REFERENCES projects (id)
                    FOREIGN KEY (platform_id) REFERENCES platforms (id)
                );
                CREATE TABLE IF NOT EXISTS bin_release_tags (
                    id TEXT,
                    bin_release_id INTEGER,
                    PRIMARY KEY (id, bin_release_id)
                    FOREIGN KEY (bin_release_id) REFERENCES bin_release (id)
                );");
            log_TablesInitialized(logger, total);
        }
    }
}
