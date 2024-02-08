using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ReleaseShip.Data.Relational;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Data.SQLite
{
    public class SQLiteDatabaseContext : IDatabaseContext
    {
        private readonly string connectionString;

        public SQLiteDatabaseContext(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new SqliteConnection(this.connectionString);
        }

        public ValueTask<IDbConnection> GetConnection(CancellationToken token)
        {
            return new ValueTask<IDbConnection>(CreateConnection());
        }

        public Task InitializeAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
