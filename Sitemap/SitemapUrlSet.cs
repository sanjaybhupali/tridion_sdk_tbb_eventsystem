using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SitemapNavigation
{
    using System.Xml.Serialization;

    [XmlRoot("urlset", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public class SitemapUrlSet
    {
        [XmlElement("url")]
        public List<SitemapUrl> Urls { get; set; } = new List<SitemapUrl>();
    }

    public class SitemapUrl
    {
        [XmlElement("loc")]
        public string Loc { get; set; } // The full URL

        [XmlElement("lastmod")]
        public string LastMod { get; set; } // Format: YYYY-MM-DD

        [XmlElement("changefreq")]
        public string ChangeFreq { get; set; } // e.g., weekly

        [XmlElement("priority")]
        public string Priority { get; set; } // e.g., 0.5

        // Your custom field
        [XmlElement("categoryTitle")]
        public string CategoryTitle { get; set; }

        [XmlElement("RouteName")]
        public string RouteName { get; set; }

        [XmlElement("DisplayTitle")]
        public string DisplayTitle { get; set; }

        [XmlElement("VisibilityStatus")]
        public string VisibilityStatus { get; set; }
    }
}
