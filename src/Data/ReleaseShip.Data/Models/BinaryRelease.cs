using System;
using System.Collections.Generic;
using System.Text;

namespace ReleaseShip.Data.Models
{
    public class BinaryRelease
    {
        public int Id { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public long PublishDateUtc { get; set; }
        public string PlatformId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
