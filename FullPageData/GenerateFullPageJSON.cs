// Do NOT add: using System.Xml         (conflicts with Newtonsoft.Json.Formatting)
// Do NOT add: using Tridion.ContentManager.Templating (ambiguous ComponentPresentation)
using System;
using System.Collections.Generic;
using Tridion.ContentManager;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.ContentManagement.Fields;
using Tridion.ContentManager.Templating;
using Tridion.ContentManager.Templating.Assembly;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using CmComponentPresentation =
    Tridion.ContentManager.CommunicationManagement.ComponentPresentation;
using Tridion.ContentManager.CommunicationManagement.Regions;

[TcmTemplateTitle("Generate Full Page JSON")]
public class GenerateFullPageJson : ITemplate
{
    private Engine _engine;
    private Package _package;
    private TemplatingLogger _logger;

    private const string FIELD_REGION_NAME = "regionName";
    private const string FIELD_ENTITY_VIEW = "entityViewName";
    private const string FIELD_SCHEMA_ROOT_ELEM = "schemaRootElement";

    // ── Entry point ──────────────────────────────────────────────────────────
    public void Transform(Engine engine, Package package)
    {
        _engine = engine;
        _package = package;
        _logger = TemplatingLogger.GetLogger(GetType());
     //  System.Diagnostics.Debugger.Launch();
      //  System.Diagnostics.Debugger.Break();
        try
        {
            // Step 1 — get the page item from the package
            Item pageItem = package.GetByName(Package.PageName);
            if (pageItem == null)
            {
                _logger.Error("Package does not contain a Page item.");
                return;
            }

            // Step 2 — resolve the URI string
            string pageUri = pageItem.GetAsSource().GetValue("ID");
            if (string.IsNullOrEmpty(pageUri))
            {
                _logger.Error("Page item has no ID value in package source.");
                return;
            }

            // Step 3 — load the Page object
            Page page = engine.GetObject(new TcmUri(pageUri)) as Page;
            if (page == null)
            {
                _logger.Error("engine.GetObject returned null for URI: " + pageUri);
                return;
            }

            _logger.Info("Building JSON for page: " + page.Title + " [" + page.Id + "]");

            // Step 4 — build the JSON
            JObject pageJson = BuildPageJson(page);
            string jsonOutput = pageJson.ToString(Newtonsoft.Json.Formatting.Indented);

            // Step 5 — push to package (OutputName item may or may not pre-exist)
            package.PushItem("PageJson",
                package.CreateStringItem(ContentType.Html, jsonOutput));

            Item outputItem = package.GetByName(Package.OutputName);
            if (outputItem != null)
                outputItem.SetAsString(jsonOutput);
            else
                package.PushItem(Package.OutputName,
                    package.CreateStringItem(ContentType.Html, jsonOutput));
        }
        catch (Exception ex)
        {
            _logger.Error("GenerateFullPageJson failed: " + ex.Message
                          + "\nStack: " + ex.StackTrace);
            throw; // re-throw so Tridion marks publish as failed
        }
    }

    // ── Page root ─────────────────────────────────────────────────────────────
    private JObject BuildPageJson(Page page)
    {
        return new JObject
        {
            ["__typename"] = "Page",
            ["id"] = SafeStr(page.Id),
            ["tcmUri"] = SafeStr(page.Id),
            ["title"] = page.Title ?? "",
            ["urlPath"] = page.PublishLocationUrl ?? "",
            ["fileName"] = page.FileName ?? "",
            ["publicationId"] = page.OwningRepository?.Id.ToString() ?? "",
            ["publicationTitle"] = page.OwningRepository?.Title ?? "",
            ["pageTemplate"] = BuildPageTemplateJson(page),
            ["customMetas"] = BuildCustomMetasJson(page),
            ["customMetasStructure"] = BuildCustomMetaStructure(page),
            ["regions"] = BuildRegionsJson(page),
            ["publishedAt"] = DateTime.UtcNow.ToString("o"),
            ["isPublished"] = true
        };
    }

    // ── Page template ─────────────────────────────────────────────────────────
    private JObject BuildPageTemplateJson(Page page)
    {
        if (page.PageTemplate == null) return new JObject();
        return new JObject
        {
            ["id"] = SafeStr(page.PageTemplate.Id),
            ["title"] = page.PageTemplate.Title ?? "",
            ["fileExtension"] = page.PageTemplate.FileExtension ?? "",
            ["templateType"] = page.PageTemplate.TemplateType.ToString(),
            ["customMetas"] = BuildPageTemplateCustomMetasJson(page.PageTemplate),
        };
    }

    // ── Page custom metas ─────────────────────────────────────────────────────
    private JArray BuildCustomMetasJson(Page page)
    {
        var arr = new JArray();
        if (page.Metadata == null || page.MetadataSchema == null) return arr;
        try
        {
            var fields = new ItemFields(page.Metadata, page.MetadataSchema);
            foreach (ItemField f in fields)
            {
                if (f == null) continue;
                arr.Add(new JObject
                {
                    ["key"] = f.Name ?? "",
                    ["value"] = GetFieldValue(f),
                    ["fieldType"] = f.GetType().Name,
                    ["mandatory"] = f.Definition?.MinOccurs > 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("customMetas failed: " + ex.Message);
        }
        return arr;
    }

    // ── Page Template custom metas ─────────────────────────────────────────────────────
    private JArray BuildPageTemplateCustomMetasJson(PageTemplate pt)
    {
        var arr = new JArray();
        if (pt.Metadata == null || pt.MetadataSchema == null) return arr;
        try
        {
            var fields = new ItemFields(pt.Metadata, pt.MetadataSchema);
            foreach (ItemField f in fields)
            {
                if (f == null) continue;
                arr.Add(new JObject
                {
                    ["key"] = f.Name ?? "",
                    ["value"] = GetFieldValue(f),
                    ["fieldType"] = f.GetType().Name,
                    ["mandatory"] = f.Definition?.MinOccurs > 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("customMetas failed: " + ex.Message);
        }
        return arr;
    }

    private JArray BuildCustomMetaStructure(Page page)
    {
        var arr = new JArray();
        if (page.Metadata == null || page.MetadataSchema == null) return arr;
        try
        {
            var fields = new ItemFields(page.Metadata, page.MetadataSchema);
            foreach (ItemField f in fields)
            {
                if (f == null) continue;
                arr.Add(new JObject
                {
                    ["fieldName"] = f.Name ?? "",
                    ["fieldType"] = f.GetType().Name,
                    ["minOccurs"] = f.Definition?.MinOccurs ?? 0,
                    ["maxOccurs"] = f.Definition?.MaxOccurs ?? 1,
                    ["description"] = f.Definition?.Description ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("customMetaStructure failed: " + ex.Message);
        }
        return arr;
    }

    // ── Regions ───────────────────────────────────────────────────────────────
    private JArray BuildRegionsJsonorg(Page page)
    {
        var arr = new JArray();
        var regionNames = new List<string>();

        if (page.ComponentPresentations == null) return arr;

        foreach (EmbeddedRegion cp in page.Regions)
        {
            if (cp?.ComponentPresentations == null) continue;
          //  string rn = ResolveRegionNameFromCT(cp.RegionName) ?? "Main";
          // if (!regionNames.Contains(rn)) regionNames.Add(rn);
        }

        foreach (string regionName in regionNames)
        {
            arr.Add(new JObject
            {
                ["__typename"] = "Region",
                ["name"] = regionName,
                ["components"] = BuildComponentPresentations(page, regionName)
            });
        }
        return arr;
    }

    // ── Replace BuildRegionsJson with this ───────────────────────────────────────
    private JArray BuildRegionsJson(Page page)
    {
        var arr = new JArray();

        if (page.Regions == null || page.Regions.Count == 0)
        {
            _logger.Info("Page has no Regions: " + page.Title);
            return arr;
        }

        foreach (EmbeddedRegion embeddedRegion in page.Regions)
        {
            if (embeddedRegion == null) continue;

            _logger.Info("Processing region: " + embeddedRegion.RegionName
                + " | CPs: " + (embeddedRegion.ComponentPresentations?.Count ?? 0));

            var regionObj = new JObject
            {
                ["__typename"] = "Region",
                ["name"] = embeddedRegion.RegionName ?? "",
                ["componentPresentations"] = BuildCPsFromRegion(embeddedRegion)
            };

            // Nested regions — EmbeddedRegion can itself contain child Regions
            if (embeddedRegion.Regions != null && embeddedRegion.Regions.Count > 0)
                regionObj["nestedRegions"] = BuildNestedRegions(embeddedRegion);

            arr.Add(regionObj);
        }
        return arr;
    }
    // ── Serialize all CPs inside one EmbeddedRegion ───────────────────────────────
    private JArray BuildCPsFromRegion(EmbeddedRegion embeddedRegion)
    {
        var arr = new JArray();

        if (embeddedRegion.ComponentPresentations == null) return arr;

        foreach (CmComponentPresentation cp in embeddedRegion.ComponentPresentations)
        {
            if (cp == null) continue;
            try
            {
                arr.Add(BuildCPJson(cp));
            }
            catch (Exception ex)
            {
                _logger.Warning("CP build failed in region ["
                    + embeddedRegion.RegionName + "] component ["
                    + cp.Component?.Id + "]: " + ex.Message);
            }
        }
        return arr;
    }
    // ── Nested regions (EmbeddedRegion.Regions is also IList<EmbeddedRegion>) ─────
    private JArray BuildNestedRegions(EmbeddedRegion parentRegion)
    {
        var arr = new JArray();
        if (parentRegion.Regions == null) return arr;

        foreach (EmbeddedRegion nested in parentRegion.Regions)
        {
            if (nested == null) continue;
            arr.Add(new JObject
            {
                ["__typename"] = "NestedRegion",
                ["name"] = nested.RegionName ?? "",
                ["componentPresentations"] = BuildCPsFromRegion(nested)
            });
        }
        return arr;
    }

    // ── Component presentations ───────────────────────────────────────────────
    private JArray BuildComponentPresentations(Page page, string regionName)
    {
        var arr = new JArray();
        if (page.ComponentPresentations == null) return arr;

        foreach (CmComponentPresentation cp in page.ComponentPresentations)
        {
            if (cp?.ComponentTemplate == null) continue;
            string ctRegion = ResolveRegionNameFromCT(cp.ComponentTemplate) ?? "Main";
            if (ctRegion != regionName) continue;

            try
            {
                arr.Add(BuildCPJson(cp));
            }
            catch (Exception ex)
            {
                _logger.Warning("CP build failed for component ["
                    + cp.Component?.Id + "]: " + ex.Message);
            }
        }
        return arr;
    }

    // ── BuildCPJson — unchanged from before ───────────────────────────────────────
    // Each CmComponentPresentation has:
    //   .Component        → Component object
    //   .ComponentTemplate → ComponentTemplate object
    //   .Conditions        → IList (zero in your screenshot)
    private JObject BuildCPJson(CmComponentPresentation cp)
    {
        Component component = cp.Component;
        var fields = new JObject();

        if (component?.Content != null && component.Schema != null)
        {
            try
            {
                foreach (ItemField f in new ItemFields(component.Content, component.Schema))
                {
                    if (f != null) fields[f.Name] = GetFieldValue(f);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("Content read failed ["
                    + SafeStr(component?.Id) + "]: " + ex.Message);
            }
        }

        return new JObject
        {
            ["__typename"] = "ComponentPresentation",
            ["component"] = new JObject
            {
                ["id"] = SafeStr(component?.Id),
                ["title"] = component?.Title ?? "",
                ["schema"] = component?.Schema?.Title ?? "",
                ["schemaId"] = SafeStr(component?.Schema?.Id),
                ["schemaRootElementName"] = SafeStr(component?.Schema?.RootElementName),
                ["fields"] = fields,
                ["metadata"] = BuildComponentMetadata(component)
            },
            ["componentTemplate"] = BuildComponentTemplateJson(cp.ComponentTemplate)
        };
    }

    private JObject BuildComponentMetadata(Component component)
    {
        var obj = new JObject();
        if (component?.Metadata == null || component.MetadataSchema == null) return obj;
        try
        {
            foreach (ItemField f in new ItemFields(component.Metadata, component.MetadataSchema))
            {
                if (f != null) obj[f.Name] = GetFieldValue(f);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Component metadata read failed: " + ex.Message);
        }
        return obj;
    }

    // ── Component Template ────────────────────────────────────────────────────
    private JObject BuildComponentTemplateJson(ComponentTemplate ct)
    {
        if (ct == null) return new JObject();

        JObject ctMeta = ReadCTMetadataFields(ct);
        string regionName = ctMeta?[FIELD_REGION_NAME]?.ToString() ?? "";
        string entityView = ctMeta?[FIELD_ENTITY_VIEW]?.ToString() ?? "";
        string schemaRoot = ctMeta?[FIELD_SCHEMA_ROOT_ELEM]?.ToString() ?? "";

        return new JObject
        {
            ["id"] = SafeStr(ct.Id),
            ["title"] = ct.Title ?? "",
            ["outputFormat"] = ct.TemplateType.ToString(),
            ["priority"] = ct.Priority.ToString(),
            ["isRepositoryPublishable"] = ct.IsRepositoryPublishable,
            ["parametersSchemaId"] = ct.ParameterSchema?.Id.ToString() ?? "",
            ["metadataSchemaId"] = ct.MetadataSchema?.Id.ToString() ?? "",
            ["metadataSchemaTitle"] = ct.MetadataSchema?.Title ?? "",
            ["ctMetadata"] = ctMeta,
            ["regionName"] = regionName,
            ["entityViewName"] = entityView,
            ["schemaRootElement"] = schemaRoot
        };
    }

    private JObject ReadCTMetadataFields(ComponentTemplate ct)
    {
        var obj = new JObject();
        if (ct?.Metadata == null || ct.MetadataSchema == null) return obj;
        try
        {
            foreach (ItemField f in new ItemFields(ct.Metadata, ct.MetadataSchema))
            {
                if (f != null) obj[f.Name] = GetFieldValue(f);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("CT metadata read failed [" + ct.Id + "]: " + ex.Message);
        }
        return obj;
    }

    private string ResolveRegionNameFromCT(ComponentTemplate ct)
    {
        if (ct?.Metadata == null || ct.MetadataSchema == null) return null;
        try
        {
            foreach (ItemField f in new ItemFields(ct.Metadata, ct.MetadataSchema))
            {
                if (f?.Name != FIELD_REGION_NAME) continue;
                var tf = f as TextField;
                return string.IsNullOrWhiteSpace(tf?.Value) ? null : tf.Value;
            }
        }
        catch { /* fall through to null */ }
        return null;
    }

    // ── Field value extractor ─────────────────────────────────────────────────
    private JToken GetFieldValue(ItemField field)
    {
        if (field == null) return JValue.CreateNull();

        string typeName = field.GetType().Name;
        try
        {
            switch (typeName)
            {
                case "SingleLineTextField":
                case "MultiLineTextField":
                case "XhtmlField":
                case "TextField":
                    return ((TextField)field).Value ?? "";

                case "NumberField":
                    return ((NumberField)field).Value;

                case "DateField":
                    return ((DateField)field).Value.ToString("o");

                case "KeywordField":
                    {
                        var kw = ((KeywordField)field).Value;
                        return kw == null
                            ? (JToken)JValue.CreateNull()
                            : new JObject
                            {
                                ["id"] = kw.Id.ToString(),
                                ["title"] = kw.Title ?? "",
                                ["key"] = kw.Key ?? ""
                            };
                    }

                case "ComponentLinkField":
                    {
                        var arr = new JArray();
                        foreach (Component c in ((ComponentLinkField)field).Values)
                        {
                            if (c == null) continue;
                            arr.Add(new JObject
                            {
                                ["id"] = SafeStr(c.Id),
                                ["title"] = c.Title ?? ""
                            });
                        }
                        return arr;
                    }

                case "MultimediaLinkField":
                    {
                        var mm = ((MultimediaLinkField)field).Value;
                        if (mm == null) return JValue.CreateNull();
                        return new JObject
                        {
                            ["id"] = SafeStr(mm.Id),
                            ["title"] = mm.Title ?? "",
                            ["mimeType"] = mm.BinaryContent?.MultimediaType?.MimeType ?? "",
                            ["fileName"] = mm.BinaryContent?.Filename ?? ""
                        };
                    }

                case "EmbeddedSchemaField":
                    {
                        var emb = new JArray();
                        foreach (ItemFields fs in ((EmbeddedSchemaField)field).Values)
                        {
                            if (fs == null) continue;
                            var obj = new JObject();
                            foreach (ItemField f in fs)
                            {
                                if (f != null) obj[f.Name] = GetFieldValue(f);
                            }
                            emb.Add(obj);
                        }
                        return emb;
                    }

                default:
                    return field.ToString() ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("GetFieldValue failed for field ["
                + field.Name + "] type [" + typeName + "]: " + ex.Message);
            return "";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string SafeStr(object o) =>
        o == null ? "" : o.ToString();
}