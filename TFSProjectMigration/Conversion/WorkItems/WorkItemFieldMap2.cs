using System.Collections.Generic;

namespace TFSProjectMigration.Conversion.WorkItems
{
    public class WorkItemFieldMap2
    {
        public Dictionary<string, string> MappedReferenceNames { get; set; }        

        public List<string> SourceFields { get; set; }

        public string SourceWorkIemType { get; set; }

        public List<string> TargetFields { get; set; }

        public string TargetWorkIemType { get; set; }
    }
}