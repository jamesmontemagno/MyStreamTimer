using System;
using System.Windows.Input;
using MvvmHelpers;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.Model;

namespace MyStreamTimer.Shared.ViewModel
{
    public class AboutViewModel: BaseViewModel
    {
        public ICommand OpenUrlCommand { get; }
        public AboutViewModel()
        {
            OpenUrlCommand = new Command<string>(ExecuteOpenUrlCommand);
        }

        void ExecuteOpenUrlCommand(string url)
        {
            var clipboard = ServiceContainer.Resolve<IClipboard>();
            if (clipboard == null)
                throw new Exception("Clipboard must be implemented");

            clipboard.OpenUrl(url);
        }
    }
}
