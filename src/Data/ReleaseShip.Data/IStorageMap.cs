using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Data
{
    public interface IStorageMap
    {
        Task<string> MapFilePathAsync(string id);
    }
}
