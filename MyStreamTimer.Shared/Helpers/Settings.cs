using System;
using System.Collections.Generic;
using System.Text;
using Plugin.Settings;
using Plugin.Settings.Abstractions;

namespace MyStreamTimer.Shared.Helpers
{
    public class Settings
    {
        readonly string id;
        public Settings(string id)
        {
            this.id = id;
            fileNameDefault = $"{id}.txt";
            switch(id)
            {
                case Constants.Countdown:
                    minutesDefault = 5;
                    break;
                case Constants.Countup:
                    outputDefault = @"{0:hh\:mm\:ss}";
                    break;
                case Constants.Giveaway:
                    minutesDefault = 60;
                    outputDefault = @"Giveaway in {0:hh\:mm\:ss}";
                    break;
            }
        }
        static ISettings AppSettings => CrossSettings.Current;

        #region Setting Constants

        const string minutesKey = "key_minutes";
        readonly int minutesDefault = 5;

        const string outputKey = "key_output";
        readonly string outputDefault = @"Starting in {0:mm\:ss}";

        const string finishKey = "key_finish";
        readonly string finishDefault = @"Let's do this!";

        const string fileNameKey = "key_file_name";
        readonly string fileNameDefault = @"countdown.txt";


        const string autoStartKey = "key_auto_start";
        readonly bool autoStartDefault = false;

        #endregion

        public bool AutoStart
        {
            get => AppSettings.GetValueOrDefault($"{autoStartKey}_{id}", autoStartDefault);
            set => AppSettings.AddOrUpdateValue($"{autoStartKey}_{id}", value);
        }

        public int Minutes
        {
            get => AppSettings.GetValueOrDefault($"{minutesKey}_{id}", minutesDefault);
            set => AppSettings.AddOrUpdateValue($"{minutesKey}_{id}", value);
        }

        public string Output
        {
            get => AppSettings.GetValueOrDefault($"{outputKey}_{id}", outputDefault);
            set => AppSettings.AddOrUpdateValue($"{outputKey}_{id}", value);
        }

        public string Finish
        {
            get => AppSettings.GetValueOrDefault($"{finishKey}_{id}", finishDefault);
            set => AppSettings.AddOrUpdateValue($"{finishKey}_{id}", value);
        }

        public string FileName
        {
            get => AppSettings.GetValueOrDefault($"{fileNameKey}_{id}", fileNameDefault);
            set => AppSettings.AddOrUpdateValue($"{fileNameKey}_{id}", value);
        }

    }
}
