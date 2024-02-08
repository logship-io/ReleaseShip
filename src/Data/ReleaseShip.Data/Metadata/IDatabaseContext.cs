using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Relational
{
    public interface IDatabaseContext
    {
        ValueTask<IDbConnection> GetConnection(CancellationToken token);

        Task InitializeAsync(CancellationToken token);
    }
}
