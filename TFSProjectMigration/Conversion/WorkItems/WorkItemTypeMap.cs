using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TFSProjectMigration.Conversion.WorkItems
{
    public class WorkItemTypeMap
    {
        public WorkItemTypeCollection GetCollection(TfsProject source)
        {
            return source.project.WorkItemTypes;
        }

        public Dictionary<WorkItemType, WorkItemType> mapping = new Dictionary<WorkItemType, WorkItemType>();
        public List<WorkItemFieldMap> fieldMapping = new List<WorkItemFieldMap>();
        public static WorkItemTypeMap MapTypes(TfsProject source, TfsProject target)
        {
            WorkItemTypeCollection workItemTypes = target.project.WorkItemTypes;
            WorkItemTypeMap fieldMap = new WorkItemTypeMap();
            foreach (WorkItemType workItemTypeSource in source.project.WorkItemTypes)
            {
                List<List<string>> fieldList = new List<List<string>>();
                List<string> sourceList = new List<string>();
                List<string> targetList = new List<string>();
                WorkItemType workItemTypeTarget = null;
                if (workItemTypes.Contains(workItemTypeSource.Name))
                {
                    workItemTypeTarget = workItemTypes[workItemTypeSource.Name];
                }
                else if (workItemTypeSource.Name == "User Story")
                {
                    workItemTypeTarget = workItemTypes["Product Backlog Item"];
                }
                else if (workItemTypeSource.Name == "Issue")
                {
                    workItemTypeTarget = workItemTypes["Impediment"];
                }
                else
                {
                    // not automatically mapped
                    continue;
                }

                WorkItemFieldMap m = new WorkItemFieldMap(workItemTypeSource, workItemTypeTarget);
                m.GenerateDefaultMap();
                fieldMap.fieldMapping.Add(m);
                fieldMap.mapping[workItemTypeSource] = workItemTypeTarget;
            }

            return fieldMap;
        }

        public void SaveToDisk(string filename)
        {
            using (var tw = File.CreateText(filename))
            {
                var serializer = Newtonsoft.Json.JsonSerializer.Create();
                serializer.Serialize(tw, GenerateWorkItemFieldMap2List().ToList());
            }
        }

        private IEnumerable<WorkItemFieldMap2> GenerateWorkItemFieldMap2List()
        {
            foreach (var item in mapping)
            {
                yield return new WorkItemFieldMap2{SourceWorkIemType = item.Key.Name, TargetWorkIemType = item.Value.Name, SourceFields = item.Key.FieldDefinitions.Cast<FieldDefinition>().Select(a => a.ReferenceName).ToList(), TargetFields = item.Value.FieldDefinitions.Cast<FieldDefinition>().Select(a => a.ReferenceName).ToList(), MappedReferenceNames = GetFieldMapping(item.Key, item.Value)?.ToDictionary(a => a.Key.ReferenceName, a => a.Value.ReferenceName), };
            }
        }

        public void ReadFromDisk(string filename, TfsProject source, TfsProject target)
        {
            using (var tw = File.OpenText(filename))
            {
                var serializer = JsonSerializer.Create();
                var st = serializer.Deserialize<List<WorkItemFieldMap2>>(new JsonTextReader(tw));
                mapping = new Dictionary<WorkItemType, WorkItemType>();
                fieldMapping = new List<WorkItemFieldMap>();
                foreach (var item in st)
                {
                    var sourceType = source.project.WorkItemTypes[item.SourceWorkIemType];
                    var targetType = target.project.WorkItemTypes[item.TargetWorkIemType];
                    mapping[sourceType] = targetType;
                    var newFm = new WorkItemFieldMap(sourceType, targetType);
                    foreach (var fieldmap in item.MappedReferenceNames)
                    {
                        var sourceFieldDefinition = sourceType.FieldDefinitions.Cast<FieldDefinition>().First(a => a.ReferenceName == fieldmap.Key);
                        var targetFieldDefinition = targetType.FieldDefinitions.Cast<FieldDefinition>().First(a => a.ReferenceName == fieldmap.Value);
                        newFm.mapping[sourceFieldDefinition] = targetFieldDefinition;
                    }

                    fieldMapping.Add(newFm);
                }
            }
        }

        internal Dictionary<FieldDefinition, FieldDefinition> GetFieldMapping(WorkItemType currentSourceWorkItemType, WorkItemType currentTargetWorkItemType)
        {
            var f = fieldMapping.FirstOrDefault(a => a.sourceWorkItemType == currentSourceWorkItemType && a.targetWorkItemType == currentTargetWorkItemType);
            if (f == null)
                return null;
            return f.mapping;
        }

        internal WorkItemType GetMapping(WorkItemType currentSourceWorkItemType)
        {
            WorkItemType target;
            if (mapping.TryGetValue(currentSourceWorkItemType, out target))
                return target;
            return null;
        }
    }

    public class WorkItemFieldMap
    {
        public Dictionary<FieldDefinition, FieldDefinition> mapping = new Dictionary<FieldDefinition, FieldDefinition>();
        public WorkItemType sourceWorkItemType;
        public WorkItemType targetWorkItemType;
        public WorkItemFieldMap(WorkItemType sourceWorkItemType, WorkItemType targetWorkItemType)
        {
            this.sourceWorkItemType = sourceWorkItemType;
            this.targetWorkItemType = targetWorkItemType;
        }

        public void GenerateDefaultMap()
        {
            var sourceFields = sourceWorkItemType.FieldDefinitions.Cast<FieldDefinition>().ToList();
            var targetFields = targetWorkItemType.FieldDefinitions.Cast<FieldDefinition>().ToList();
            foreach (FieldDefinition field in sourceWorkItemType.FieldDefinitions)
            {
                if (field.ReferenceName == "System.Id" || field.ReferenceName == "System.AreaId" || field.ReferenceName == "System.IterationId" || field.ReferenceName == "System.Rev" || field.ReferenceName == "System.Watermark")
                {
                    continue;
                }

                var firstByReferenceName = targetFields.FirstOrDefault(a => a.ReferenceName == field.ReferenceName);
                if (firstByReferenceName != null)
                {
                    mapping[field] = firstByReferenceName;
                }

                var firstByName = targetFields.FirstOrDefault(a => a.Name == field.Name);
                if (firstByName != null)
                {
                    mapping[field] = firstByName;
                }
            }
        }
    }
}