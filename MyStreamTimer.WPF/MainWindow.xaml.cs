using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyStreamTimer.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static StartupEventArgs StartArgs { get; set; }
        public MainWindow()
        {
            InitializeComponent();

            var start = false;
            var mins = -1;
            try
            {
                var first = StartArgs?.Args?.FirstOrDefault();
                if (first != null)
                {
                    var uri = new Uri(first);
                    if(uri.Host.ToLower() == "countdown" && uri.Query.ToLower().Contains("?mins=") && int.TryParse(uri.Query.Remove(0, 6), out mins))
                    {
                        start = true;
                    }
                }
            }
            catch (Exception)
            {

            }

            TabItemDown.DataContext = new TimerViewModel(Constants.Countdown, start, mins);
            TabItemUp.DataContext = new TimerViewModel(Constants.Countup);
            TabItemGiveaway.DataContext = new TimerViewModel(Constants.Giveaway);            
        }

        void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.FileName = e.Uri.OriginalString;
            process.Start();
            e.Handled = true;
        }
    }
}
