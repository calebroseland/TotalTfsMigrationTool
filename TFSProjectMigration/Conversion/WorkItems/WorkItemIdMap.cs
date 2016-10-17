using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace TFSProjectMigration.Conversion.WorkItems
{
    public class WorkItemIdMap
    {
        string filename;
        public WorkItemIdMap(string filename)
        {
            this.filename = filename;
            if (File.Exists(filename))
            {
                foreach (var line in File.ReadLines(filename))
                {                    
                    var fields = line.Split('|');
                    mapping[Convert.ToInt32(fields[0])] = Convert.ToInt32(fields[1]);
                }
            }
            else
            {
                using (File.Create(filename))
                {

                }
            }
        }

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
            Map(source.Id, target.Id);
        }

        internal bool Contains(int id)
        {
            return mapping.ContainsKey(id);
        }

        internal void Map(int id1, int id2)
        {
            mapping[id1] = id2;
            File.AppendAllText(filename, id1 + "|" + id2 + Environment.NewLine);
        }


        public void SaveToDisk(string filename)
        {
            using (var tw = File.CreateText(filename))
            {
                var serializer = Newtonsoft.Json.JsonSerializer.Create();
                serializer.Serialize(tw, mapping);
            }
        }
        
        public void ReadFromDisk(string filename)
        {
            this.filename = filename;            
            using (var tw = File.OpenText(filename))
            {
                var serializer = Newtonsoft.Json.JsonSerializer.Create();
                mapping = serializer.Deserialize<Dictionary<int, int>>(new JsonTextReader(tw));
            }
        }
        
    }
}
