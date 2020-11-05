using MyStreamTimer.StreamDeck.Helpers;
using StreamDeckLib;
using StreamDeckLib.Messages;
using System;
using System.Threading.Tasks;

namespace MyStreamTimer.StreamDeck
{
    [ActionUuid(Uuid = "com.refractored.mystreamtimer.action.topofhour")]
    public class TopOfHourAction : BaseStreamDeckAction
    {
        // AdenEarnshaw cheered 44 cpayette 9/18/2020
        public override async Task OnKeyUp(StreamDeckEventPayload args)
        {
            try
            {
                //await Manager.OpenUrlAsync(args.context, "http://mystreamtimer.com");

                var url = @"mystreamtimer://countdown/?topofhour";

                //url = HttpUtility.UrlEncode(url);

                StreamDeckUtils.OpenUrl(url);
                await Manager.ShowOkAsync(args.context);
            }
            catch (Exception ex)
            {

                await Manager.ShowAlertAsync(args.context);

            }          
        }

        

        public override async Task OnDidReceiveSettings(StreamDeckEventPayload args)
        {
            await base.OnDidReceiveSettings(args);
        }

        public override async Task OnWillAppear(StreamDeckEventPayload args)
        {
            await base.OnWillAppear(args);
        }

    }
}
