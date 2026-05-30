namespace ReleaseShip.Data.Models
{
    public class ContainerUpload
    {
        public string Id { get; set; } = string.Empty;
        public int RepositoryId { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public long OffsetBytes { get; set; }
        public long StartedDateUtc { get; set; }
        public long ExpiresDateUtc { get; set; }
        public string State { get; set; } = string.Empty;
        public string? CompletedDigest { get; set; }
    }
}
