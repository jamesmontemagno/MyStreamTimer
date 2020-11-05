using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Settings.Configuration;
using StreamDeckLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MyStreamTimer.StreamDeck
{
    class Program
    {


        static async Task Main(string[] args)
        {

            using (var config = StreamDeckLib.Config.ConfigurationBuilder.BuildDefaultConfiguration(args))
            {

                await ConnectionManager.Initialize(args, config.LoggerFactory)
                                                             .RegisterAllActions(typeof(Program).Assembly)
                                                             .StartAsync();

            }

        }

    }
}
