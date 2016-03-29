using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TFSProjectMigration.Conversion.WorkItems
{
   public class WorkItemIdMap
   {
      public Dictionary<int, int> mapping = new Dictionary<int, int>();

      public int this[int sourceId]
      {
         get
         {
            int targetId;
            if (mapping.TryGetValue(sourceId, out targetId))
               return targetId;

            return 0;
         }
      }
      public void Map(WorkItem source, WorkItem target)
      {
         mapping[source.Id] = target.Id;
      }

      internal bool Contains(int id)
      {
         return mapping.ContainsKey(id);
      }

      internal void Map(int id1, int id2)
      {
         mapping[id1] = id2;
      }

      public void Save(string name)
      {

      }

      public WorkItemIdMap Read(string name)
      {
         return new WorkItemIdMap();
      }
   }
}
