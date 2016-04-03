// Decompiled with JetBrains decompiler
// Type: TFSProjectMigration.WorkItemTypeMappingViewModel
// Assembly: Total TFS Migration Tool, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E9A1E242-0589-41B9-9F12-7F69893176EC
// Assembly location: C:\projects\_github\TotalTfsMigrationTool\TFSProjectMigration\bin\Debug\Total TFS Migration Tool.exe
// Compiler-generated code is shown

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using TFSProjectMigration.Conversion.WorkItems;

namespace TFSProjectMigration
{
  public class WorkItemTypeMappingViewModel : ViewModelBase
  {
    private WorkItemType currentSourceWorkItemType;
    private WorkItemType currentTargetWorkItemType;
    private Func<TfsProject> sourceProject;
    private Func<TfsProject> targetProject;

    public RelayCommand UnmapWorkItemTypes { get; set; }

    public RelayCommand MapWorkItemTypes { get; set; }

    public WorkItemTypeMap FieldMap { get; set; }

    public List<MappedValue<WorkItemType>> SourceWorkItemTypes { get; set; }

    public List<MappedValue<WorkItemType>> TargetWorkItemTypes { get; set; }

    public WorkItemType CurrentSourceWorkItemType
    {
      get
      {
        return this.currentSourceWorkItemType;
      }
      set
      {
        this.currentSourceWorkItemType = value;
        this.UpdateMappedWorkItemType();
        this.UpdateCurrentMappedWorkItemFields();
      }
    }

    public WorkItemType CurrentTargetWorkItemType
    {
      get
      {
        return this.currentTargetWorkItemType;
      }
      set
      {
        this.currentTargetWorkItemType = value;
        this.UpdateCurrentMappedWorkItemFields();
      }
    }

    public FieldDefinition CurrentSourceFieldDefinition { get; set; }

    public FieldDefinition CurrentTargetFieldDefinition { get; set; }

    public List<MappedValue<FieldDefinition>> SourceFieldDefinitions { get; set; }

    public List<MappedValue<FieldDefinition>> TargetFieldDefinitions { get; set; }

    public Dictionary<FieldDefinition, FieldDefinition> CurrentMappedWorkItemFields { get; set; }

    public RelayCommand GenerateFieldMapping { get; set; }

    public RelayCommand LoadFieldMapping { get; set; }

    public RelayCommand SaveFieldMapping { get; set; }

    public WorkItemTypeMappingViewModel(Func<TfsProject> sourceProject, Func<TfsProject> targetProject)
    {
      this.FieldMap = new WorkItemTypeMap();
      this.CurrentMappedWorkItemFields= new Dictionary<FieldDefinition, FieldDefinition>();
      
      // ISSUE: method pointer
      this.MapWorkItemTypes = new RelayCommand(mapWorkItemTypes);
      // ISSUE: method pointer
      this.UnmapWorkItemTypes = new RelayCommand(unmapWorkItemTypes);
      // ISSUE: method pointer
      this.GenerateFieldMapping = new RelayCommand(generateFieldMapping);
      // ISSUE: method pointer
      this.LoadFieldMapping = new RelayCommand(loadFieldMapping);
      // ISSUE: method pointer
      this.SaveFieldMapping = new RelayCommand(saveFieldMapping);
      this.sourceProject = sourceProject;
      this.targetProject = targetProject;
    }

    public void Refresh()
    {
      this.SourceWorkItemTypes = sourceProject().project?.WorkItemTypes.Cast<WorkItemType>().Select(a=> new MappedValue<WorkItemType>(a)).ToList();
      this.TargetWorkItemTypes = targetProject().project?.WorkItemTypes.Cast<WorkItemType>().Select(a => new MappedValue<WorkItemType>(a)).ToList();

      this.RaisePropertyChanged("SourceWorkItemTypes");
      this.RaisePropertyChanged("TargetWorkItemTypes");
    }

    private void saveFieldMapping()
    {
      SaveFileDialog saveFileDialog = new SaveFileDialog();
      saveFileDialog.Filter = "*.json|*.json";
      bool? nullable = saveFileDialog.ShowDialog();
      if (!nullable.HasValue || !nullable.GetValueOrDefault())
        return;
      this.FieldMap.SaveToDisk(saveFileDialog.FileName);
    }

    private void loadFieldMapping()
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "*.json|*.json";
      bool? nullable = openFileDialog.ShowDialog();
      if (!nullable.HasValue || !nullable.GetValueOrDefault())
        return;
      this.FieldMap.ReadFromDisk(openFileDialog.FileName, this.sourceProject(), this.targetProject());
    }

    private void generateFieldMapping()
    {
      this.FieldMap = WorkItemTypeMap.MapTypes(this.sourceProject(), this.targetProject());
      this.RaisePropertyChanged("FieldMap");
    }

    private void mapWorkItemTypes()
    {
      this.FieldMap.mapping[this.CurrentSourceWorkItemType] = this.CurrentTargetWorkItemType;
    }

    private void unmapWorkItemTypes()
    {
      this.FieldMap.mapping.Remove(this.CurrentSourceWorkItemType);
      this.CurrentTargetWorkItemType = (WorkItemType) null;
      this.RaisePropertyChanged("CurrentTargetWorkItemType");
    }

    private void UpdateMappedWorkItemType()
    {
      this.currentTargetWorkItemType = this.FieldMap.GetMapping(this.CurrentSourceWorkItemType);
      this.RaisePropertyChanged("CurrentTargetWorkItemType");
    }

    private void UpdateCurrentMappedWorkItemFields()
    {
      this.CurrentMappedWorkItemFields = this.FieldMap.GetFieldMapping(this.CurrentSourceWorkItemType, this.CurrentTargetWorkItemType) ?? new Dictionary<FieldDefinition, FieldDefinition>();
      this.RaisePropertyChanged("CurrentMappedWorkItemFields");
            this.SourceFieldDefinitions = sourceProject().project?.WorkItemTypes.Cast<WorkItemType>().Where(a => a == CurrentSourceWorkItemType).FirstOrDefault()?.FieldDefinitions.Cast<FieldDefinition>().Select(a => new MappedValue<FieldDefinition>(a)).ToList(); //Enumerable.ToList(Enumerable.Select<FieldDefinition, MappedValue<FieldDefinition>>(Enumerable.Cast<FieldDefinition>((IEnumerable) this.CurrentSourceWorkItemType.FieldDefinitions), f=>  ?? (WorkItemTypeMappingViewModel.\u003C\u003Ec.\u003C\u003E9__31_0 = new Func<FieldDefinition, MappedValue<FieldDefinition>>((object) WorkItemTypeMappingViewModel.\u003C\u003Ec.\u003C\u003E9, __methodptr(\u003CUpdateCurrentMappedWorkItemFields\u003Eb__31_0)))));
      this.RaisePropertyChanged("SourceFieldDefinitions");
            this.TargetFieldDefinitions = targetProject().project?.WorkItemTypes.Cast<WorkItemType>().Where(a => a == CurrentTargetWorkItemType).FirstOrDefault()?.FieldDefinitions.Cast<FieldDefinition>().Select(a => new MappedValue<FieldDefinition>(a)).ToList();
      this.RaisePropertyChanged("TargetFieldDefinitions");
    }

  
    }
}

