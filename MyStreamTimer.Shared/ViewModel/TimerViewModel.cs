﻿using MvvmHelpers;
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
                return platformHelpers.DisplayAlert("Path Copied", $"Path to file is located at {directory}, use Command + Shift + G to bring up directory selection in Finder. The main folder may be name MyStreamTimer and not com.refractored.mystreamtimer in Finder.");
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
                    if (FinishAtTime <= DateTime.Now.TimeOfDay)
                    {
                        CountdownOutput = "Select time in future";
                        return;
                    }

                    currentMinutes = (float)(FinishAtTime.TotalMinutes - DateTime.Now.TimeOfDay.TotalMinutes);
                }
            }
            currentOutput = Output;

            startTime = DateTime.Now;
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
                string text;
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
                        text = string.Format(currentOutput, elapsedTime);
                    }
                }
                else
                {
                    var elapsedTime = DateTime.Now.AddTicks(extraTicksForUp) - startTime;
                    text = string.Format(currentOutput, elapsedTime);
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
