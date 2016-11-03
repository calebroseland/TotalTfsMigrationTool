using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.TeamFoundation.Client;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TFSProjectMigration.Conversion;
using TFSProjectMigration.Conversion.Users;

namespace TFSProjectMigration
{
    public class MigrationViewModel : ViewModelBase
    {
        public MigrationViewModel()
        {            
            BrowseSource = new RelayCommand(ConnectSourceProjectButton_Click);
            BrowseTarget = new RelayCommand(ConnectDestinationProjectButton_Click);

            BrowseMappingFile = new RelayCommand(browseMappingFile);

            ValidateConfiguration = new RelayCommand(validateConfiguration);
            StartMigration = new RelayCommand(startMigration);

            UserMapping = new UserMappingViewModel(() => SourceProject, () => TargetProject);
            FieldMapping = new WorkItemTypeMappingViewModel(() => SourceProject, () => TargetProject);
        }

        private void browseMappingFile()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "*.dat|*.dat";

            if (dlg.ShowDialog()??false)
            {
                MappingFile = dlg.FileName;
                RaisePropertyChanged("MappingFile");
            }
        }

        public string MappingFile { get; set; }


        bool busy;

        private async void startMigration()
        {
            // lame check to prevent running multiple threads at once
            if (busy)
                return;

            try
            {
                MigrateProject mp = new MigrateProject(SourceProject, TargetProject);

                mp.Log = (logMessage) =>
                {
                    Log(logMessage);
                };
                                
                mp.FieldMap = FieldMapping.FieldMap;
                mp.Usermap = UserMapping.Usermap;

                mp.workItemIdMap = new Conversion.WorkItems.WorkItemIdMap(MappingFile);

                await Task.Factory.StartNew(() => mp.StartMigration(IsNotIncludeClosed, IsNotIncludeRemoved, IsIncludeHistoryComment, IsIncludeHistoryLink, ShouldFixMultilineFields));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex);
            }
            finally
            {
                busy = false;
            }
        }

        private void validateConfiguration()
        {
            //foreach (var item in FieldMapping.GetConfigurationErrors())
            //{
            //    Log(item);
            //}
        }

        public string MigrationName  {  get; set;  }

        public event Action<string> Log;
        public WorkItemTypeMappingViewModel FieldMapping { get; set; }

        public UserMappingViewModel UserMapping { get; set; }

        public TfsProject SourceProject
        {
            get;
            set;
        }

        public TfsProject TargetProject
        {
            get;
            set;
        }

        public RelayCommand BrowseSource
        {
            get;
            set;
        }

        public RelayCommand BrowseTarget
        {
            get;
            set;
        }

        public RelayCommand ValidateConfiguration
        {
            get;
            private set;
        }

        public RelayCommand StartMigration
        {
            get;
            private set;
        }

        public RelayCommand Migrate
        {
            get;
            set;
        }

        public bool IsNotIncludeClosed
        {
            get;
            set;
        }

        public bool IsNotIncludeRemoved
        {
            get;
            set;
        }
        public bool IsIncludeHistoryComment
        {
            get;
            set;
        }
        public bool IsIncludeHistoryLink
        {
            get;
            set;
        }
        public bool ShouldFixMultilineFields
        {
            get;
            set;
        }
        public RelayCommand BrowseMappingFile { get; private set; }

        private void ConnectSourceProjectButton_Click()
        {
            TeamProjectPicker tpp = new TeamProjectPicker(TeamProjectPickerMode.SingleProject, false);
            System.Windows.Forms.DialogResult result = tpp.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                SourceProject = new TfsProject(tpp.SelectedTeamProjectCollection, tpp.SelectedProjects[0].Name);
                RaisePropertyChanged("SourceProject");

                FieldMapping.Refresh();
                UserMapping.Refresh();
            }
        }

        private void ConnectDestinationProjectButton_Click()
        {
            TeamProjectPicker tpp = new TeamProjectPicker(TeamProjectPickerMode.SingleProject, false);
            System.Windows.Forms.DialogResult result = tpp.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TargetProject = new TfsProject(tpp.SelectedTeamProjectCollection, tpp.SelectedProjects[0].Name);
                RaisePropertyChanged("TargetProject");

                FieldMapping.Refresh();
                UserMapping.Refresh();
            }
        }
    }

    public class MappedValue<T>
    {
        public MappedValue(T innerValue)
        {
            this.InnerValue = innerValue;
        }

        public bool IsSet
        {
            get;
            set;
        }

        public T InnerValue
        {
            get;
            set;
        }

        public override bool Equals(object obj)
        {
            var c = obj as MappedValue<T>;
            if (c == null)
                return false;
            return Equals(c.InnerValue, InnerValue);
        }

        public override int GetHashCode()
        {
            return InnerValue?.GetHashCode() ?? 7;
        }
    }
}