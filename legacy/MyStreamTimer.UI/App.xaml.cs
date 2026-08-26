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
                var (action, mins, vm) = Utils.ParseStartupArgs(uri.AbsoluteUri);
                MyStreamTimer.UI.MainPage.OpeningArgs = (action, mins, vm);
                if (action != Utils.CommandAction.None)
                {
                    var first = vm == "countdown" || vm == "countdown1";
                    var second = vm == "countdown2";
                    var third = vm == "countdown3";
                    var fourth = vm == "countdown4" && GlobalSettings.IsPro;
                    var up = vm == "countup" || vm == "countup1";
                    var up2 = vm == "countup2" && GlobalSettings.IsPro;
                    if (first)
                    {
                        MyStreamTimer.UI.MainPage.DownVM?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[0];
                    }
                    else if (second)
                    {
                        MyStreamTimer.UI.MainPage.DownVM2?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[1];
                    }
                    else if (third)
                    {
                        MyStreamTimer.UI.MainPage.DownVM3?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[2];
                    }
                    else if (fourth)
                    {
                        MyStreamTimer.UI.MainPage.DownVM4?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[3];
                    }
                    else if(up)
                    {
                        MyStreamTimer.UI.MainPage.UpVM?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[4];
                    }
                    else if(up2)
                    {
                        MyStreamTimer.UI.MainPage.UpVM2?.Init(mins, action);
                        ((TabbedPage)MainPage).CurrentPage = ((TabbedPage)MainPage).Children[5];
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
