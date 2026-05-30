namespace ReleaseShip.Data.Models
{
    public class ContainerManifestBlob
    {
        public int ManifestId { get; set; }
        public string Digest { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
