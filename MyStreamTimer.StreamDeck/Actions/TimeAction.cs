using MyStreamTimer.StreamDeck.Helpers;
using StreamDeckLib;
using StreamDeckLib.Messages;
using System;
using System.Threading.Tasks;

namespace MyStreamTimer.StreamDeck
{
    [ActionUuid(Uuid = "com.refractored.mystreamtimer.action.time")]
    public class TimeAction : BaseStreamDeckActionWithSettingsModel<Models.CountdownSettingsModel>
    {
        public override async Task OnKeyUp(StreamDeckEventPayload args)
        {
            try
            {
                //await Manager.OpenUrlAsync(args.context, "http://mystreamtimer.com");

                var url = @$"mystreamtimer://countdown/?to={SettingsModel.Time}";

                //url = HttpUtility.UrlEncode(url);

                await Manager.OpenUrlAsync(args.context,$"https://{url}");

                StreamDeckUtils.OpenUrl(url);
                await Manager.ShowOkAsync(args.context);
            }
            catch (Exception ex)
            {

                await Manager.ShowAlertAsync(args.context);

            }
            /*SettingsModel.Counter++;
            await Manager.SetTitleAsync(args.context, SettingsModel.Counter.ToString());

            if (SettingsModel.Counter % 10 == 0)
            {
                await Manager.ShowAlertAsync(args.context);
            }
            else if (SettingsModel.Counter % 15 == 0)
            {
                await Manager.OpenUrlAsync(args.context, "https://www.bing.com");
            }
            else if (SettingsModel.Counter % 3 == 0)
            {
                await Manager.ShowOkAsync(args.context);
            }
            else if (SettingsModel.Counter % 7 == 0)
            {
                await Manager.SetImageAsync(args.context, "images/Fritz.png");
            }

            //update settings
            await Manager.SetSettingsAsync(args.context, SettingsModel);*/
        }

        public override async Task OnDidReceiveSettings(StreamDeckEventPayload args)
        {
            await base.OnDidReceiveSettings(args);
            await Manager.SetTitleAsync(args.context, SettingsModel.Time.ToString());

        }
    }
}
