using MvvmHelpers;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using MyStreamTimer.Shared.Model;
using System;
using System.IO;
using System.Timers;
using System.Windows.Input;

// kymphillpotts cheered 150 March 9th 2019

namespace MyStreamTimer.Shared.ViewModel
{
    public class TimerViewModel : BaseViewModel
    {
        DateTime startTime;
        DateTime endTime;
        bool currentIsDown;
        int currentMinutes;
        string currentFinished, currentOutput, currentFileName;
        readonly Timer timer;

        public TimerViewModel()
        {
            StartStopTimerCommand = new Command(ExecuteStartStopTimerCommand);
            CopyFilePathCommand = new Command(ExecuteCopyFilePathCommand);
            timer = new Timer(250);
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
        }



        bool isDown = true;
        public bool IsDown
        {
            get => isDown;
            set => SetProperty(ref isDown, value);
        }

        bool isUp;
        public bool IsUp
        {
            get => isUp;
            set => SetProperty(ref isUp, value);
        }
        int minutes = 5;
        public int Minutes
        {
            get => minutes;
            set => SetProperty(ref minutes, value);
        }
        string output = @"Starting in {0:mm\:ss}";
        public string Output
        {
            get => output;
            set => SetProperty(ref output, value);
        }
        string finish = "Let's do this!";
        public string Finish
        {
            get => finish;
            set => SetProperty(ref finish, value);
        }
        string fileName = "countdown.txt";
        public string FileName
        {
            get => fileName;
            set => SetProperty(ref fileName, value);
        }

        string countdownOutput;
        public string CountdownOutput
        {
            get => countdownOutput;
            set => SetProperty(ref countdownOutput, value);
        }

        string startStop = "Start";
        public string StartStop
        {
            get => startStop;
            set => SetProperty(ref startStop, value);
        }

        public ICommand CopyFilePathCommand { get; set; }

        void ExecuteCopyFilePathCommand()
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            folder = Path.Combine(folder, "MyStreamTimer");

            var clipboard = ServiceContainer.Resolve<IClipboard>();

            clipboard?.CopyToClipboard(folder);
        }

        public ICommand StartStopTimerCommand { get; set; }

        void ExecuteStartStopTimerCommand()
        {
            try
            {
                string.Format(Output, TimeSpan.FromSeconds(5));
            }
            catch
            {
                CountdownOutput = @"Invalid time format. Use {0:mm\:ss}";
            }

            try
            {
                currentFileName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                currentFileName = Path.Combine(currentFileName, "MyStreamTimer");
                if (!Directory.Exists(currentFileName))
                    Directory.CreateDirectory(currentFileName);

                currentFileName = Path.Combine(currentFileName, FileName);
            }
            catch (Exception ex)
            {
                CountdownOutput = ex.Message;
            }

            IsBusy = !IsBusy;
            StartStop = IsBusy ? "Stop" : "Start";

            timer.Enabled = IsBusy;

            if (!IsBusy)
                return;

            currentFinished = Finish;
            currentIsDown = IsDown;
            currentMinutes = Minutes;
            currentOutput = Output;

            startTime = DateTime.Now;
            endTime = DateTime.Now.AddMinutes(currentMinutes);
            TimerElapsed(null, null);
        }
        void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            if (currentIsDown)
            {
                if (now >= endTime)
                {
                    ExecuteStartStopTimerCommand();
                    CountdownOutput = currentFinished;
                }
                else
                {
                    var elapsedTime = endTime - DateTime.Now;
                    CountdownOutput = string.Format(currentOutput, elapsedTime);
                }
            }
            else
            {
                var elapsedTime = DateTime.Now - startTime;
                CountdownOutput = string.Format(currentOutput, elapsedTime);
            }

            WriteTimeToDisk();
        }

        void WriteTimeToDisk()
        {
            try
            {
                File.WriteAllText(currentFileName, CountdownOutput);
            }
            catch (Exception ex)
            {
                CountdownOutput = ex.Message;
            }
        }
    }
}
