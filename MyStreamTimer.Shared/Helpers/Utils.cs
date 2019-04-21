using System;
namespace MyStreamTimer.Shared.Helpers
{
    public static class Utils
    {
        public static (bool, int) ParseStartupArgs(string args)
        {
            var start = false;
            var mins = -1;
            try
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    var uri = new Uri(args);
                    if (uri.Host.ToLower() == "countdown" && uri.Query.ToLower().Contains("?mins=") && int.TryParse(uri.Query.Remove(0, 6), out mins))
                    {
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
