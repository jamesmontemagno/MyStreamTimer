using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
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
using Utils = MyStreamTimer.Shared.Helpers.Utils;

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
            float mins = -1;
            try
            {
                var first = StartArgs?.Args?.FirstOrDefault();
                (start, mins) = Utils.ParseStartupArgs(first);
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
            var clipboard = ServiceContainer.Resolve<IClipboard>();
            if (clipboard == null)
                throw new Exception("Clipboard must be implemented");

            clipboard.OpenUrl(e.Uri.OriginalString);

            e.Handled = true;
        }

        private void LabelCommandsMins_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            
            var clipboard = ServiceContainer.Resolve<IClipboard>();
            if (clipboard == null)
                throw new Exception("Clipboard must be implemented");

            var text = ((Label)sender).Content as string;
            clipboard.CopyToClipboard(text);
        }
    }
}
