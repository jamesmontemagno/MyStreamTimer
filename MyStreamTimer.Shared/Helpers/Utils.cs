using System;
namespace MyStreamTimer.Shared.Helpers
{
    public static class Utils
    {
        public static (bool, float, string) ParseStartupArgs(string args)
        {
            var start = false;
            float mins = -1;
            var host = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    var uri = new Uri(args);
                    host = uri.Host.ToLower();
                    var query = uri.Query.ToLower();

                    if (host != "countdown" && host != "countdown1" && host != "countdown2" && host != "countdown3" && host != "countdown4")
                        return (start, mins, string.Empty);

                    if (query.Contains("?mins=") &&
                        float.TryParse(query.Remove(0, 6), out mins))
                    {
                        start = true;
                    }
                    else if(query.Contains("?secs=") &&
                        float.TryParse(query.Remove(0, 6), out var secs))
                    {
                        mins = secs / 60.0f;
                        start = true;
                    }
                    else if(query.Contains("?topofhour"))
                    {
                        mins = 60.0f - (float)DateTime.Now.Minute;
                        mins += (60.0f - (float)DateTime.Now.Second) / 60.0f;
                        mins -= 1;
                        if (mins < 0)
                            mins = 0;

                        start = true;
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
                        start = true;
                    }
                    
                }
            }
            catch (Exception)
            {

            }

            return (start, mins, host);
        }
    }
}
