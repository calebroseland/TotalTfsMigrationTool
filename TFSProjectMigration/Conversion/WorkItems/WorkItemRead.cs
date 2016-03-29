using System;
using System.Linq;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Xml;
using System.IO;
using log4net;

namespace TFSProjectMigration
{
    public class WorkItemRead
    {
        TfsTeamProjectCollection targetProjectCollection;
        WorkItemStore targetWorkItemStore;
        Project sourceProject;

        private static readonly ILog logger = LogManager.GetLogger(typeof(TFSWorkItemMigrationUI));

        public WorkItemTypeCollection workItemTypes => targetWorkItemStore.Projects[sourceProject.Name].WorkItemTypes;
       

        public WorkItemRead(TfsTeamProjectCollection tfs, Project sourceProject)
        {
            this.targetProjectCollection = tfs;
            this.sourceProject = sourceProject;
            targetWorkItemStore = (WorkItemStore)tfs.GetService(typeof(WorkItemStore));
        }


        /* Get required work items from project and save existing attachments of workitems to local folder */
        public WorkItemCollection GetAllWorkItems(string project)
        {
            WorkItemCollection workItemCollection = targetWorkItemStore.Query(" SELECT * " +
                                                                 " FROM WorkItems " +
                                                                 " WHERE [System.TeamProject] = '" + project +
                                                                 "' AND [System.State] <> 'Closed' ORDER BY [System.Id]");
            DownloadAttachments(workItemCollection);
            return workItemCollection;
        }


        public WorkItemCollection GetWorkItems(string project, bool IsNotIncludeClosed, bool IsNotIncludeRemoved)
        {
            String query = "";
            if (IsNotIncludeClosed && IsNotIncludeRemoved)
            {
                query = String.Format(" SELECT * " +
                                                    " FROM WorkItems " +
                                                    " WHERE [System.TeamProject] = '" + project +
                                                    "' AND [System.State] <> 'Closed' AND [System.State] <> 'Removed' ORDER BY [System.Id]");
            }

            else if (IsNotIncludeRemoved)
            {
                query = String.Format(" SELECT * " +
                                                   " FROM WorkItems " +
                                                   " WHERE [System.TeamProject] = '" + project +
                                                   "' AND [System.State] <> 'Removed' ORDER BY [System.Id]");
            }
            else if (IsNotIncludeClosed)
            {
                query = String.Format(" SELECT * " +
                                                   " FROM WorkItems " +
                                                   " WHERE [System.TeamProject] = '" + project +
                                                   "' AND [System.State] <> 'Closed'  ORDER BY [System.Id]");
            }
            else
            {
                query = String.Format(" SELECT * " +
                                                   " FROM WorkItems " +
                                                   " WHERE [System.TeamProject] = '" + project +
                                                   "' ORDER BY [System.Id]");
            }

            System.Diagnostics.Debug.WriteLine(query);

            WorkItemCollection workItemCollection = targetWorkItemStore.Query(query);
            DownloadAttachments(workItemCollection);
            return workItemCollection;
        }

        /* Save existing attachments of workitems to local folders of workitem ID */
        private void DownloadAttachments(WorkItemCollection workItemCollection)
        {
            if (!Directory.Exists(@"Attachments"))
            {
                Directory.CreateDirectory(@"Attachments");
            }
            else
            {
                EmptyFolder(new DirectoryInfo(@"Attachments"));
            }

            System.Net.WebClient webClient = new System.Net.WebClient();
            webClient.UseDefaultCredentials = true;

            foreach (WorkItem wi in workItemCollection)
            {
                if (wi.AttachedFileCount > 0)
                {
                    foreach (Attachment att in wi.Attachments)
                    {
                        try
                        {
                            String path = @"Attachments\" + wi.Id;
                            bool folderExists = Directory.Exists(path);
                            if (!folderExists)
                            {
                                Directory.CreateDirectory(path);
                            }
                            if (!File.Exists(path + "\\" + att.Name))
                            {
                                webClient.DownloadFile(att.Uri, path + "\\" + att.Name);
                            }
                            else 
                            {
                                webClient.DownloadFile(att.Uri, path + "\\" + att.Id + "_" + att.Name);
                            }
                           
                        }
                        catch (Exception)
                        {
                            logger.Info("Error downloading attachment for work item : " + wi.Id + " Type: " + wi.Type.Name);
                        }

                    }
                }
            }
        }


        /*Delete all subfolders and files in given folder*/
        private void EmptyFolder(DirectoryInfo directoryInfo)
        {

            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo subfolder in directoryInfo.GetDirectories())
            {
                EmptyFolder(subfolder);
                subfolder.Delete();
            }

        }           
    }
}
