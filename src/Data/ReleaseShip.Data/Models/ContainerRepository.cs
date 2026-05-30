namespace ReleaseShip.Data.Models
{
    public class ContainerRepository
    {
        public int Id { get; set; }
        public string Namespace { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AllowAnonymousPull { get; set; }
        public bool AllowDelete { get; set; }
        public string DefaultTagMutability { get; set; } = ContainerTagMutabilityModes.Mutable;
        public long CreatedDateUtc { get; set; }
        public long UpdatedDateUtc { get; set; }
    }
}
