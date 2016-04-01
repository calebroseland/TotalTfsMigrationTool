using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UpdateSprintAndReleaseDates
{
    class Iterations
    {
        public Dictionary<string, string> PopulateIterations()
        {
            ICommonStructureService css = (ICommonStructureService)tfs.GetService(typeof(ICommonStructureService));
            //Gets Area/Iteration base Project
            ProjectInfo projectInfo = css.GetProjectFromName(projectName);
            NodeInfo[] nodes = css.ListStructures(projectInfo.Uri);
            XmlElement areaTree = css.GetNodesXml(new[] { nodes.Single(n => n.StructureType == "ProjectModelHierarchy").Uri }, true);
            XmlElement iterationsTree = css.GetNodesXml(new[] { nodes.Single(n => n.StructureType == "ProjectLifecycle").Uri }, true);
            XmlNode areaNodes = areaTree.ChildNodes[0];
            Queue<XmlNode> nodesToDo = new Queue<XmlNode>();
            nodesToDo.Enqueue(iterationsTree.ChildNodes[0]);

            var map = new Dictionary<string, string>();
            while (nodesToDo.Any())
            {
                var current = nodesToDo.Dequeue();

                var path = current.Attributes["Path"].Value;
                var nodeId = current.Attributes["NodeID"].Value;

                map.Add(path, nodeId);
                foreach (XmlNode item in current.ChildNodes)
                {
                    foreach (XmlNode child in ((XmlNode)item).ChildNodes)
                    {
                        nodesToDo.Enqueue(child);
                    }                    
                }
            }

            return map;
        }

        public IEnumerable<WorkItem> GetSprints()
        {
            var query = string.Format(" SELECT * " +
                                   " FROM WorkItems " +
                                   " WHERE [System.TeamProject] = '" + projectName +
                                   "'  ORDER BY [System.Id]");
        
            
            WorkItemCollection workItemCollection = tfs.GetService<WorkItemStore>().Query(query);
            return workItemCollection.Cast<WorkItem>().Where(wi => wi.Type.Name.Contains("Sprint"));
        }


        public IEnumerable<WorkItem> GetReleases()
        {
            var query = string.Format(" SELECT * " +
                                   " FROM WorkItems " +
                                   " WHERE [System.TeamProject] = '" + projectName +
                                   "'  ORDER BY [System.Id]");


            WorkItemCollection workItemCollection = tfs.GetService<WorkItemStore>().Query(query);
            return workItemCollection.Cast<WorkItem>().Where(wi => wi.Type.Name.Contains("Release"));
        }

        internal void UpdateIteration(string iterationPath, DateTime dateTime1, DateTime dateTime2)
        {
            ICommonStructureService4 css = tfs.GetService<ICommonStructureService4>();            
            css.SetIterationDates(iterationPath, dateTime1, dateTime2);
        }

        public Iterations(TfsTeamProjectCollection tfs, string projectName)
        {
            this.tfs = tfs;
            this.projectName = projectName;
        }

        TfsTeamProjectCollection tfs;
        String projectName;

        
        public void UpdateIterations(XmlNode tree, string sourceProjectName)
        {            
            ICommonStructureService4 css = (ICommonStructureService4)tfs.GetService(typeof(ICommonStructureService4));
            string rootNodePath = string.Format("\\{0}\\Iteration", projectName);
            var pathRoot = css.GetNodeFromPath(rootNodePath);

            if (tree.FirstChild != null)
            {
                var firstChild = tree.FirstChild;
                CreateIterationNodes(firstChild, css, pathRoot);
            }

            RefreshCache();
            
        }


        private void RefreshCache()
        {
            ICommonStructureService css = tfs.GetService<ICommonStructureService>();
            WorkItemServer server = tfs.GetService<WorkItemServer>();
            server.SyncExternalStructures(WorkItemServer.NewRequestId(), css.GetProjectFromName(projectName).Uri);
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
    }
}
