using log4net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using System;
using System.Linq;
using System.Xml;

namespace TFSProjectMigration.ProjectStructure
{
    public class AreasAndIterationReader
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof (TFSWorkItemMigrationUI));
        /* Return Areas and Iterations of the project */
        public XmlNode[] PopulateIterations()
        {
            ICommonStructureService css = (ICommonStructureService)tfs.GetService(typeof (ICommonStructureService));
            //Gets Area/Iteration base Project
            ProjectInfo projectInfo = css.GetProjectFromName(projectName);
            NodeInfo[] nodes = css.ListStructures(projectInfo.Uri);
            XmlElement areaTree = css.GetNodesXml(new[]{nodes.Single(n => n.StructureType == "ProjectModelHierarchy").Uri}, true);
            XmlElement iterationsTree = css.GetNodesXml(new[]{nodes.Single(n => n.StructureType == "ProjectLifecycle").Uri}, true);
            XmlNode areaNodes = areaTree.ChildNodes[0];
            XmlNode iterationsNodes = iterationsTree.ChildNodes[0];
            return new XmlNode[]{areaNodes, iterationsNodes};
        }

        public AreasAndIterationReader(TfsTeamProjectCollection tfs, string projectName)
        {
            this.tfs = tfs;
            this.projectName = projectName;
        }

        TfsTeamProjectCollection tfs;
        String projectName;
    }
}