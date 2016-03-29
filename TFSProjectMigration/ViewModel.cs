using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TFSProjectMigration.Conversion;
using TFSProjectMigration.Conversion.WorkItems;

namespace TFSProjectMigration
{
   public class MigrationViewModel: ViewModelBase
   {
      public MigrationViewModel()
      {
         BrowseSource = new RelayCommand(ConnectSourceProjectButton_Click);
         BrowseTarget = new RelayCommand(ConnectDestinationProjectButton_Click);

         ValidateConfiguration = new RelayCommand(validateConfiguration);
         StartMigration = new RelayCommand(startMigration);
      }

      private void startMigration()
      {
         try
         {
            MigrateProject mp = new MigrateProject(SourceProject, TargetProject);
            mp.Log = (logMessage) =>
            {
               Log(logMessage);
            };

            mp.StartMigration(IsNotIncludeClosed, IsNotIncludeRemoved, Mapping.FieldMap);

         }
         catch (Exception ex)
         {
            MessageBox.Show("Error: " + ex);
         }
      }

      private void validateConfiguration()
      {
         foreach (var item in Mapping.GetConfigurationErrors())
         {
            Log(item);
         }
      }

      public event Action<string> Log;
      

      public MappingViewModel Mapping { get; set; } = new MappingViewModel();

      public TfsProject SourceProject { get; set; }
      public TfsProject TargetProject { get; set; }

      public RelayCommand BrowseSource { get; set; }
      public RelayCommand BrowseTarget { get; set; }
      public RelayCommand ValidateConfiguration { get; private set; }
      public RelayCommand StartMigration { get; private set; }

      public RelayCommand Migrate { get; set; }

      public bool IsNotIncludeClosed { get; set; }
      public bool IsNotIncludeRemoved { get; set; }
      

      private void ConnectSourceProjectButton_Click()
      {
         TeamProjectPicker tpp = new TeamProjectPicker(TeamProjectPickerMode.SingleProject, false);
         DialogResult result = tpp.ShowDialog();
         if (result == DialogResult.OK)
         {            
            SourceProject = new TfsProject(tpp.SelectedTeamProjectCollection, tpp.SelectedProjects[0].Name);
            RaisePropertyChanged("SourceProject");

            Mapping = new MappingViewModel(SourceProject, TargetProject);
            RaisePropertyChanged("Mapping");
         }
      }

      private void ConnectDestinationProjectButton_Click()
      {
         TeamProjectPicker tpp = new TeamProjectPicker(TeamProjectPickerMode.SingleProject, false);
         DialogResult result = tpp.ShowDialog();

         if (result == DialogResult.OK)
         {
            TargetProject = new TfsProject(tpp.SelectedTeamProjectCollection, tpp.SelectedProjects[0].Name);
            RaisePropertyChanged("TargetProject");

            Mapping = new MappingViewModel(SourceProject, TargetProject);
            RaisePropertyChanged("Mapping");
         }
      }      
   }

   public class MappingViewModel: ViewModelBase
   {
      public MappingViewModel()
      {
        
      }


      public MappingViewModel(TfsProject sourceProject, TfsProject targetProject)
      {
         this.MapWorkItemTypes = new RelayCommand(mapWorkItemTypes);
         this.UnmapWorkItemTypes = new RelayCommand(unmapWorkItemTypes);

         this.sourceProject = sourceProject;
         this.targetProject = targetProject;

         if (sourceProject.collection == null)
            return;

         if (targetProject.collection == null)
            return;
         
         FieldMap = WorkItemTypeMap.MapTypes(sourceProject, targetProject);
         SourceWorkItemTypes = sourceProject.project.WorkItemTypes.Cast<WorkItemType>().Select(a => new MappedValue<WorkItemType>(a)).ToList();
         TargetWorkItemTypes = targetProject.project.WorkItemTypes.Cast<WorkItemType>().Select(a => new MappedValue<WorkItemType>(a)).ToList();         
      }

      private void mapWorkItemTypes()
      {
         FieldMap.mapping[CurrentSourceWorkItemType] = CurrentTargetWorkItemType;
      }

      private void unmapWorkItemTypes()
      {
         FieldMap.mapping.Remove(CurrentSourceWorkItemType);
      }


      public RelayCommand UnmapWorkItemTypes { get; set; }
      public RelayCommand MapWorkItemTypes { get; set; }

      public WorkItemTypeMap FieldMap { get; set; }

      public List<MappedValue<WorkItemType>> SourceWorkItemTypes { get; set; }
      public List<MappedValue<WorkItemType>> TargetWorkItemTypes { get; set; }

      public WorkItemType CurrentSourceWorkItemType
      {
         get { return currentSourceWorkItemType; }
         set
         {
            currentSourceWorkItemType = value;
            UpdateMappedWorkItemType();
            UpdateCurrentMappedWorkItemFields();
         }
      }

      private void UpdateMappedWorkItemType()
      {
         currentTargetWorkItemType = FieldMap.GetMapping(CurrentSourceWorkItemType);
         RaisePropertyChanged("CurrentTargetWorkItemType");
      }

      private void UpdateCurrentMappedWorkItemFields()
      {
         CurrentMappedWorkItemFields = FieldMap.GetFieldMapping(CurrentSourceWorkItemType, CurrentTargetWorkItemType) ?? new Dictionary<FieldDefinition, FieldDefinition>();
         RaisePropertyChanged("CurrentMappedWorkItemFields");

         SourceFieldDefinitions = CurrentSourceWorkItemType.FieldDefinitions.Cast<FieldDefinition>().Select(a=> new MappedValue<FieldDefinition>(a)).ToList();
         RaisePropertyChanged("SourceFieldDefinitions");

         TargetFieldDefinitions = CurrentTargetWorkItemType?.FieldDefinitions.Cast<FieldDefinition>().Select(a => new MappedValue<FieldDefinition>(a)).ToList();         
         RaisePropertyChanged("TargetFieldDefinitions");
      }

      internal IEnumerable<string> GetConfigurationErrors()
      {
         foreach (var sourceWIT in SourceWorkItemTypes.Select(a=>a.InnerValue))
         {
            var targetWIT = FieldMap.GetMapping(sourceWIT);
            if (targetWIT == null)
            {
               yield return "Work item type " + sourceWIT.Name + " is not mapped";
               continue;
            }

            var fieldMapping = FieldMap.GetFieldMapping(sourceWIT, targetWIT);
            foreach (var sourceFieldDef in sourceWIT.FieldDefinitions.Cast<FieldDefinition>())
            {
               FieldDefinition _;
               if (!fieldMapping.TryGetValue(sourceFieldDef, out _))
               {
                  yield return "Field " + sourceFieldDef.Name  + " on work item type " + sourceWIT.Name + " is not mapped";
               }
            }

            foreach (var targetFieldDef in targetWIT.FieldDefinitions.Cast<FieldDefinition>())
            {
               FieldDefinition _;
               if (fieldMapping.Values.Where(a=> a == targetFieldDef).Count() > 1)
               {
                  yield return "Field " + targetFieldDef.Name + " on work item type " + targetWIT.Name + " is mapped multiple times";
               }
            }
         }
      }

      public WorkItemType CurrentTargetWorkItemType
      {
         get { return currentTargetWorkItemType; }
         set
         {
            currentTargetWorkItemType = value;
            UpdateCurrentMappedWorkItemFields();
         }
      }


      public FieldDefinition CurrentSourceFieldDefinition { get; set; }
      public FieldDefinition CurrentTargetFieldDefinition { get; set; }

      public List<MappedValue<FieldDefinition>> SourceFieldDefinitions { get; set; }
      public List<MappedValue<FieldDefinition>> TargetFieldDefinitions { get; set; }


      public Dictionary<FieldDefinition, FieldDefinition> CurrentMappedWorkItemFields { get; set; } = new Dictionary<FieldDefinition, FieldDefinition>();
      

      private WorkItemType currentSourceWorkItemType;
      private WorkItemType currentTargetWorkItemType;
      private TfsProject sourceProject;
      private TfsProject targetProject;
   }

   public class MappedValue<T>
   {
      public MappedValue(T innerValue)
      {
         this.InnerValue = innerValue;
      }

      public bool IsSet { get; set; }
      public T InnerValue { get; set; }

      public override bool Equals(object obj)
      {
         var c = obj as MappedValue<T>;
         if (c == null)
            return false;
         return Equals(c.InnerValue, InnerValue);
      }

      public override int GetHashCode()
      {
         return InnerValue?.GetHashCode()??7;
      }
   }
}
