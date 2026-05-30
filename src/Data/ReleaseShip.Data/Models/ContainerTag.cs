namespace ReleaseShip.Data.Models
{
    public class ContainerTag
    {
        public int RepositoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ManifestId { get; set; }
        public long CreatedDateUtc { get; set; }
        public long UpdatedDateUtc { get; set; }
    }
}
