using System.Xml.Serialization;

namespace ReleaseShip.Models.RSS
{
    public class RssEnclosure {
        [XmlAttribute] public string? Url { get; set; }
        [XmlAttribute] public string? Type {  get; set; }
        [XmlAttribute] public int Length {  get; set; }
    }
}