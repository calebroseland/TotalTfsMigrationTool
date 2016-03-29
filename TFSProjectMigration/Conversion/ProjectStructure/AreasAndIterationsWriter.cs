using log4net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;
using System;
using System.Xml;
using TFSProjectMigration.Conversion.ProjectStructure;

namespace TFSProjectMigration.ProjectStructure
{
   public class AreasAndIterationsWriter
   {
      private static readonly ILog logger = LogManager.GetLogger(typeof(AreasAndIterationsWriter));

      public IterationIdMap GenerateIterations(XmlNode tree, string sourceProjectName)
      {
         IterationIdMap m = new IterationIdMap();
         ICommonStructureService4 css = (ICommonStructureService4)tfs.GetService(typeof(ICommonStructureService4));
         string rootNodePath = string.Format("\\{0}\\Iteration", projectName);
         var pathRoot = css.GetNodeFromPath(rootNodePath);

         if (tree.FirstChild != null)
         {
            var firstChild = tree.FirstChild;
            CreateIterationNodes(firstChild, css, pathRoot);
         }
         RefreshCache();

         return m;
      }


      private void RefreshCache()
      {
         ICommonStructureService css = tfs.GetService<ICommonStructureService>();
         WorkItemServer server = tfs.GetService<WorkItemServer>();
         server.SyncExternalStructures(WorkItemServer.NewRequestId(), css.GetProjectFromName(projectName).Uri);
         store.RefreshCache();
      }

      private static void CreateIterationNodes(XmlNode node, ICommonStructureService4 css, NodeInfo pathRoot)
      {
         int myNodeCount = node.ChildNodes.Count;
         for (int i = 0; i < myNodeCount; i++)
         {
            XmlNode childNode = node.ChildNodes[i];
            NodeInfo createdNode;
            var name = childNode.Attributes["Name"].Value;
            try
            {
               var uri = css.CreateNode(name, pathRoot.Uri);
               Console.WriteLine("NodeCreated:" + uri);
               createdNode = css.GetNode(uri);
            }
            catch (Exception)
            {
               //node already exists
               createdNode = css.GetNodeFromPath(pathRoot.Path + @"\" + name);
               //continue;
            }

            DateTime? startDateToUpdate = null;
            if (!createdNode.StartDate.HasValue)
            {
               var startDate = childNode.Attributes["StartDate"];
               DateTime startDateParsed;
               if (startDate != null && DateTime.TryParse(startDate.Value, out startDateParsed))
                  startDateToUpdate = startDateParsed;
            }
            DateTime? finishDateToUpdate = null;
            if (!createdNode.FinishDate.HasValue)
            {
               DateTime finishDateParsed;
               var finishDate = childNode.Attributes["FinishDate"];
               if (finishDate != null && DateTime.TryParse(finishDate.Value, out finishDateParsed))
                  finishDateToUpdate = finishDateParsed;
            }
            if (startDateToUpdate.HasValue || finishDateToUpdate.HasValue)
               css.SetIterationDates(createdNode.Uri, startDateToUpdate, finishDateToUpdate);
            if (createdNode != null && node.HasChildNodes)
            {
               foreach (XmlNode subChildNode in childNode.ChildNodes)
               {
                  CreateIterationNodes(subChildNode, css, createdNode);
               }
            }
         }
      }

      public AreaIdMap GenerateAreas(XmlNode tree, string sourceProjectName)
      {
         AreaIdMap map = new AreaIdMap();

         ICommonStructureService css = (ICommonStructureService)tfs.GetService(typeof(ICommonStructureService));
         string rootNodePath = string.Format("\\{0}\\Area", projectName);
         var pathRoot = css.GetNodeFromPath(rootNodePath);

         if (tree.FirstChild != null)
         {
            int myNodeCount = tree.FirstChild.ChildNodes.Count;
            for (int i = 0; i < myNodeCount; i++)
            {
               XmlNode Node = tree.ChildNodes[0].ChildNodes[i];
               try
               {
                  css.CreateNode(Node.Attributes["Name"].Value, pathRoot.Uri);
               }
               catch (Exception)
               {
                  //node already exists
                  continue;
               }
               if (Node.FirstChild != null)
               {
                  string nodePath = rootNodePath + "\\" + Node.Attributes["Name"].Value;
                  GenerateSubAreas(Node, nodePath, css);
               }
            }
         }

         RefreshCache();

         return map;
      }

      private void GenerateSubAreas(XmlNode tree, string nodePath, ICommonStructureService css)
      {
         var path = css.GetNodeFromPath(nodePath);
         int nodeCount = tree.FirstChild.ChildNodes.Count;
         for (int i = 0; i < nodeCount; i++)
         {
            XmlNode node = tree.ChildNodes[0].ChildNodes[i];
            try
            {
               css.CreateNode(node.Attributes["Name"].Value, path.Uri);
            }
            catch (Exception ex)
            {
               //node already exists
               continue;
            }
            if (node.FirstChild != null)
            {
               string newPath = nodePath + "\\" + node.Attributes["Name"].Value;
               GenerateSubAreas(node, newPath, css);
            }
         }
      }

      public AreasAndIterationsWriter(TfsTeamProjectCollection tfs, string projectName)
      {
         this.tfs = tfs;
         this.projectName = projectName;
         store = (WorkItemStore)tfs.GetService(typeof(WorkItemStore));
      }

      TfsTeamProjectCollection tfs;
      String projectName;
      private WorkItemStore store;
   }
}