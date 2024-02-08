using Microsoft.Extensions.DependencyInjection;
using ReleaseShip.Data.Relational;
using ReleaseShip.Data.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Data.SQLite
{
    public static class Extensions
    {
        public static IServiceCollection AddReleaseShipSqlite(this IServiceCollection services, string connectionString)
        {
            return services.AddSingleton<IDatabaseContext>(_ =>
            {
                var sql = new SQLiteDatabaseContext(connectionString);
                return sql;
            }).AddReleaseShipData();
        }
    }
}
