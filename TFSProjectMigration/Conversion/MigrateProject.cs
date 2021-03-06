﻿using log4net;
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

        public WorkItemIdMap workItemIdMap;
        public WorkItemTypeMap FieldMap;


        public Action<string> Log = (s) => { };
        private TfsProject sourceTFS;
        private TfsProject targetTFS;
        internal UserMap Usermap;

        public void StartMigration(bool isNotIncludeClosed, bool isNotIncludeRemoved, bool isIncludeHistoryComment, bool isIncludeHistoryLink, bool shouldFixMultilineFields)
        {
            if (workItemIdMap == null)
                throw new InvalidFieldValueException(nameof(workItemIdMap) + " is not set");


            if (FieldMap == null)
                throw new InvalidFieldValueException(nameof(FieldMap) + " is not set");


            logger.InfoFormat("--------------------------------Migration from '{0}' to '{1}' Start----------------------------------------------", sourceTFS.project.Name, targetTFS.project.Name);

            Log("Generating Areas & Iterations...");
            SetupAreasAndIterations();


            Log("Copying Team Queries...");
            CopyTeamQueries();

            Log("Copying Work Items...");
            WorkItemMigration mig = new WorkItemMigration(sourceTFS, targetTFS);
            mig.WorkitemTemplateMap = FieldMap;
            mig.UsersMap = Usermap;
            mig.WorkItemIdMap = workItemIdMap;
            mig.CopyWorkItems(isNotIncludeClosed, isNotIncludeRemoved, isIncludeHistoryComment, isIncludeHistoryLink, shouldFixMultilineFields);

            Log("Copying Test Plans...");
            TestPlanMigration tcm = new TestPlanMigration(sourceTFS, targetTFS);
            tcm.UsersMap = Usermap;
            tcm.WorkItemIdMap = workItemIdMap;
            tcm.CopyTestPlans();
            
            Log("Project Migrated");
            logger.Info("--------------------------------Migration END----------------------------------------------");
        }

        private void CopyTeamQueries()
        {
            var teamQueryReader = new TeamQueryReader(sourceTFS.collection, sourceTFS.project);
            var teamQueryWriter = new TeamQueryWriter(targetTFS.collection, targetTFS.project);

            teamQueryWriter.SetTeamQueries(teamQueryReader.queryCol, sourceTFS.project.Name); //Copy Queries
        }

        private void SetupAreasAndIterations()
        {
            var areasAndIterationsReader = new AreasAndIterationReader(sourceTFS.collection, sourceTFS.project.Name);
            XmlNode[] iterations = areasAndIterationsReader.PopulateIterations(); //Get Iterations and Areas from source tfs 

            var areasAndIterationsWriter = new AreasAndIterationsWriter(targetTFS.collection, targetTFS.project.Name);
            var areaIdMap = areasAndIterationsWriter.GenerateAreas(iterations[0], sourceTFS.project.Name); //Copy Areas
            var iterationsIdMap = areasAndIterationsWriter.GenerateIterations(iterations[1], sourceTFS.project.Name); //Copy Iterations
        }


        public MigrateProject(TfsProject sourceTFS, TfsProject targetTFS)
      {
         this.sourceTFS = sourceTFS;
         this.targetTFS = targetTFS;                 
      }
   }
}
