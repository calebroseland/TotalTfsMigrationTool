using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
