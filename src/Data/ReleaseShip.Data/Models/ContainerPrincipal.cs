namespace ReleaseShip.Data.Models
{
    public class ContainerPrincipal
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public bool IsBootstrapAdmin { get; set; }
        public long CreatedDateUtc { get; set; }
        public long? DisabledDateUtc { get; set; }
    }
}
