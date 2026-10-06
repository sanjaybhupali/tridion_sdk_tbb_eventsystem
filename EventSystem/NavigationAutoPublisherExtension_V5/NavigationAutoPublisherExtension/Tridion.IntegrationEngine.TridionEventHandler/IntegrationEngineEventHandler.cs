using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Tridion.ContentManager;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.Extensibility;
using Tridion.ContentManager.Extensibility.Events;
using Tridion.ContentManager.Publishing;
using Tridion.Logging;

namespace Tridion.IntegrationEngine.TridionEventHandler
{
    [TcmExtension("NavigationAutoPublisher-Extension")]
    public class IntegrationEngineEventHandler : TcmExtension
    {
      //  private const string LogFile = @"C:\sdkcode\EventSystem\navpublish.log";
        private const string navPath = "/Home/_Navigation.tpg";
        public IntegrationEngineEventHandler()
        {
            EventSystem.Subscribe<PublishTransaction, SaveEventArgs>(
                OnPublishTransactionSave, EventPhases.TransactionCommitted);
            // Subscribe to the Page Publish transaction committed event (Success)
            // EventSystem.Subscribe<Page, PublishEventArgs>(OnPagePublishCommitted, EventPhases.TransactionCommitted);

        }
        private void OnPublishTransactionSave(PublishTransaction publishTransaction, SaveEventArgs args, EventPhases phase)
        {
         //   System.Diagnostics.Debugger.Launch();

            try
            {
                if (publishTransaction.State != PublishTransactionState.Success)
                {
                    return;
                }

                foreach (var context in publishTransaction.PublishContexts)
                {
                    PublicationTarget publicationTarget = context.PublicationTarget;

                    foreach (var processedItem in context.ProcessedItems)
                    {
                        if (!processedItem.HasRenderFailure
                            && processedItem.ResolvedItem.Item is Page page)
                        {
                            Logger.Write(
                                $"New page '{page.Title}' ({page.Id}) was published. Triggering navigation.json republish.",
                                "NavigationPublishEvents", LoggingCategory.General);


                    //        var message = $"{DateTime.Now}: Paageid={page.Title}{Environment.NewLine}";
                     //       System.IO.File.AppendAllText(LogFile, message);
                     //       Logger.Write(message, "NavigationAutoPublisher-Extension", LoggingCategory.Logging);



                            if (!page.WebDavUrl.Contains(navPath))
                            {
                                TcmUri pageId = page.Id;
                                TcmUri targetId = publicationTarget.Id;
                                // On the original event thread - just read the identity, don't keep the session itself
                                string impersonatedUser = processedItem.Session.User.Title; // or whatever the correct member is - verify via IntelliSense

                                Logger.Write(
                               $"Going to RepublishNavigationJsonAsync  '{page.Title}' ({page.Id}) was published. Triggering navigation.json republish.",
                               "NavigationPublishEvents", LoggingCategory.General);


                                // Use a fresh Session created on the background thread itself -
                                // do NOT reuse processedItem.Session or page.Session here.
                                Task.Run(() => RepublishNavigationJsonAsync(pageId, targetId, impersonatedUser));
                            }

                          
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(
                    $"Error in IntegrationEngineEventHandler.OnPublishTransactionSave: {ex}",
                    "NavigationPublishEvents", LoggingCategory.General);
            }
        }



        private static void RepublishNavigationJsonAsync(TcmUri pageId, TcmUri targetId, string userName)
        {
            try
            {
                using (Session session = new Session(userName))
                {
                    Page page = (Page)session.GetObject(pageId);
                    Publication publication = (Publication)page.ContextRepository;

                    TcmUri navPageUri = ResolveNavigationJsonPageUri(publication);
                    if (navPageUri == null)
                    {

                        Logger.Write($"navigation.json page not found for publication {publication.Id}",
                            "NavigationAutoPublisher-Extension", LoggingCategory.General);
                        return;
                    }
                 //  System.Diagnostics.Debugger.Launch();
                    Page navigationPage = (Page)session.GetObject(navPageUri);
                    PublicationTarget target = (PublicationTarget)session.GetObject(targetId);

                    var instruction = new PublishInstruction(session)
                    {
                        RenderInstruction = new RenderInstruction(session),
                        
                        ResolveInstruction = new ResolveInstruction(session) { Purpose = ResolvePurpose.Publish }
                    };

                   

                    var transactions = PublishEngine.Publish(
                        new IdentifiableObject[] { navigationPage },
                        instruction,
                        new List<PublicationTarget> { target },
                        PublishPriority.High
                        
                    );

                    System.Threading.Thread.Sleep(4000); // let it actually process before checking state
                   
                    foreach (var t in transactions)
                    {
                        // Re-fetch the transaction to get its current, post-processing state,
                        // not just the state at the moment Publish() returned.

                        var refreshed = (PublishTransaction)session.GetObject(t.Id);

                      //  var message = $"{DateTime.Now}: Transaction Id={refreshed.Id}, State={refreshed.State}, Target={refreshed.TargetType?.Title}{Environment.NewLine}";
                     //   System.IO.File.AppendAllText(LogFile, message);
                    //    Logger.Write(message, "NavigationAutoPublisher-Extension", LoggingCategory.Logging);



                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex.Message, "NavigationAutoPublisher-Extension", LoggingCategory.Logging);
              
                //System.IO.File.AppendAllText(LogFile,
                  //  $"{DateTime.Now}: RepublishNavigationJsonAsync failed: {ex}{Environment.NewLine}");

                
            }
        }


        /// <summary>
        /// Determine if the page is "new" - i.e. this publish represents its first
        /// appearance on the site. Approach: check revision/version number,
        /// or check whether it has prior publish info.
        /// </summary>
        private static bool IsNewlyCreatedPage(Page page)
        {
            return page.Version == 1;
        }



        private static TcmUri ResolveNavigationJsonPageUri(Publication publication)
        {
          //  string navPath = "/Home/_Navigation.tpg";

            try
            {
                Session session = publication.Session;
                string webDavUrl = publication.WebDavUrl + navPath;
                var navPage = session.GetObject(webDavUrl) as Page;
                return navPage?.Id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Log(string message, TraceEventType severity = TraceEventType.Information)
        {
            Logger.Write(message, "Integration Engine Event Handler", LoggingCategory.General, severity);
        }
    }
}