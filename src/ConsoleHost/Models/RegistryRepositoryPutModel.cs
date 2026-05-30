namespace ReleaseShip.ConsoleHost.Models
{
    public class RegistryRepositoryPutModel
    {
        public string? Description { get; set; }
        public bool AllowAnonymousPull { get; set; }
        public bool AllowDelete { get; set; }
        public string DefaultTagMutability { get; set; } = string.Empty;
        public IReadOnlyList<string> ProtectedTagPatterns { get; set; } = [];
    }
}
