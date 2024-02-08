namespace ReleaseShip.ConsoleHost.Models
{
    public class ReleaseUploadModel
    {
        public string PlatformId { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new();

        public IFormFile? Archive { get; set; }
    }
}
