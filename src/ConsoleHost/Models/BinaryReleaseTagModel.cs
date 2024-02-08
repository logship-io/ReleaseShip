using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Models
{
    public class BinaryReleaseTagModel
    {
        public string Id { get; set; } = string.Empty;
        public int BinReleaseId { get; set; }
        public DateTime CreateDateUtc {  get; set; } 

        public string PlatformId { get; set; } = string.Empty;
    }
}
