using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static Mutex mutex = null;
        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "My Stream Timer";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                //app is already running! Exiting the application  
                Application.Current.Shutdown();
            }

            MyStreamTimer.WPF.MainWindow.StartArgs = e;
            base.OnStartup(e);
            ServiceContainer.Register<IClipboard>(() => new ClipboardImplementation());

        }
    }
}
