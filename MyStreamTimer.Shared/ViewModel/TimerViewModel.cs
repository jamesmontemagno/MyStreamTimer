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
        float currentMinutes;
        string currentFinished, currentOutput, currentFileName;
        readonly Timer timer;
        string identifier;

        Settings settings;
        public ICommand StartStopTimerCommand { get; }
        public ICommand CopyFilePathCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddMinuteCommand { get; }

        float bootMins = -1;

        public TimerViewModel(string id, bool bootStart = false, float bootMins = -1)
        {
            identifier = id;
            settings = new Settings(id);
            this.bootMins = bootMins;

            switch (identifier)
            {
                case Constants.Countdown:
                    IsDown = true;
                    break;
                case Constants.Countup:
                    IsDown = false;
                    break;
                case Constants.Giveaway:
                    IsDown = true;
                    break;
            }

            StartStopTimerCommand = new Command(ExecuteStartStopTimerCommand);
            CopyFilePathCommand = new Command(ExecuteCopyFilePathCommand);
            ResetCommand = new Command(ExecuteResetCommand);
            AddMinuteCommand = new Command(ExecuteAddMinuteCommand);
            timer = new Timer(250);
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;

            if (AutoStart || bootStart)
                ExecuteStartStopTimerCommand();
        }

        public void Init(float mins)
        {
            if (IsBusy)
                ExecuteStartStopTimerCommand();

            bootMins = mins;

            ExecuteStartStopTimerCommand();
        }

        bool isDown = true;
        public bool IsDown
        {
            get => isDown;
            set => SetProperty(ref isDown, value);
        }

        public int Minutes
        {
            get => settings.Minutes;
            set
            {
                settings.Minutes = value;
                OnPropertyChanged(nameof(Minutes));
            }
        }

        public string Output
        {
            get => settings.Output;
            set
            {
                settings.Output = value;
                OnPropertyChanged(nameof(Output));
            }
        }

        public bool AutoStart
        {
            get => settings.AutoStart;
            set
            {
                settings.AutoStart = value;
                OnPropertyChanged(nameof(AutoStart));
            }
        }

        public string Finish
        {
            get => settings.Finish;
            set
            {
                settings.Finish = value;
                OnPropertyChanged(nameof(Finish));
            }
        }
        public string FileName
        {
            get => settings.FileName;
            set
            {
                settings.FileName = value;
                OnPropertyChanged(nameof(FileName));
            }
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

        string GetDirectory()
        {
            var clipboard = ServiceContainer.Resolve<IClipboard>();
            if (clipboard == null)
                throw new Exception("Clipboard must be implemented");

            var folder = clipboard.BaseDirectory;
            return Path.Combine(folder, "MyStreamTimer");
        }

        void ExecuteCopyFilePathCommand()
        {
            var directory = GetDirectory();
            var clipboard = ServiceContainer.Resolve<IClipboard>();
            clipboard?.CopyToClipboard(directory);
        }

        void ExecuteAddMinuteCommand()
        {
            if (!IsBusy)
                return;
            endTime = endTime.AddMinutes(1);
        }

        void ExecuteResetCommand()
        {
            if (!IsBusy)
                return;
            //Stop
            ExecuteStartStopTimerCommand();
            //Start
            ExecuteStartStopTimerCommand();
        }

        

        void ExecuteStartStopTimerCommand()
        {
            try
            {
                string.Format(Output, TimeSpan.FromSeconds(5));
            }
            catch
            {
                CountdownOutput = @"Invalid time format. Use {0:hh\:mm\:ss}";
                return;
            }

            try
            {
                currentFileName = GetDirectory();

                if (!Directory.Exists(currentFileName))
                    Directory.CreateDirectory(currentFileName);

                currentFileName = Path.Combine(currentFileName, FileName);
            }
            catch (Exception ex)
            {
                CountdownOutput = ex.Message;
                return;
            }

            IsBusy = !IsBusy;
            StartStop = IsBusy ? "Stop" : "Start";

            timer.Enabled = IsBusy;

            if (!IsBusy)
                return;

            currentFinished = Finish;
            currentIsDown = IsDown;
            if (bootMins > 0)
            {
                currentMinutes = bootMins;
                bootMins = -1;
            }
            else
            {
                currentMinutes = Minutes;
            }
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
                // This is a bit of a shell game to reduce the chance that two programs get into a race 
                // condition by accessing a file while it's being written.
                // To do this, we write to a temp file, then move the old file to a "DeleteMe" name, 
                // move the temp file to our timer filename, then delete the "DeleteMe" file.
                // The slowest operation is WriteAllText, and this method makes sure that operation happens on 
                // a throwaway filename, so that OBS or another application doesn't try to hit the file
                // while it's being written
                var path = Path.GetDirectoryName(currentFileName);
                var tempFilename = Path.Combine(path, Path.GetRandomFileName());
                var deleteMeFilename = Path.Combine(path, Path.GetRandomFileName());

                // write to the temp file
                File.WriteAllText(tempFilename, CountdownOutput);

                // do the swap
                File.Move(currentFileName, deleteMeFilename);
                File.Move(tempFilename, currentFileName);

                // kill the old timer file
                File.Delete(deleteMeFilename);
            }
            catch (Exception ex)
            {
                CountdownOutput = ex.Message;
            }
        }
    }
}
