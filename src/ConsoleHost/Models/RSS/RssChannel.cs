using System.Xml.Serialization;

namespace ReleaseShip.Models.RSS
{
    public class RssChannel
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Description { get; set; }

        [XmlElement("item")]
        public List<RssItem>? Items { get; set; }

    }
}
