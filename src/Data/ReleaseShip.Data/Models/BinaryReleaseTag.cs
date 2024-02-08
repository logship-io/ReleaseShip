using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Models
{
    public class BinaryReleaseTag
    {
        public string Id { get; set; } = string.Empty;
        public int BinReleaseId { get; set; }
        public long PublishDateUtc {  get; set; }
        public string PlatformId { get; set; } = string.Empty;
    }
}
