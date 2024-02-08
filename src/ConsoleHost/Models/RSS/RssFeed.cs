using System.Xml.Serialization;

namespace ReleaseShip.Models.RSS
{
    [XmlRoot("rss")]
    public class RssFeed
    {
        [XmlAttribute("version")]
        public string? Version { get; set; }

        [XmlElement("channel")]
        public RssChannel? Channel { get; set; }
    }
}
