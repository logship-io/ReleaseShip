namespace ReleaseShip.Data.Models
{
    public class ContainerToken
    {
        public int Id { get; set; }
        public int PrincipalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HashedSecret { get; set; } = string.Empty;
        public string ScopeType { get; set; } = string.Empty;
        public string ScopeValue { get; set; } = string.Empty;
        public bool CanPull { get; set; }
        public bool CanPush { get; set; }
        public bool CanDelete { get; set; }
        public long CreatedDateUtc { get; set; }
        public long? LastUsedDateUtc { get; set; }
        public long? RevokedDateUtc { get; set; }
    }
}
