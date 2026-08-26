using System;
namespace MyStreamTimer.Shared.Helpers
{
    public static class Utils
    {
        public enum CommandAction
        {
            Start,
            Stop,
            Add,
            Subtract,
            Pause,
            Resume,
            Reset,
            None
        }
        public static (CommandAction, float, string) ParseStartupArgs(string args)
        {
            var action = CommandAction.None;
            float mins = -1;
            var host = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    var uri = new Uri(args);
                    host = uri.Host.ToLower();
                    var query = uri.Query.ToLower();

                    if (host != "countdown" && host != "countdown1" && host != "countdown2" && host != "countdown3" && host != "countdown4" && host != "countup" && host != "countup1" && host != "countup2")
                        return (action, mins, string.Empty);

                    if (query.Contains("?mins=") &&
                        float.TryParse(query.Remove(0, 6), out mins))
                    {
                        if (mins > 0)
                            action = CommandAction.Start;
                    }
                    else if(query.Contains("?secs=") &&
                        float.TryParse(query.Remove(0, 6), out var secs))
                    {
                        mins = secs / 60.0f;
                        if (mins > 0)
                            action = CommandAction.Start;
                    }
                    else if(query.Contains("?topofhour"))
                    {
                        mins = 60.0f - (float)DateTime.Now.Minute;
                        mins += (60.0f - (float)DateTime.Now.Second) / 60.0f;
                        mins -= 1;
                        if (mins < 0)
                            mins = 0;

                        if (mins > 0)
                            action = CommandAction.Start;
                    }
                    else if(query.Contains("?to=") &&
                        DateTime.TryParse(query.Remove(0, 4), out var date))
                    {
                        if (date.TimeOfDay > DateTime.Now.TimeOfDay)
                            mins = (float)(date.TimeOfDay.TotalMinutes - DateTime.Now.TimeOfDay.TotalMinutes);
                        else
                        {
                            //time until midnight
                            mins = (float)(1440.0 - DateTime.Now.TimeOfDay.TotalMinutes);
                            mins += (float)date.TimeOfDay.TotalMinutes;
                        }
                        if (mins > 0)
                            action = CommandAction.Start;
                    }
                    else if (query.Contains("?addmins=") &&
                        float.TryParse(query.Remove(0, 9), out mins))
                    {
                        if (mins > 0)
                            action = CommandAction.Add;
                    }
                    else if (query.Contains("?addsecs=") &&
                        float.TryParse(query.Remove(0, 9), out var addsecs))
                    {
                        mins = addsecs / 60.0f;
                        if (mins > 0)
                            action = CommandAction.Add;
                    }
                    else if (query.Contains("?subtractmins=") &&
                        float.TryParse(query.Remove(0, 14), out mins))
                    {
                        if (mins > 0)
                            action = CommandAction.Subtract;
                    }
                    else if (query.Contains("?subtractsecs=") &&
                        float.TryParse(query.Remove(0, 14), out var addsecs2))
                    {
                        mins = addsecs2 / 60.0f;
                        if(mins > 0)
                            action = CommandAction.Subtract;
                    }
                    else if(query.Contains("?pause"))
                    {
                        action = CommandAction.Pause;
                    }
                    else if(query.Contains("?resume"))
                    {
                        action = CommandAction.Resume;
                    }
                    else if (query.Contains("?reset"))
                    {
                        action = CommandAction.Reset;
                    }
                    else if (query.Contains("?stop"))
                    {
                        action = CommandAction.Stop;
                    }

                }
            }
            catch (Exception)
            {

            }

            return (action, mins, host);
        }
    }
}
