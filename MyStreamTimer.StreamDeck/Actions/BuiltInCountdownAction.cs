using MyStreamTimer.StreamDeck.Helpers;
using StreamDeckLib;
using StreamDeckLib.Messages;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace MyStreamTimer.StreamDeck
{
    [ActionUuid(Uuid = "com.refractored.mystreamtimer.action.builtincountdown")]
    public class BuiltInCountdownAction : BaseStreamDeckActionWithSettingsModel<Models.BuiltInCountdownSettingsModel>
    {
        DateTime startTime;
        DateTime endTime;
        bool isBusy;
        string countdownOutput;
        CancellationTokenSource timerCTS;
        StreamDeckEventPayload currentArgs;
        public override async Task OnKeyUp(StreamDeckEventPayload args)
        {
            currentArgs = args;
            await ExecuteStartStopTimerCommand();

            await Manager.ShowOkAsync(args.context);
        }
        string path;
        async Task ExecuteStartStopTimerCommand()
        {
            try
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                path = Path.Combine(path, "MyStreamTimerStreamDeck");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                path = Path.Combine(path, SettingsModel.FileName);

                await WriteTimeToDisk(true, string.Empty);
            }
            catch (Exception ex)
            {
                await Manager.ShowAlertAsync(currentArgs.context);
                return;
            }

            //if pausable...

            if (isBusy)
            {
                timerCTS?.Cancel();
            }

            isBusy = !isBusy;


            if (!isBusy)
                return;


            var currentSeconds = SettingsModel.Seconds;
            var currentMinutes = SettingsModel.Minutes;


            startTime = DateTime.Now;
            endTime = DateTime.Now.AddMinutes(currentMinutes).AddSeconds(currentSeconds);

            timerCTS = new CancellationTokenSource();
            await Task.Factory.StartNew(UpdateTimer, TaskCreationOptions.LongRunning, timerCTS.Token);
        }

        async void UpdateTimer(object thing)
        {
            while (!timerCTS.IsCancellationRequested)
            {
                var now = DateTime.Now;
                string text;
 
                if (now >= endTime)
                {
                    text = string.Empty;
                    await ExecuteStartStopTimerCommand();
                    countdownOutput = text;
                    await WriteTimeToDisk(false, text);
                    return;
                }
                else
                {
                    var elapsedTime = endTime - DateTime.Now;
                    string format;
                    if (elapsedTime.TotalMinutes > 600)
                        format = @"{0:hh\:mm\:ss}";
                    else if (elapsedTime.TotalMinutes >= 60)
                        format = @"{0:h\:mm\:ss}";
                    else if(elapsedTime.TotalMinutes >= 10)
                        format = @"{0:mm\:ss}";
                    else
                        format = @"{0:m\:ss}";

                    text = string.Format(format, elapsedTime);
                }

                if (text != countdownOutput)
                {
                    await WriteTimeToDisk(false, text);
                    countdownOutput = text;
                }
                await Task.Delay(500);
            }
        }

        async Task WriteTimeToDisk(bool create, string text)
        {
            try
            {
                if (create)
                    File.WriteAllText(path, text);
                else
                {
                    using var streamWriter = new StreamWriter(path, false);
                    streamWriter.WriteLine(text);
                }

                await Manager.SetTitleAsync(currentArgs.context, text);

            }
            catch (Exception ex)
            {
                //error
            }
        }

        public override async Task OnDidReceiveSettings(StreamDeckEventPayload args)
        {
            await base.OnDidReceiveSettings(args);
            await Manager.SetTitleAsync(args.context, $"{SettingsModel.Minutes} mins");

        }
    }
}
