namespace ReleaseShip.Data.Models
{
    public class ContainerManifestReference
    {
        public ContainerManifest Manifest { get; set; } = new();
        public string? TagName { get; set; }
    }
}
