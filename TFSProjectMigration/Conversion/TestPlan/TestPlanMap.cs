using Microsoft.TeamFoundation.TestManagement.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSProjectMigration.Conversion.TestPlan
{
   class TestPlanIdMap
   {
      public Dictionary<int, int> mapping = new Dictionary<int, int>();

      public void Map(ITestPlan source, ITestPlan target)
      {
         this.mapping[source.Id] = target.Id;
      }
   }
}
