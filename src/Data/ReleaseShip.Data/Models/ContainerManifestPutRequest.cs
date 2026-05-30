using System.Collections.Generic;

namespace ReleaseShip.Data.Models
{
    public class ContainerManifestPutRequest
    {
        public string RepositoryName { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public byte[] ContentBytes { get; set; } = [];
        public IReadOnlyList<string> ReferencedBlobDigests { get; set; } = [];
        public string? ConfigDigest { get; set; }
        public int SchemaVersion { get; set; }
        public string? ArtifactType { get; set; }
        public string? SubjectDigest { get; set; }
    }
}
