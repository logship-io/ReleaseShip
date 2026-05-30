namespace ReleaseShip.Data.Models
{
    public class ContainerTagRule
    {
        public int Id { get; set; }
        public int RepositoryId { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public bool IsImmutable { get; set; }
        public long CreatedDateUtc { get; set; }
    }
}
