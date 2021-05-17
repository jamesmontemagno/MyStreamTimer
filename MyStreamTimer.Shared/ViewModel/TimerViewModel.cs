using MvvmHelpers;
using MvvmHelpers.Commands;
using MyStreamTimer.Shared.Helpers;
using MyStreamTimer.Shared.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Command = MvvmHelpers.Commands.Command;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

// kymphillpotts cheered 150 March 9th 2019
// lubdubw subscribed view twitch prime! May 15th 2020

namespace MyStreamTimer.Shared.ViewModel
{
    public class TimerViewModel : BaseViewModel
    {


        DateTime startTime;
        DateTime endTime;
        bool currentIsDown;
        float currentMinutes;
        string currentFinished, currentOutput, currentFileName;
        int currentOutputStyle;
        bool currentBeepAtZero;
        readonly Timer timer;
        string identifier;

        Settings settings;
        public ICommand StartStopTimerCommand { get; }
        public AsyncCommand CopyFilePathCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddMinuteCommand { get; }
        public ICommand PauseResumeTimerCommand { get; }

        IPlatformHelpers platformHelpers;
        float bootMins = -1;
        CancellationTokenSource timerCTS;

        public TimerViewModel()
        {

        }

        public TimerViewModel(string id, bool bootStart = false, float bootMins = -1)
        {

            platformHelpers = ServiceContainer.Resolve<IPlatformHelpers>();
            identifier = id;
            settings = new Settings(id);
            this.bootMins = bootMins;

            InitializeFile();

            switch (identifier)
            {
                case Constants.Giveaway:
                case Constants.Countdown2:
                case Constants.Countdown3:
                case Constants.Countdown:
                    IsDown = true;
                    break;
                case Constants.Countup:
                    IsDown = false;
                    break;
            }

            StartStopTimerCommand = new Command(()=>ExecuteStartStopTimerCommand(true));
            CopyFilePathCommand = new AsyncCommand(ExecuteCopyFilePathCommand);
            ResetCommand = new Command(ExecuteResetCommand);
            AddMinuteCommand = new Command(ExecuteAddMinuteCommand);
            PauseResumeTimerCommand = new Command(ExecutePauseResumeTimerCommand);
            timer = new Timer(800);
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

        public bool UseMinutes
        {
            get => settings.UseMinutes;
            set
            {
                if (settings.UseMinutes != value)
                {
                    settings.UseMinutes = value;
                    OnPropertyChanged();
                }
            }
        }


        public TimeSpan FinishAtTime
        {
            get => settings.FinishAtTime;
            set
            {
                if (settings.FinishAtTime != value)
                {
                    settings.FinishAtTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Seconds
        {
            get => settings.Seconds;
            set
            {
                settings.Seconds = value;
                OnPropertyChanged();
            }
        }

        public int Minutes
        {
            get => settings.Minutes;
            set
            {
                settings.Minutes = value;
                OnPropertyChanged();
            }
        }

        public string Output
        {
            get => settings.Output;
            set
            {
                settings.Output = value;
                OnPropertyChanged();
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

        public bool BeepAtZero
        {
            get => settings.MakeSound;
            set
            {
                settings.MakeSound = value;
                OnPropertyChanged(nameof(BeepAtZero));
            }
        }

        public int OutputStyle
        {
            get => settings.OutputStyle;
            set
            {
                settings.OutputStyle = value;
                OnPropertyChanged(nameof(OutputStyle));
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
            set
            {
                platformHelpers.InvokeOnMainThread(() =>
                {
                    SetProperty(ref countdownOutput, value);
                });
            }
        }

        string startStop = "Start";
        public string StartStop
        {
            get => startStop;
            set => SetProperty(ref startStop, value);
        }

        bool canPauseResume = false;
        public bool CanPauseResume
        {
            get => canPauseResume;
            set => SetProperty(ref canPauseResume, value);
        }
        string pauseResume = "Resume";
        public string PauseResume
        {
            get => pauseResume;
            set => SetProperty(ref pauseResume, value);
        }

        string GetDirectory() => GlobalSettings.DirectoryPath;

        Task ExecuteCopyFilePathCommand()
        {
            var directory = GetDirectory();
            
            if (platformHelpers == null)
                return Task.CompletedTask;
            platformHelpers.CopyToClipboard(directory);
            if(platformHelpers.IsMac)
            {
                return platformHelpers.DisplayAlert("Path Copied", $"Path to file is located at {directory}. \n\n Use Command + Shift + G to bring up directory selection in Finder. \n\n The main folder may be named MyStreamTimer and not com.refractored.mystreamtimer in Finder. \n\n M1 Processors may link deeper into the Documents folder, browse manually in Finder if this is the case.");
            }

            return platformHelpers.DisplayAlert("Path Copied", $"Path to file is located at {directory}");

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

        void InitializeFile()
        {
            try
            {

                var dir = GetDirectory();

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                dir = Path.Combine(dir, FileName);
                if(!File.Exists(dir))
                    File.WriteAllText(dir, string.Empty);
            }
            catch
            {

            }
        }

        void ExecuteStartStopTimerCommand(bool forceReset = false)
        {
            if(forceReset)
            {
                bootMins = -1;
                extraTicksForUp = 0;
            }

            try
            {
                if(OutputStyle == 0)
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

            if(IsBusy)
            {
                timerCTS?.Cancel();
            }

            IsBusy = !IsBusy;
            StartStop = IsBusy ? "Stop" : "Start";

            //timer.Enabled = IsBusy;

            if(forceReset && !IsBusy)
            {
                CanPauseResume = false;
                PauseResume = "Resume";
            }

            if (!IsBusy)
                return;

            CanPauseResume = true;
            PauseResume = "Pause";

            currentFinished = Finish;
            currentIsDown = IsDown;
            var currentSeconds = 0;
            if (bootMins > 0)
            {
                currentMinutes = bootMins;
                bootMins = -1;
            }
            else
            {
                if (UseMinutes)
                {

                    currentMinutes = Minutes;
                    currentSeconds = Seconds;
                    
                }
                else
                {

                    if (FinishAtTime > DateTime.Now.TimeOfDay)
                        currentMinutes = (float)(FinishAtTime.TotalMinutes - DateTime.Now.TimeOfDay.TotalMinutes);
                    else
                    {
                        //time until midnight
                        currentMinutes = (float)(1440.0 - DateTime.Now.TimeOfDay.TotalMinutes);
                        currentMinutes += (float)FinishAtTime.TotalMinutes;
                    }

                }
            }
            currentOutput = Output;
            currentBeepAtZero = BeepAtZero;
            currentOutputStyle = OutputStyle;


            if (currentIsDown)
                startTime = DateTime.Now;
            else
                startTime = DateTime.Now.AddMinutes(-currentMinutes).AddSeconds(-currentSeconds);

            endTime = DateTime.Now.AddMinutes(currentMinutes).AddSeconds(currentSeconds);
            //TimerElapsed(null, null);

            timerCTS = new CancellationTokenSource();
            Task.Factory.StartNew(UpdateTimer, TaskCreationOptions.LongRunning, timerCTS.Token);
        }
        long extraTicksForUp;
        void ExecutePauseResumeTimerCommand()
        {
            if(IsBusy)
            {
                if (currentIsDown)
                {
                    bootMins = (float)(endTime - DateTime.Now).TotalMinutes;
                }
                else
                {
                    var elapsedTime = DateTime.Now.AddTicks(extraTicksForUp) - startTime;
                    extraTicksForUp = elapsedTime.Ticks;
                }
                PauseResume = "Resume";
            }

            ExecuteStartStopTimerCommand();

            CanPauseResume = true;
        }

        async void UpdateTimer(object thing)
        {
            while(!timerCTS.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var text = string.Empty;
                if (currentIsDown)
                {
                    if (now >= endTime)
                    {
                        text = currentFinished;
                        //WriteTimeToDisk(false, text);
                        ExecuteStartStopTimerCommand();
                        CountdownOutput = text;
                        WriteTimeToDisk(false, text);
                        return;
                    }
                    else
                    {
                        var elapsedTime = endTime - DateTime.Now;
                        switch (currentOutputStyle)
                        {
                            case 0:
                                text = string.Format(currentOutput, elapsedTime);
                                break;
                            case 1:
                                //Auto
                                if(Math.Floor(elapsedTime.TotalDays) > 0)
                                {
                                    if (elapsedTime.TotalDays >= 1000)
                                        text = string.Format("{0:dddd\\:hh\\:mm\\:ss}", elapsedTime);
                                    else if (elapsedTime.TotalDays >= 100)
                                        text = string.Format("{0:ddd\\:hh\\:mm\\:ss}", elapsedTime);
                                    else if (elapsedTime.TotalDays >= 10)
                                        text = string.Format("{0:dd\\:hh\\:mm\\:ss}", elapsedTime);
                                    else
                                        text = string.Format("{0:d\\:hh\\:mm\\:ss}", elapsedTime);
                                }
                                else if(Math.Floor(elapsedTime.TotalHours) > 0)
                                {
                                    if(elapsedTime.TotalHours >= 10)
                                        text = string.Format("{0:hh\\:mm\\:ss}", elapsedTime);
                                    else
                                        text = string.Format("{0:h\\:mm\\:ss}", elapsedTime);
                                }
                                else if(Math.Floor(elapsedTime.TotalMinutes) > 0)
                                {
                                    if (elapsedTime.TotalMinutes >= 10)
                                        text = string.Format("{0:mm\\:ss}", elapsedTime);
                                    else
                                        text = string.Format("{0:m\\:ss}", elapsedTime);
                                }
                                else
                                {
                                    if (elapsedTime.TotalSeconds >= 10)
                                        text = string.Format("{0:ss}", elapsedTime);
                                    else
                                        text = Math.Floor(elapsedTime.TotalSeconds).ToString("N0");
                                }
                                break;
                            case 2:
                                //Total seconds
                                text = Math.Floor(elapsedTime.TotalSeconds).ToString("N0");
                                break;
                            case 3:
                                // Total Mins:Seconds
                                text = $"{Math.Floor(elapsedTime.TotalMinutes).ToString("N0")}:{string.Format("{0:ss}", elapsedTime)}";
                                break;
                        }
                    }
                }
                else
                {
                    var elapsedTime = DateTime.Now.AddTicks(extraTicksForUp) - startTime;
                    
                    switch (currentOutputStyle)
                    {
                        case 0:
                            text = string.Format(currentOutput, elapsedTime);
                            break;
                        case 1:
                            //Auto
                            if (Math.Floor(elapsedTime.TotalDays) > 0)
                            {
                                if (elapsedTime.TotalDays >= 1000)
                                    text = string.Format("{0:ddddd\\:hh\\:mm\\:ss}", elapsedTime);
                                else if (elapsedTime.TotalDays >= 1000)
                                    text = string.Format("{0:dddd\\:hh\\:mm\\:ss}", elapsedTime);
                                else if (elapsedTime.TotalDays >= 100)
                                    text = string.Format("{0:ddd\\:hh\\:mm\\:ss}", elapsedTime);
                                else if (elapsedTime.TotalDays >= 10)
                                    text = string.Format("{0:dd\\:hh\\:mm\\:ss}", elapsedTime);
                                else
                                    text = string.Format("{0:d\\:hh\\:mm\\:ss}", elapsedTime);
                            }
                            else if (Math.Floor(elapsedTime.TotalHours) > 0)
                            {
                                if (elapsedTime.TotalHours >= 10)
                                    text = string.Format("{0:hh\\:mm\\:ss}", elapsedTime);
                                else
                                    text = string.Format("{0:h\\:mm\\:ss}", elapsedTime);
                            }
                            else if (Math.Floor(elapsedTime.TotalMinutes) > 0)
                            {
                                if (elapsedTime.TotalMinutes >= 10)
                                    text = string.Format("{0:mm\\:ss}", elapsedTime);
                                else
                                    text = string.Format("{0:m\\:ss}", elapsedTime);
                            }
                            else
                            {
                                if (elapsedTime.TotalSeconds >= 10)
                                    text = string.Format("{0:ss}", elapsedTime);
                                else
                                    text = Math.Floor(elapsedTime.TotalSeconds).ToString("N0");
                            }
                            break;
                        case 2:
                            //Total seconds
                            text = Math.Floor(elapsedTime.TotalSeconds).ToString("N0");
                            break;
                        case 3:
                            // Total Mins:Seconds
                            text = $"{Math.Floor(elapsedTime.TotalMinutes).ToString("N0")}:{string.Format("{0:ss}", elapsedTime)}";
                            break;
                    }
                }
                if (text != CountdownOutput)
                {
                    WriteTimeToDisk(false, text);
                    CountdownOutput = text;
                }
                await Task.Delay(500); 
            }
        }
        void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            //var now = DateTime.Now;
            //if (currentIsDown)
            //{
            //    if (now >= endTime)
            //    {
            //        CountdownOutput = currentFinished;
            //        WriteTimeToDisk(e == null);
            //        ExecuteStartStopTimerCommand();
            //        CountdownOutput = currentFinished;
            //        WriteTimeToDisk(e == null);
            //        return;
            //    }
            //    else
            //    {
            //        var elapsedTime = endTime - DateTime.Now;
            //        CountdownOutput = string.Format(currentOutput, elapsedTime);
            //    }
            //}
            //else
            //{
            //    var elapsedTime = DateTime.Now - startTime;
            //    CountdownOutput = string.Format(currentOutput, elapsedTime);
            //}

            //WriteTimeToDisk(e == null);
        }

        void WriteTimeToDisk(bool create, string text)
        {
            try
            {
                if(create)
                    File.WriteAllText(currentFileName, text);
                else
                {
                    using var streamWriter = new StreamWriter(currentFileName, false);
                    streamWriter.WriteLine(text);
                }
            }
            catch (Exception ex)
            {
                CountdownOutput = ex.Message;
            }
        }
    }
}
