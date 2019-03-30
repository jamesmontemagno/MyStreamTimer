using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;

namespace MyStreamTimer.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ServiceContainer.Register<IClipboard>(() => new ClipboardImplementation());
        }
    }
}
