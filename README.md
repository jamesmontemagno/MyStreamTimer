# My Stream Timer
My Stream Timer is an easy to use countdown and count-up timer for streamers. Multiple timers are available that write a file to disk to use with OBS, SLOBS, or your favorite streaming application. Have it auto start so it works with Stream Deck!

![](Art/demo.png)


## Integrating into OBS/SLOBS

Open My Stream Timer and tap the copy icon to copy the location on disk where My Stream Timer saves output files.

![](Art/CopyLocation.png)

Next, Open OBS/SLOBS and add a **Text** source. Check "Read from file" and click browse and navigate location that was copied to the clipboard. Select on of the text files for count down, up, or giveaway. That's it! When you start the countdown it will show up!

![](Art/SelectFromFile.png)

## Integrating into Stream Deck

You can integrate a **Open** command under **System** to launch My Stream Timer and start a countdown from a specific amoutn of time. You don't need to browse for a file location at all as you can input a protocol url:

Format: mystreamtimer://countdown/?mins=6

## In Action

## Development 
Currently consists of a WPF app written with [.NET Core 3-preview](https://devblogs.microsoft.com/dotnet/announcing-net-core-3-preview-3/) and VS 2019. Contains all logic in a .NET Standard Library so we can port it over to macOS.

