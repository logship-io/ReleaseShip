namespace ReleaseShip.ConsoleHost.Models
{
    public class RegistryTokenIssueModel
    {
        public string PrincipalUsername { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ScopeType { get; set; } = "global";
        public string ScopeValue { get; set; } = string.Empty;
        public bool CanPull { get; set; } = true;
        public bool CanPush { get; set; }
        public bool CanDelete { get; set; }
    }
}
