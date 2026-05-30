namespace ReleaseShip.Data.Models
{
    public class ContainerManifest
    {
        public int Id { get; set; }
        public int RepositoryId { get; set; }
        public string Digest { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public string? ArtifactType { get; set; }
        public string? SubjectDigest { get; set; }
        public string? ConfigDigest { get; set; }
        public byte[] ContentBytes { get; set; } = [];
        public long CreatedDateUtc { get; set; }
    }
}
