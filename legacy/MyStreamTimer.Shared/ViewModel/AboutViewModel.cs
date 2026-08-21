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
        IPlatformHelpers platformHelpers;
        public string Version { get; }
        public ICommand OpenUrlCommand { get; }
        public AboutViewModel()
        {
            OpenUrlCommand = new Command<string>(ExecuteOpenUrlCommand);
            directory = GlobalSettings.DirectoryPath;
            platformHelpers = ServiceContainer.Resolve<IPlatformHelpers>();
            if (platformHelpers == null)
                throw new Exception("Platform Helpers must be implemented");

            Version = platformHelpers.Version;
        }

        void ExecuteOpenUrlCommand(string url)
        {
            platformHelpers.OpenUrl(url);
        }

        string directory;
        public string Directory
        {
            get => directory;
            set => SetProperty(ref directory, value);

        }


        public bool StayOnTop
        {
            get => GlobalSettings.StayOnTop;
            set
            {
                GlobalSettings.StayOnTop = value;
                platformHelpers.SetScreenSaver(value);

                OnPropertyChanged();
            }
        }
    }
}
