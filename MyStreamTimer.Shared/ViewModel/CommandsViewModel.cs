using System;
using System.Windows.Input;
using MvvmHelpers;
using MvvmHelpers.Commands;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.Shared.ViewModel
{
    public class CommandsViewModel: BaseViewModel
    {
        public ICommand CopyTextCommand { get; }
        public CommandsViewModel()
        {
            CopyTextCommand = new Command<string>(ExecuteCopyTextCommand);
        }

        void ExecuteCopyTextCommand(string text)
        {
            var clipboard = ServiceContainer.Resolve<IPlatformHelpers>();
            if (clipboard == null)
                throw new Exception("Clipboard must be implemented");

            clipboard.CopyToClipboard(text);
        }
    }
}
