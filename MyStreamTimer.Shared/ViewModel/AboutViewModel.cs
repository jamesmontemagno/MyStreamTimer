using System;
using System.Windows.Input;
using MvvmHelpers;
using MvvmHelpers.Commands;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
namespace MyStreamTimer.Shared.ViewModel
{
    public class AboutViewModel: BaseViewModel
    {
        public ICommand OpenUrlCommand { get; }
        public AboutViewModel()
        {
            OpenUrlCommand = new Command<string>(ExecuteOpenUrlCommand);
            directory = GlobalSettings.DirectoryPath;
        }

        void ExecuteOpenUrlCommand(string url)
        {
            var platform = ServiceContainer.Resolve<IPlatformHelpers>();
            if (platform == null)
                throw new Exception("Platform Helpers must be implemented");

            platform.OpenUrl(url);
        }

        string directory;
        public string Directory
        {
            get => directory;
            set
            {
                if (SetProperty(ref directory, value))
                    GlobalSettings.DirectoryPath = value;
            }

        }

        
        public bool StayOnTop
        {
            get => GlobalSettings.StayOnTop;
            set
            {
                GlobalSettings.StayOnTop = value;
                var platform = ServiceContainer.Resolve<IPlatformHelpers>();
                if (platform != null)
                    platform.SetScreenSaver(value);

                OnPropertyChanged();
            }
        }
    }
}
