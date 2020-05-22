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
    }
}
