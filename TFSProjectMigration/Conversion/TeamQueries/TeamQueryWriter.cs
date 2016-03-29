using log4net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSProjectMigration.Conversion.TeamQueries
{
   class TeamQueryWriter
   {      
      public void SetTeamQueries(QueryHierarchy sourceQueryCol, string sourceProjectName)
      {
         foreach (QueryFolder queryFolder in sourceQueryCol)
         {
            if (queryFolder.Name == "Team Queries" || queryFolder.Name == "Shared Queries")
            {
               QueryFolder teamQueriesFolder = (QueryFolder)project.QueryHierarchy["Shared Queries"];
               SetQueryItem(queryFolder, teamQueriesFolder, sourceProjectName);

               QueryFolder test = (QueryFolder)project.QueryHierarchy["Shared Queries"];
            }
         }
      }

      private void SetQueryItem(QueryFolder queryFolder, QueryFolder parentFolder, string sourceProjectName)
      {
         QueryItem newItem = null;
         foreach (QueryItem subQuery in queryFolder)
         {
            try
            {
               if (subQuery.GetType() == typeof(QueryFolder))
               {
                  newItem = new QueryFolder(subQuery.Name);
                  if (!parentFolder.Contains(subQuery.Name))
                  {
                     parentFolder.Add(newItem);
                     project.QueryHierarchy.Save();
                     SetQueryItem((QueryFolder)subQuery, (QueryFolder)newItem, sourceProjectName);
                  }
                  else
                  {
                     logger.WarnFormat("Query Folder {0} already exists", subQuery);
                  }

               }
               else
               {
                  QueryDefinition oldDef = (QueryDefinition)subQuery;
                  string queryText = oldDef.QueryText.Replace(sourceProjectName, project.Name).Replace("User Story", "Product Backlog Item").Replace("Issue", "Impediment");

                  newItem = new QueryDefinition(subQuery.Name, queryText);
                  if (!parentFolder.Contains(subQuery.Name))
                  {
                     parentFolder.Add(newItem);
                     project.QueryHierarchy.Save();
                  }
                  else
                  {
                     logger.WarnFormat("Query Definition {0} already exists", subQuery);
                  }
               }
            }
            catch (Exception ex)
            {
               if (newItem != null)
                  newItem.Delete();
               logger.ErrorFormat("Error creating Query: {0} : {1}", subQuery, ex.Message);
               continue;
            }
         }
      }

      public TeamQueryWriter(TfsTeamProjectCollection tfs, Project project)
      {
         this.tfs = tfs;
         this.project = project;
         store = (WorkItemStore)tfs.GetService(typeof(WorkItemStore));
      }

      TfsTeamProjectCollection tfs;
      Project project;
      private WorkItemStore store;

      private static readonly ILog logger = LogManager.GetLogger(typeof(TeamQueryWriter));
   }
}
