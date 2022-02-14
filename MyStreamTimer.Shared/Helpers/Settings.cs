using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MyStreamTimer.Shared.Interfaces;
using Plugin.Settings;
using Plugin.Settings.Abstractions;

// msgpacket subscribe via Twitch PRIME! May 15th 2020

namespace MyStreamTimer.Shared.Helpers
{

    public static class GlobalSettings
    {
        public static DateTime AddSubTime(this DateTime dateTime, int months = 1)
            => dateTime.AddMonths(months).AddDays(5);

        static string defaultDirectoryPath;
        const string directoryPathKey = "global_directory_path";
        static bool defaultStayOn = true;

        static GlobalSettings()
        {
            var platform = ServiceContainer.Resolve<IPlatformHelpers>();
            defaultDirectoryPath = Path.Combine(platform.BaseDirectory, "MyStreamTimer");

            if (platform.IsMac)
                defaultStayOn = false;
        }
        static ISettings AppSettings => CrossSettings.Current;
        public static string DirectoryPath
        {
            get => AppSettings.GetValueOrDefault(directoryPathKey, defaultDirectoryPath);
            set => AppSettings.AddOrUpdateValue(directoryPathKey, value);
        }

        public static int TimesUsed
        {
            get => AppSettings.GetValueOrDefault(nameof(TimesUsed), 0);
            set => AppSettings.AddOrUpdateValue(nameof(TimesUsed), value);
        }

        public static bool IsBronze
        {
            get => AppSettings.GetValueOrDefault(nameof(IsBronze), false);
            set => AppSettings.AddOrUpdateValue(nameof(IsBronze), value);
        }

        public static bool IsSilver
        {
            get => AppSettings.GetValueOrDefault(nameof(IsSilver), false);
            set => AppSettings.AddOrUpdateValue(nameof(IsSilver), value);
        }

        public static bool IsGold
        {
            get => AppSettings.GetValueOrDefault(nameof(IsGold), false);
            set => AppSettings.AddOrUpdateValue(nameof(IsGold), value);
        }

        const bool checkSubStatus = true;
        public static bool CheckSubStatus
        {
            get => AppSettings.GetValueOrDefault(nameof(CheckSubStatus), checkSubStatus);
            set => AppSettings.AddOrUpdateValue(nameof(CheckSubStatus), value);
        }

        public static string SubPrice
        {
            get => AppSettings.GetValueOrDefault(nameof(SubPrice), string.Empty);
            set => AppSettings.AddOrUpdateValue(nameof(SubPrice), value);
        }

        public static string SubPrice6Months
        {
            get => AppSettings.GetValueOrDefault(nameof(SubPrice6Months), string.Empty);
            set => AppSettings.AddOrUpdateValue(nameof(SubPrice6Months), value);
        }

        const bool hasTippedSub = false;
        public static bool HasTippedSub
        {
            get => AppSettings.GetValueOrDefault(nameof(HasTippedSub), hasTippedSub);
            set => AppSettings.AddOrUpdateValue(nameof(HasTippedSub), value);
        }

        const bool showSupportPopUp = true;
        public static bool ShowSupportPopUp
        {
            get => AppSettings.GetValueOrDefault(nameof(ShowSupportPopUp), showSupportPopUp);
            set => AppSettings.AddOrUpdateValue(nameof(ShowSupportPopUp), value);
        }

        public static DateTime SubExpirationDate
        {
            get => AppSettings.GetValueOrDefault(nameof(SubExpirationDate), DateTime.UtcNow.AddDays(-1));
            set => AppSettings.AddOrUpdateValue(nameof(SubExpirationDate), value);
        }

        public static bool IsSubValid => SubExpirationDate > DateTime.UtcNow;

        //#if DEBUG
        //        public static bool IsPro => false;
        //#else
        public static bool IsPro => IsBronze || IsSilver || IsGold || (HasTippedSub && IsSubValid);
//#endif

        public static string ProPrice
        {
            get => AppSettings.GetValueOrDefault(nameof(ProPrice), string.Empty);
            set => AppSettings.AddOrUpdateValue(nameof(ProPrice), value);
        }

        public static DateTime ProPriceDate
        {
            get => AppSettings.GetValueOrDefault(nameof(ProPriceDate), DateTime.UtcNow);
            set => AppSettings.AddOrUpdateValue(nameof(ProPriceDate), value);
        }

        public static bool StayOnTop
        {
            get => AppSettings.GetValueOrDefault(nameof(StayOnTop), defaultStayOn);
            set => AppSettings.AddOrUpdateValue(nameof(StayOnTop), value);
        }

        public static bool FirstRun
        {
            get => AppSettings.GetValueOrDefault(nameof(FirstRun), true);
            set => AppSettings.AddOrUpdateValue(nameof(FirstRun), value);
        }
    }
    public class Settings
    {
        readonly string id;
        public Settings(string id)
        {
            this.id = id;
            fileNameDefault = $"{id}.txt";
            switch(id)
            {
                case Constants.Countdown2:
                case Constants.Countdown3:
                case Constants.Countdown4:
                case Constants.Countdown:
                    minutesDefault = 5;
                    break;
                case Constants.Countup:
                case Constants.Countup2:
                    outputDefault = @"{0:hh\:mm\:ss}";
                    minutesDefault = 0;
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

        const string secondsKey = "key_seconds";
        readonly int secondsDefault = 0;

        const string outputKey = "key_output";
        readonly string outputDefault = @"Starting in {0:hh\:mm\:ss}";

        const string finishKey = "key_finish";
        readonly string finishDefault = @"Let's do this!";

        const string fileNameKey = "key_file_name";
        readonly string fileNameDefault = @"countdown.txt";


        const string autoStartKey = "key_auto_start";
        readonly bool autoStartDefault = false;
        const string makeSoundKey = "make_sound";
        readonly bool makeSoundDefault = false;


        const string outputStyleKey = "key_output_style";
        const int outputStyleDefault = 0;

        #endregion

        public TimeSpan FinishAtTime
        {
            get
            {
                var defaultTicks = (long)-1.0;
                var ticks = AppSettings.GetValueOrDefault($"{nameof(FinishAtTime)}_{id}", defaultTicks);

                if (ticks == -1)
                {
                    var ts = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute + 15, 0);
                    return ts;
                }

                return new TimeSpan(ticks);
            }
            set
            {
                var ticks = value.Ticks;
                AppSettings.AddOrUpdateValue($"{nameof(FinishAtTime)}_{id}", ticks);
            }
        }

        public bool UseMinutes
        {
            get => AppSettings.GetValueOrDefault($"{nameof(UseMinutes)}_{id}", true);
            set => AppSettings.AddOrUpdateValue($"{nameof(UseMinutes)}_{id}", value);
        }

        public bool MakeSound
        {
            get => AppSettings.GetValueOrDefault($"{makeSoundKey}_{id}", makeSoundDefault);
            set => AppSettings.AddOrUpdateValue($"{makeSoundKey}_{id}", value);
        }

        public bool AutoStart
        {
            get => AppSettings.GetValueOrDefault($"{autoStartKey}_{id}", autoStartDefault);
            set => AppSettings.AddOrUpdateValue($"{autoStartKey}_{id}", value);
        }

        public int OutputStyle
        {
            get => AppSettings.GetValueOrDefault($"{outputStyleKey}_{id}", outputStyleDefault);
            set => AppSettings.AddOrUpdateValue($"{outputStyleKey}_{id}", value);
        }

        public int Seconds
        {
            get => AppSettings.GetValueOrDefault($"{secondsKey}_{id}", secondsDefault);
            set => AppSettings.AddOrUpdateValue($"{secondsKey}_{id}", value);
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
