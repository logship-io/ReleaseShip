using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Services
{
    public interface IPlatformService
    {
        Task AddPlatform(string platformId, CancellationToken token);
    }
}
