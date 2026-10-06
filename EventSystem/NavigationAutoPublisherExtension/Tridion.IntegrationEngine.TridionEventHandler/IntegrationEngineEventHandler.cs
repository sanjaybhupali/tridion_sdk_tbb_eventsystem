using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tridion.ContentManager;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.Extensibility;
using Tridion.ContentManager.Extensibility.Events;
using Tridion.ContentManager.Publishing;
using Tridion.Logging;

namespace Tridion.IntegrationEngine.TridionEventHandler
{
    [TcmExtension("NavigationAutoPublishEvents")]
    public class IntegrationEngineEventHandler : TcmExtension
    {
        private const string NavPath = "/Home/_Navigation.tpg";
        //   private const string LogFile = @"C:\sdkcode\EventSystem\navpublish.log";
        public IntegrationEngineEventHandler()
        {
            // Event System subscription for publish transaction status updates
            EventSystem.Subscribe<PublishTransaction, SaveEventArgs>(
                OnPublishTransactionSave, EventPhases.TransactionCommitted);
            

        }

        private void OnPublishTransactionSave(PublishTransaction publishTransaction, SaveEventArgs args, EventPhases phase)
        {
           // System.Diagnostics.Debugger.Launch();
            try
            {
                if (publishTransaction.State != PublishTransactionState.Success)
                {
                    return;
                }

                // TODO: also verify the transaction is a Publish (not Unpublish) via its Action member.

                var queued = new HashSet<string>(); // one navigation publish per publication+target
                string userName = null;

                foreach (var context in publishTransaction.PublishContexts)
                {
                    PublicationTarget publicationTarget = context.PublicationTarget;

                    foreach (var processedItem in context.ProcessedItems)
                    {
                        if (processedItem.HasRenderFailure
                            || !(processedItem.ResolvedItem.Item is Page page))
                        {
                            continue;
                        }

                        // Loop prevention: ignore the navigation page itself.
                        if (page.WebDavUrl.EndsWith(NavPath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Only new pages (heuristic; remove if you want every publish to trigger it).
                        //if (page.Version != 1)
                        //{
                        //    continue;
                        //}

                        if (publicationTarget == null)
                        {
                            Logger.Write($"No PublicationTarget for page {page.Id}; navigation republish skipped.",
                                "NavigationAutoPublishEvents", LoggingCategory.General);
                            continue;
                        }

                        TcmUri pageId = page.Id;
                        TcmUri targetId = publicationTarget.Id;

                        string key = page.ContextRepository.Id + "|" + targetId;

                        //  var message = $"{DateTime.Now}: page-Id={page.Id}, Title={page.Title}, Version={page.Version},GetpublishUrl= {page.GetPublishUrl(targetId).ToString()},{Environment.NewLine}";
                        //   System.IO.File.AppendAllText(LogFile, message);

                        if (!queued.Add(key))
                        {
                            continue; // already queued for this publication+target
                        }

                        userName = userName ?? processedItem.Session.User.Title;
                        string user = userName;

                        var message = $"{DateTime.Now}: page-Id={page.Id}, page-Title={page.Title}, page-Version={page.Version},{Environment.NewLine}";

                        Logger.Write(message, "NavigationAutoPublishEvents", LoggingCategory.General);

                        Task.Run(() => RepublishNavigationJsonAsync(pageId, targetId, user));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error in OnPublishTransactionSave: {ex}",
                    "NavigationAutoPublishEvents", LoggingCategory.General);
            }
        }

        private static void RepublishNavigationJsonAsync(TcmUri pageId, TcmUri targetId, string userName)
        {
            //  System.Diagnostics.Debugger.Break();
            //  System.Diagnostics.Debugger.Launch();
            try
            {
                using (Session session = new Session(userName))
                {
                    Page page = (Page)session.GetObject(pageId);
                    Publication publication = (Publication)page.ContextRepository;

                    TcmUri navPageUri = ResolveNavigationJsonPageUri(publication);
                    if (navPageUri == null)
                    {
                        Logger.Write($"Navigation page '{NavPath}' not found for Publication {publication.Id}",
                            "NavigationAutoPublishEvents", LoggingCategory.General);
                        return;
                    }

                    Page navigationPage = (Page)session.GetObject(navPageUri);

                    var instruction = new PublishInstruction(session)
                    {
                        RenderInstruction = new RenderInstruction(session),
                        ResolveInstruction = new ResolveInstruction(session) { Purpose = ResolvePurpose.Publish }
                    };

                    // Handle Classic Publishing targets vs. Topology Manager Target Types
                    if (targetId != null)
                    {
                       
                        PublicationTarget target = (PublicationTarget)session.GetObject(targetId);
                        Logger.Write("Before PublishEngine.Publish", "NavigationAutoPublishEvents", LoggingCategory.General);
                        PublishEngine.Publish(
                            new IdentifiableObject[] { navigationPage },
                            instruction,
                            new List<PublicationTarget> { target },
                            PublishPriority.Normal
                        );
                        //   var message = $"{DateTime.Now}: PAGE- Id={page.Id}, Title={page.Title}, Version={page.Version},GetpublishUrl= {page.GetPublishUrl(targetId).ToString()},{Environment.NewLine}";
                        //  System.IO.File.AppendAllText(LogFile, message);


                    }
                    //else
                    //{
                    //    PublishEngine.Publish(
                    //        new IdentifiableObject[] { navigationPage },
                    //        instruction,
                    //        publication.TargetTypes,
                    //        PublishPriority.Normal
                    //    );
                    //}

                    Logger.Write($"Successfully queued navigation page republish for Publication {publication.Id}",
                        "NavigationAutoPublishEvents", LoggingCategory.General);
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"RepublishNavigationJsonAsync failed: {ex}", "NavigationAutoPublishEvents", LoggingCategory.Logging);
            }
        }

        private static TcmUri ResolveNavigationJsonPageUri(Publication publication)
        {
            try
            {
                Session session = publication.Session;
                string webDavUrl = publication.WebDavUrl + NavPath;

                if (session.IsExistingObject(webDavUrl))
                {
                    var navPage = session.GetObject(webDavUrl) as Page;
                    return navPage?.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error resolving WebDAV URL '{NavPath}': {ex.Message}", "NavigationAutoPublishEvents", LoggingCategory.Logging);
            }

            return null;
        }
    }
}