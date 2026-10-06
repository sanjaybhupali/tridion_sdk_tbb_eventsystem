using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Tridion.ContentManager;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.ContentManagement.Fields;
using Tridion.ContentManager.Publishing;
using Tridion.ContentManager.Templating;
using Tridion.ContentManager.Templating.Assembly;
using static System.Collections.Specialized.BitVector32;

namespace SitemapNavigation
{
    public class SitemapNav : ITemplate
    {
        private Engine engine;
        private Package package;
        private static readonly TemplatingLogger log = TemplatingLogger.GetLogger(typeof(SitemapNav));

        public void Transform(Engine engine, Package package)
        {
           // System.Diagnostics.Debugger.Launch();
            //Initialize
            this.engine = engine;
            this.package = package;
            try
            {
                log.Info($"Inside TaxonomySitemapNav Transform");

                //   System.Diagnostics.Debugger.Launch();
                //   System.Diagnostics.Debugger.Break();
                Page page = null;
                List<TaxonomyBasedNavigationItem> taxonomyBasedNavigationItems = new List<TaxonomyBasedNavigationItem>();

                Item pageItem = package.GetByType(ContentType.Page);
                if (pageItem != null)
                {
                    page = engine.GetObject(pageItem.GetAsSource().GetValue("ID")) as Page;
                }
                else
                {
                    throw new InvalidOperationException("No Page found.  Verify that this template is used with a Page.");
                }
                // get the publication object


                string categoryId = string.Empty;
                string xmlContent = string.Empty;



                // str grp
                if (page != null)
                {
                    StructureGroup currentSG = page.OrganizationalItem as StructureGroup;

                    if (currentSG != null)
                    {
                        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
                        XElement urlSet = new XElement(ns + "urlset");

                        Action<StructureGroup> processStructureGroup = null;
                        processStructureGroup = (sg) =>
                        {
                            string webDav = sg.WebDavUrl;
                            string currentPath = string.Empty;

                            if (!string.IsNullOrEmpty(webDav))
                            {
                                string[] segments = webDav.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 3)
                                {
                                    currentPath = string.Join("/", segments.Skip(3));
                                }
                            }

                            OrganizationalItemItemsFilter pageFilter = new OrganizationalItemItemsFilter(engine.GetSession());
                            pageFilter.ItemTypes = new ItemType[] { ItemType.Page };

                            foreach (IdentifiableObject item in sg.GetItems(pageFilter))
                            {
                                Page childPage = item as Page;
                                if (childPage != null)
                                {
                                    string fileName = childPage.FileName;
                                    string pageFile = fileName.Equals("index", StringComparison.OrdinalIgnoreCase) ? "index.html" : fileName + ".html";

                                    if (!childPage.OrganizationalItem.Title.StartsWith("_") && !childPage.Title.StartsWith("_") && (childPage.PublishLocationUrl != null) && childPage.PublishLocationUrl.EndsWith(".html"))
                                    {
                                        string loc = string.IsNullOrEmpty(currentPath) ? "/" + pageFile : "/" + currentPath + "/" + pageFile;

                                        XElement urlElement = new XElement(ns + "url",
                                            new XElement(ns + "loc", childPage.PublishLocationUrl),
                                             new XElement(ns + "lastmod", childPage.RevisionDate.ToString("yyyy-MM-dd"))

                                        );
                                        urlSet.Add(urlElement);
                                    }
                                }
                            }

                            OrganizationalItemItemsFilter sgFilter = new OrganizationalItemItemsFilter(engine.GetSession());
                            sgFilter.ItemTypes = new ItemType[] { ItemType.StructureGroup };

                            foreach (IdentifiableObject item in sg.GetItems(sgFilter))
                            {
                                StructureGroup nestedSG = item as StructureGroup;
                                if (nestedSG != null)
                                {
                                    processStructureGroup(nestedSG);
                                }
                            }
                        };

                        processStructureGroup(currentSG);

                        // Generate the raw XML string
                        string innerXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + urlSet.ToString(SaveOptions.DisableFormatting);

                        // Safely escape backslashes and double quotes for raw JSON embedding
                        //    string escapedXml = innerXml.Replace("\\", "\\\\").Replace("\"", "\\\"");

                        // Construct the JSON structure manually to avoid \u003c / \u003e encoding
                        string jsonContent = string.Format(
                            "{0}",
                            innerXml
                        );

                        // Push the final JSON string into the package output
                        package.PushItem(Package.OutputName, package.CreateStringItem(ContentType.Text, jsonContent));
                    }
                }

                // end str grp


            }
            catch (Exception ex)
            {
                log.Debug($"Debug TaxonomySitemapNav Error occured : {ex.Message} ");

                log.Error($"Error TaxonomySitemapNav Error occured : {ex.Message} ");
            }
        }

        private string GenerateSitemapXml(List<TaxonomyBasedNavigationItem> items)
        {
            var sitemapSet = new SitemapUrlSet();

            // 1. Flatten all navigation items into one big list of URLs
            foreach (var item in items)
            {
                SitemapGenerator.FlattenAndPopulate(item, sitemapSet.Urls);
            }
            // --- CLEANUP LOGIC STARTS HERE ---
            // 1. Create a blank namespace container
            var ns = new XmlSerializerNamespaces();

            // 2. Add only the sitemaps.org namespace with no prefix (the empty string "")
            ns.Add("", "http://www.sitemaps.org/schemas/sitemap/0.9");
            // 1. Define your formatting settings
            var settings = new XmlWriterSettings
            {
                Indent = true,                // Set to 'false' to remove \r\n and spaces
                IndentChars = "  ",           // Use two spaces for indentation
                NewLineChars = "",        // This is where those characters come from
                OmitXmlDeclaration = false,   // Keeps the <?xml...?> header
                Encoding = System.Text.Encoding.UTF8
            };

            var serializer = new XmlSerializer(typeof(SitemapUrlSet));

            using (var sw = new Utf8StringWriter())
            {
                // 2. Use XmlWriter with your settings
                using (var writer = XmlWriter.Create(sw, settings))
                {
                    serializer.Serialize(writer, sitemapSet, ns);
                }
                return sw.ToString();
            }
        }

        // Helper to handle UTF-8 encoding (Standard StringWriter defaults to UTF-16)
        public class Utf8StringWriter : StringWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        }

        public static TaxonomyBasedNavigationItem FindById(IEnumerable<TaxonomyBasedNavigationItem> items, string id)
        {
            foreach (var item in items)
            {
                if (item.id == id)
                    return item;

                if (item.childTaxonomyBasedNavigationItem != null)
                {
                    var found = FindById(item.childTaxonomyBasedNavigationItem, id);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        public static string ConvertToXml(List<TaxonomyBasedNavigationItem> items)
        {
            var serializer = new XmlSerializer(typeof(List<TaxonomyBasedNavigationItem>), new XmlRootAttribute("Navigation"));

            var settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineHandling = NewLineHandling.None,
                OmitXmlDeclaration = false
            };

            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");


            using (var sw = new Utf8StringWriter())
            {

                using (var xw = XmlWriter.Create(sw, settings))
                {

                    serializer.Serialize(xw, items, ns);
                    string xml = sw.ToString();
                    return xml;
                }


            }
        }

        public static string workingConvertToXml(List<TaxonomyBasedNavigationItem> items)
        {


            var serializer = new XmlSerializer(typeof(List<TaxonomyBasedNavigationItem>), new XmlRootAttribute("Navigation"));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            using (var writer = new Utf8StringWriter())
            {
                serializer.Serialize(writer, items, ns);
                return writer.ToString();
            }
        }

        /*
                public class Utf8StringWriter : StringWriter
                {
                    public override Encoding Encoding
                    {
                        get { return Encoding.UTF8; }
                    }
                }
        */
    }

    [XmlRoot("NavigationItem")]
    public class TaxonomyBasedNavigationItem
    {
        public string title { get; set; }
        public string id { get; set; }
        public bool isRoot { get; set; }

        [XmlElement("Child")]
        public List<TaxonomyBasedNavigationItem> childTaxonomyBasedNavigationItem = new List<TaxonomyBasedNavigationItem>();

        [XmlElement("UrlMap")]
        public List<UrlMapNavigationItem> urlMapNavigationItem = new List<UrlMapNavigationItem>();

    }

    [XmlRoot("UrlMapNavigationItem")]
    public class UrlMapNavigationItem
    {
        public string url { get; set; }
        public string categoryTitle { get; set; }
        public string keywordId { get; set; }
        public string key { get; set; }
        public string routeName { get; set; }
        public string displayTitle { get; set; }
        public string visibilityStatus { get; set; }

    }
}
