namespace ReleaseShip.Data.Models
{
    public class ContainerBlob
    {
        public string Digest { get; set; } = string.Empty;
        public string Algorithm { get; set; } = "sha256";
        public long SizeBytes { get; set; }
        public string? MediaType { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public long CreatedDateUtc { get; set; }
    }
}
