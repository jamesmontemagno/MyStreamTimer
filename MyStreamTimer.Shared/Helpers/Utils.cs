using System;
namespace MyStreamTimer.Shared.Helpers
{
    public static class Utils
    {
        public static (bool, float) ParseStartupArgs(string args)
        {
            var start = false;
            float mins = -1;
            try
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    var uri = new Uri(args);
                    var host = uri.Host.ToLower();
                    var query = uri.Query.ToLower();

                    if (host != "countdown")
                        return (start, mins);

                    if (query.Contains("?mins=") &&
                        float.TryParse(query.Remove(0, 6), out mins))
                    {
                        start = true;
                    }
                    else if(query.Contains("?topofhour"))
                    {
                        mins = 60.0f - (float)DateTime.Now.Minute;
                        mins += (60.0f - (float)DateTime.Now.Second) / 60.0f;
                        start = true;
                    }
                    else if(query.Contains("?to=") &&
                        DateTime.TryParse(query.Remove(0, 4), out var date) &&
                        date.TimeOfDay > DateTime.Now.TimeOfDay)
                    {
                        mins = (float)(date.TimeOfDay.TotalMinutes - DateTime.Now.TimeOfDay.TotalMinutes);
                        start = true;
                    }
                    
                }
            }
            catch (Exception)
            {

            }

            return (start, mins);
        }
    }
}
