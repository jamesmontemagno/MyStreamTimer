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
                var (start, mins, vm) = Utils.ParseStartupArgs(uri.AbsoluteUri);
                MyStreamTimer.UI.MainPage.OpeningArgs = (start, mins, vm);
                if (start && mins >= 0)
                {
                    var first = vm == "countdown" || vm == "countdown1";
                    var second = vm == "countdown2";
                    var third = vm == "countdown3";
                    if (first)
                    {
                        MyStreamTimer.UI.MainPage.DownVM?.Init(mins);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[0];
                    }
                    else if (second)
                    {
                        MyStreamTimer.UI.MainPage.DownVM2?.Init(mins);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[1];
                    }
                    else if (third)
                    {
                        MyStreamTimer.UI.MainPage.DownVM3?.Init(mins);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[2];
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
