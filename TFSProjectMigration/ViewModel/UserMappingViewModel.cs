using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using System;
using TFSProjectMigration.Conversion.Users;

namespace TFSProjectMigration
{
    public class UserMappingViewModel : ViewModelBase
    {
        private Func<TfsProject> sourceProject;
        private Func<TfsProject> targetProject;

        public UserMappingViewModel(Func<TfsProject> sourceProject, Func<TfsProject> targetProject)
        {
            this.sourceProject = sourceProject;
            this.targetProject = targetProject;

            this.GenerateUserMapping = new RelayCommand(generateUserMapping);
            this.LoadUserMapping = new RelayCommand(loadUserMapping);
            this.SaveUserMapping = new RelayCommand(saveUserMapping);
        }

        private void saveUserMapping()
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "*.json|*.json";

            if (sfd.ShowDialog() ?? false)
            {
                Usermap.SaveToDisk(sfd.FileName);
            }

        }

        private void loadUserMapping()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "*.json|*.json";

            if (ofd.ShowDialog() ?? false)
            {
                Usermap.ReadFromDisk(ofd.FileName);
            }
        }


        private void generateUserMapping()
        {
            UserMapper m = new UserMapper(sourceProject(), targetProject());
            this.Usermap = m.MapUserIds();
        }


        public RelayCommand GenerateUserMapping { get; private set; }
        public RelayCommand LoadUserMapping { get; private set; }
        public RelayCommand SaveUserMapping { get; private set; }


        public UserMap Usermap { get; internal set; } = new UserMap();

        internal void Refresh()
        {
            
        }
    }
}