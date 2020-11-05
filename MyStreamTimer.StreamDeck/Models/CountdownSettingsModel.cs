using System;
using System.Collections.Generic;
using System.Text;

namespace MyStreamTimer.StreamDeck.Models
{
    public class CountdownSettingsModel
    {
        public int Minutes { get; set; } = 5;
        public string Time { get; set; } = "12:00";
    }
}
