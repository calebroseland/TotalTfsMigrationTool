using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TFSProjectMigration.Conversion.TeamQueries
{
   class TeamQueryReader
   {
      private Project sourceProject;
      private TfsTeamProjectCollection sourceTFS;

      public TeamQueryReader(TfsTeamProjectCollection sourceTFS, Project sourceProject)
      {
         this.sourceTFS = sourceTFS;
         this.sourceProject = sourceProject;
      }

      public QueryHierarchy queryCol => this.sourceProject.QueryHierarchy;
   }
}
