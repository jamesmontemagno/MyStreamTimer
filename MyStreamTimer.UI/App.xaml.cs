using System;
using System.Collections.Generic;
using MyStreamTimer.Shared.Helpers;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly:XamlCompilation(XamlCompilationOptions.Compile)]

namespace MyStreamTimer.UI
{
    public partial class App : Application
    {
        public App()
        {

            InitializeComponent();
            MainPage = new MainPage();   
        }

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            try
            {
                var (start, mins) = Utils.ParseStartupArgs(uri.AbsoluteUri);
                MyStreamTimer.UI.MainPage.OpeningArgs = (start, mins);
                if (start && mins >= 0)
                    MyStreamTimer.UI.MainPage.DownVM?.Init(mins);
            }
            catch (Exception)
            {
            }
        }
    }
}
