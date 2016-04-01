using Microsoft.TeamFoundation.TestManagement.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
            using (var tw = File.OpenText(filename))
            {
                var serializer = Newtonsoft.Json.JsonSerializer.Create();
                mapping = serializer.Deserialize<Dictionary<int, int>>(new JsonTextReader(tw));
            }
        }
        
    }
}
