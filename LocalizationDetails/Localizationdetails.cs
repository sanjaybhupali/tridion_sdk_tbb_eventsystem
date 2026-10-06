using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Tridion.ContentManager.Templating;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.ContentManagement.Fields;
using Tridion.ContentManager.Templating.Assembly;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tridion.ContentManager;
using static System.Collections.Specialized.BitVector32;
using System.Globalization;
using Tridion.ContentManager.Publishing;

namespace TaxonomySitemapNavigation
{
    [TcmTemplateTitle("Localization Details")]
    public class Localizationdetails : ITemplate
    {
        private Package _package;
        private Engine _engine;
        private Component _localizationConfigurationComponent;
        private Session _session;
        private static readonly TemplatingLogger log = TemplatingLogger.GetLogger(typeof(Localizationdetails));

        public void Transform(Engine engine, Package package)
        {
            //Initialize
            this._engine = engine;
            this._package = package;

            try
            {
                //   System.Diagnostics.Debugger.Launch();
                log.Info($"Inside GetJsonContent Transform");


                Page page = null;
                Item outputItem = Package.GetByType(ContentType.Page);
                if (outputItem != null)
                {
                    page = engine.GetObject(outputItem.GetAsSource().GetValue("ID")) as Page;
                }
                else
                {
                    throw new InvalidOperationException("No Page found.  Verify that this template is used with a Page.");
                }

                // Get the page component
              //  _localizationConfigurationComponent = page.ComponentPresentations[0].Component;

                // get the publication object
                var publication = GetPublication(page);
                this._session = engine.GetSession();



                string jsonContent = string.Empty;
                var localizationData = GetChildPublicationDetails(publication);

                string summariesJson = JsonSerialize(localizationData, IsPreview);


                if (string.IsNullOrEmpty(summariesJson))
                {
                    //  throw new DxaException("Output Json should not be empty.");
                    log.Error(" Output Json should not be empty. ");
                }

                // 1. Parse the string into a JArray object
                //   JArray cleanArray = JArray.Parse(summariesJson);

                // 2. Put the JArray (the object) into your response, NOT the .ToString() result
                //   var response = new
                //     {
                //         status = "success",
                //         localizations = cleanArray // Passing the object, not a string
                //     };

                // 3. Serialize the WHOLE response once at the very end
                //  string finalOutput = JsonConvert.SerializeObject(summariesJson);

                outputItem = Package.CreateStringItem(ContentType.Text, summariesJson);

                Package.PushItem(Package.OutputName, outputItem);

                //   package.PushItem(Package.OutputName, package.CreateStringItem(ContentType.Text, summariesJson.ToString()));

            }
            catch (Exception ex)
            {
                log.Error($" Error TaxonomySitemapNav Error occured : {ex.Message} ");
            }
        }
        protected Package Package
        {
            get
            {
                if (_package == null)
                {
                    log.Error(" Initialize has not been called. ");
                    //  throw new DxaException("Initialize has not been called.");
                }
                return _package;
            }
            set
            {
                // Allows dependency injection for unit test purposes.
                _package = value;
            }
        }
        protected string JsonSerialize(object objectToSerialize, bool prettyPrint = false,
            JsonSerializerSettings settings = null)
        {
            if (settings == null)
            {
                settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
            }

            Newtonsoft.Json.Formatting jsonFormatting = prettyPrint
                ? Newtonsoft.Json.Formatting.Indented
                : Newtonsoft.Json.Formatting.None;

            return JsonConvert.SerializeObject(objectToSerialize, jsonFormatting, settings);
        }

        protected bool IsPreview
            => (_engine.RenderMode == RenderMode.PreviewDynamic) || (_engine.RenderMode == RenderMode.PreviewStatic);

        /*
                private List<SiteLocalizationData> DetermineSiteLocalizations(Session session, Engine engine,Publication contextPublication)
                {
                    string siteId = GetPublicationMetadataValue(contextPublication,"siteId");

                    Publication master = GetMasterPublication(contextPublication);
                //    Logger.Debug(String.Format("Master publication is : {0}, siteId is {1}", master.Title, siteId));
                    List<SiteLocalizationData> siteLocalizations = new List<SiteLocalizationData>();
                    bool masterAdded = false;
                    if (GetPublicationMetadataValue(master, "siteId") == siteId)
                    {
                      var isMaster =  GetPublicationMetadataValue(master, "isMaster");
                        masterAdded = bool.TryParse(isMaster, out bool parsedValue) && parsedValue;

                          siteLocalizations.Add(GetPublicationDetails(master, masterAdded));
                    }
                    if (siteId != null)
                    {
                        siteLocalizations.AddRange(GetChildPublicationDetails(session , engine,master, siteId, masterAdded));
                    }
                    //It is possible that no publication has been set explicitly as the master
                    //in which case we set the context publication as the master
                    if (!siteLocalizations.Any(p => p.IsMaster))
                    {
                        string currentPubId = contextPublication.Id.ItemId.ToString(CultureInfo.InvariantCulture);
                        foreach (SiteLocalizationData pub in siteLocalizations)
                        {
                            if (pub.Id == currentPubId)
                            {
                                pub.IsMaster = true;
                            }
                        }
                    }
                    return siteLocalizations;
                }
        */

        public string GetFieldValue(ItemFields fields, string fieldName)
        {
            // Check if fields object is null or if the field doesn't exist
            if (fields == null || !fields.Contains(fieldName))
            {
                return string.Empty;
            }

            // Attempt to cast the field to a TextField
            TextField textField = fields[fieldName] as TextField;

            // Return the first value if it exists, otherwise return empty string
            return (textField != null && textField.Values.Count > 0)
                   ? textField.Value
                   : string.Empty;
        }

        private IEnumerable<SiteLocalizationData> GetChildPublicationDetails(Publication master)
        {
            //    System.Diagnostics.Debugger.Break();


            List<SiteLocalizationData> pubs = new List<SiteLocalizationData>();




            // Add parent


            var parentPublicationDetails = GetPublicationDetails(master);

            pubs.Add(parentPublicationDetails); // Parent


            // Get childern
            UsingItemsFilter filter = new UsingItemsFilter(_session) { ItemTypes = new List<ItemType> { ItemType.Publication } };
            foreach (XmlElement item in master.GetListUsingItems(filter).ChildNodes)
            {
                string id = item.GetAttribute("ID");
                Publication child = (Publication)_engine.GetObject(id);
                pubs.Add(GetPublicationDetails(child));
            }
            return pubs;
        }

        private SiteLocalizationData GetPublicationDetails(Publication pub)
        {
         //   System.Diagnostics.Debugger.Launch();




            SiteLocalizationData pubData = new SiteLocalizationData
            {
                Id = pub.Id.ItemId.ToString(CultureInfo.InvariantCulture),
                Path = pub.PublicationUrl,

            };

            if (!string.IsNullOrEmpty(pub.Locale))
            {
                CultureInfo metadata = new CultureInfo(pub.Locale);
                pubData.Language = metadata.DisplayName;
            }
            else
            {
                pubData.Language = pub.Locale;
            }
            /*
                        TcmUri localUri = new TcmUri(_localizationConfigurationComponent.Id.ItemId, ItemType.Component, pub.Id.ItemId);
                        Component locComp = (Component)_engine.GetObject(localUri);


                        if (locComp != null)
                        {
                            // var s = component.Content;
                            // Get the fields and schema
                            ItemFields fields = new ItemFields(locComp.Content, locComp.Schema);
                            foreach (ItemFields field in fields.GetEmbeddedFields("settings"))
                            {
                                if (field.GetTextValue("name") == "language")
                                {
                                    pubData.Language = field.GetTextValue("value");
                                    break;
                                }
                            }
                        }
            */
            return pubData;
        }


        private Publication GetMasterPublication(Publication contextPublication)
        {
            string siteId = GetPublicationMetadataValue(contextPublication, "siteId");
            List<Publication> validParents = new List<Publication>();
            if (siteId != null && siteId != "multisite-master")
            {
                foreach (Repository item in contextPublication.Parents)
                {
                    Publication parent = (Publication)item;
                    if (IsCandidateMaster(parent, siteId))
                    {
                        validParents.Add(parent);
                    }
                }
            }
            if (validParents.Count > 1)
            {
                //  Logger.Error(String.Format("Publication {0} has more than one parent with the same (or empty) siteId {1}. Cannot determine site grouping, so picking the first parent: {2}.", contextPublication.Title, siteId, validParents[0].Title));
            }
            return validParents.Count == 0 ? contextPublication : GetMasterPublication(validParents[0]);
        }
        private bool IsCandidateMaster(Publication pub, string childId)
        {
            //A publication is a valid master if:
            //a) Its siteId is "multisite-master" or
            //b) Its siteId matches the passed (child) siteId
            string siteId = GetPublicationMetadataValue(pub, "siteId");
            return siteId == "multisite-master" || childId == siteId;
        }

        public string GetPublicationMetadataValue(Publication pub, string fieldName)
        {
            // 1. Check if metadata exists
            if (pub.Metadata == null || pub.MetadataSchema == null)
            {
                return string.Empty;
            }

            // 2. Load the fields
            ItemFields meta = new ItemFields(pub.Metadata, pub.MetadataSchema);

            // 3. Check if the field exists and is a TextField
            if (meta.Contains(fieldName))
            {
                TextField field = meta[fieldName] as TextField;
                if (field != null && field.Values.Count > 0)
                {
                    return field.Value; // Returns the first value
                }
            }

            return string.Empty;
        }


        protected Publication GetPublication(Page page)
        {

            RepositoryLocalObject inputItem = (RepositoryLocalObject)page;
            if (inputItem == null)
            {
                // throw new DxaException("Unable to determine the context Publication.");
            }

            return (Publication)inputItem.ContextRepository;
        }
        public object ConvertXmlToJson(string xmlInput)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlInput);

            // This converts the XML nodes into a JSON string
            // Set 'omitRootObject: true' to remove the <Language> wrapper
            string json = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.None, true);
            return JObject.Parse(json);
            //  return json;
        }

    }
    public class LocalizationData
    {
        [JsonProperty("defaultLocalization")]
        public bool IsDefaultLocalization { get; set; }

        [JsonProperty("staging")]
        public bool IsXpmEnabled { get; set; }

        [JsonProperty("mediaRoot")]
        public string MediaRoot { get; set; }

        [JsonProperty("siteLocalizations")]
        public SiteLocalizationData[] SiteLocalizations { get; set; }

        [JsonProperty("files")]
        public string[] ConfigStaticContentUrls { get; set; }

        [JsonProperty("dataPresentationTemplateUri")]
        public string DataPresentationTemplateUri { get; set; }
    }
    public class SiteLocalizationData
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Language { get; set; }
        //  public bool IsMaster { get; set; }
    }


}
