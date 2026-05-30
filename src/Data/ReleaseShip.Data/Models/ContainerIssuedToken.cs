namespace ReleaseShip.Data.Models
{
    public class ContainerIssuedToken
    {
        public int Id { get; set; }
        public string PrincipalUsername { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string ScopeType { get; set; } = string.Empty;
        public string ScopeValue { get; set; } = string.Empty;
        public bool CanPull { get; set; }
        public bool CanPush { get; set; }
        public bool CanDelete { get; set; }
    }
}
