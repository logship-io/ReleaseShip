namespace ReleaseShip.Models.RSS
{
    public class RssItem
    {
        public string? Title { get; set; } 
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTimeOffset PubDate { get; set; }
        public string? Author { get; set; }
        public RssEnclosure? Enclosure { get; set; }    
    }
}
