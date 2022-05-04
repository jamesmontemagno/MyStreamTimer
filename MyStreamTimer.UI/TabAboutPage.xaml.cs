using System;
using System.Collections.Generic;
using System.IO;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.ViewModel;
using Xamarin.Forms;

namespace MyStreamTimer.UI
{
    public partial class TabAboutPage : ContentPage
    {
        public TabAboutPage()
        {
            InitializeComponent();
        }

        void Button_Clicked(System.Object sender, System.EventArgs e)
        {
            var platform = ServiceContainer.Resolve<IPlatformHelpers>();
            var defaultDirectoryPath = Path.Combine(platform.BaseDirectory, "MyStreamTimer");
            var vm = (AboutViewModel)BindingContext;
            vm.Directory = defaultDirectoryPath;
        }

        async void ButtonPickFolder_Clicked(System.Object sender, System.EventArgs e)
        {
            var vm = (AboutViewModel)BindingContext;
            try
            {
                var platform = ServiceContainer.Resolve<IPlatformHelpers>();
                var location = await platform.PickFolder();
                if (!string.IsNullOrWhiteSpace(location))
                    vm.Directory = location;
            }
            catch (Exception ex)
            {

            }
        }

        async void ButtonTest_Clicked(System.Object sender, System.EventArgs e)
        {
            var vm = (AboutViewModel)BindingContext;
            if (GlobalSettings.DirectoryPath == vm.Directory)
                return;

            var platform = ServiceContainer.Resolve<IPlatformHelpers>();

            if(platform.HasRunningTimers)
            {
                await DisplayAlert("Active timers", "Please stop all timbers before changing the save directory", "OK");
                return;
            }
            try
            {
                try
                {

                    if(Device.RuntimePlatform == Device.macOS)
                    {
                        
                        platform.WriteFileNative(vm.Directory);
                    }
                }
                catch (Exception ex)
                {

                }


                var dir = vm.Directory;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                dir = Path.Combine(dir, Path.GetRandomFileName());
                if (File.Exists(dir))
                    File.Delete(dir);

                File.WriteAllText(dir, string.Empty);
                if (File.Exists(dir))
                    File.Delete(dir);
                await DisplayAlert("Success", "This directory is valid and can be accessed! New files will be saved here.", "OK");
                GlobalSettings.DirectoryPath = vm.Directory;
            }
            catch
            {
                await DisplayAlert("Invalid Directory", "Double check this is a valid directory path, or make sure that My Stream Timer has full write access to put files in this directory.", "OK");
            }
        }
    }
}
