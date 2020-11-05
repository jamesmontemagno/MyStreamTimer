using System;
using System.Collections.Generic;
using System.Text;

namespace MyStreamTimer.StreamDeck.Models
{
    public class BuiltInCountdownSettingsModel
    {
        public int Minutes { get; set; } = 5;
        public int Seconds { get; set; } = 0;
        public string FileName { get; set; } = "countdown.txt";
    }
}
