# My Stream Timer
My Stream Timer is an easy to use countdown and count-up timer for streamers. Multiple timers are available that write a file to disk to use with OBS, SLOBS, or your favorite streaming application. Have it auto start so it works with Stream Deck!


Download today on Windows or macOS:
* Windows 10 via the [Microsoft Store](https://www.microsoft.com/en-us/p/my-stream-timer/9n5nxx3wk7k7)
* macOS 10.10+ via the [App Store](https://itunes.apple.com/us/app/my-stream-timer/id1460539461?mt=12)

![](Art/demo.png)


## Integrating into OBS/SLOBS

Open My Stream Timer and tap the copy icon to copy the location on disk where My Stream Timer saves output files.

![](Art/CopyLocation.png)

Next, Open OBS/SLOBS and add a **Text** source. Check "Read from file" and click browse and navigate location that was copied to the clipboard. Select on of the text files for count down, up, or giveaway. That's it! When you start the countdown it will show up!

![](Art/SelectFromFile.png)

## Integrating into Stream Deck

You can integrate a **Open** command under **System** to launch My Stream Timer and start a countdown from a specific amoutn of time. You don't need to browse for a file location at all as you can input a protocol url:

* Count down from X minutes: mystreamtimer://countdown/?mins=6
* Count down to specific time (24 hour clock): mystreamtimer://countdown/?to=15:30
* Count down to top of the hour: mystreamtimer://countdown/?topofhour

## In Action

View the walkthrough on [YouTube](https://youtu.be/j_GdGIdDRxI)

## Development 
Currently consists of a WPF app written with [.NET Core 3-preview](https://devblogs.microsoft.com/dotnet/announcing-net-core-3-preview-3/) and VS 2019 & a macOS app powered by [Xamarin.Forms](http://xamarin.com/forms) with a shared .NET Standard Library.

