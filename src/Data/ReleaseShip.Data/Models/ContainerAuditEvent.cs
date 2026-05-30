namespace ReleaseShip.Data.Models
{
    public class ContainerAuditEvent
    {
        public int Id { get; set; }
        public string? PrincipalName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetValue { get; set; } = string.Empty;
        public long CreatedDateUtc { get; set; }
        public string? MetadataJson { get; set; }
    }
}
