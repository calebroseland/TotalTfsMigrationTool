using log4net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TFSProjectMigration.Conversion.TeamQueries;
using TFSProjectMigration.ProjectStructure;
using TFSProjectMigration.Conversion.WorkItems;
using TFSProjectMigration.Conversion.Users;

namespace TFSProjectMigration.Conversion
{
    class MigrateProject
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MigrateProject));

        public Action<string> Log = (s) => { };
        private TfsProject sourceTFS;
        private TfsProject targetTFS;



        public void StartMigration(bool isNotIncludeClosed, bool isNotIncludeRemoved, WorkItemTypeMap witMap)
        {
            var workItemRead = new WorkItemRead(sourceTFS.collection, sourceTFS.project);
            var workItemWrite = new WorkItemWrite(targetTFS.collection, targetTFS.project);

            var areasAndIterationsReader = new AreasAndIterationReader(sourceTFS.collection, sourceTFS.project.Name);
            var areasAndIterationsWriter = new AreasAndIterationsWriter(targetTFS.collection, targetTFS.project.Name);


            var teamQueryReader = new TeamQueryReader(sourceTFS.collection, sourceTFS.project);
            var teamQueryWriter = new TeamQueryWriter(targetTFS.collection, targetTFS.project);

            
            WorkItemIdMap wiMap = new WorkItemIdMap();
            logger.InfoFormat("--------------------------------Migration from '{0}' to '{1}' Start----------------------------------------------", sourceTFS.project.Name, targetTFS.project.Name);


            WorkItemCollection source = workItemRead.GetWorkItems(sourceTFS.project.Name, isNotIncludeClosed, isNotIncludeRemoved); //Get Workitems from source tfs 
            XmlNode[] iterations = areasAndIterationsReader.PopulateIterations(); //Get Iterations and Areas from source tfs 

            Log("Mapping users...");
            var userMigration = new UserMigration(sourceTFS, targetTFS);
            userMigration.MapUserIds();


            Log("Generating Areas...");
            var areaIdMap = areasAndIterationsWriter.GenerateAreas(iterations[0], sourceTFS.project.Name); //Copy Areas

            Log("\nGenerating Iterations...");
            var iterationsIdMap = areasAndIterationsWriter.GenerateIterations(iterations[1], sourceTFS.project.Name); //Copy Iterations

            Log("\nCopying Team Queries...");
            teamQueryWriter.SetTeamQueries(teamQueryReader.queryCol, sourceTFS.project.Name); //Copy Queries

            Log("\nCopying Work Items...");
            var sourceStore = (WorkItemStore)sourceTFS.collection.GetService(typeof(WorkItemStore));
            workItemWrite.writeWorkItems(sourceStore, source, sourceTFS.project.Name, witMap); //Copy Workitems

            Log("\nCopying Test Plans...");

            TestPlanMigration tcm = new TestPlanMigration(sourceTFS.collection, targetTFS.collection, sourceTFS.project.Name, targetTFS.project.Name, workItemWrite.WorkItemIdMap);
            tcm.CopyTestPlans(); //Copy Test Plans

            Log("Project Migrated");
            logger.Info("--------------------------------Migration END----------------------------------------------");
        }

      public MigrateProject(TfsProject sourceTFS, TfsProject targetTFS)
      {
         this.sourceTFS = sourceTFS;
         this.targetTFS = targetTFS;                 
      }
   }
}
