namespace ReleaseShip.Data.Models
{
    public class ContainerAuthenticatedPrincipal
    {
        public int PrincipalId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool CanPull { get; set; }
        public bool CanPush { get; set; }
        public bool CanDelete { get; set; }
        public string ScopeType { get; set; } = "global";
        public string ScopeValue { get; set; } = string.Empty;
        public string AuthenticationMode { get; set; } = string.Empty;
    }
}
