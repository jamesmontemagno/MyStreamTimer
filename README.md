# My Stream Timer
My Stream Timer is an easy to use countdown and count-up timer for streamers. Multiple timers are available that write a file to disk to use with OBS, SLOBS, or your favorite streaming application. Have it auto start so it works with Stream Deck!


Download today on Windows or macOS:
* Windows 10 (1809+) / Windows 11 via the [Microsoft Store](https://www.microsoft.com/p/my-stream-timer/9n5nxx3wk7k7?WT.mc_id=friends-0000-jamont)
* macOS via the [App Store](https://itunes.apple.com/us/app/my-stream-timer/id1460539461?mt=12)

![](Art/demo.png)

The Windows app is distributed exclusively through the Microsoft Store (Pro features use Store licensing). Releases are submitted automatically by the [Windows Store Publish](.github/workflows/windows-store-publish.yml) workflow when a `vX.Y.Z-windows` tag is pushed.

## What's new in 3.0 (Windows)

My Stream Timer for Windows was rewritten from the ground up in **WinUI 3 / Windows App SDK** with a modern Fluent design. Everything you had configured before — timer settings, file names, output folder and Pro unlocks — carries over automatically.

* Sidebar navigation, Mica, Light/Dark/System themes
* **Rename timers and pick an icon** for each one (Settings › Timers)
* **Pop‑out timer windows** (Pro) with custom font, size and colours — great on a second monitor or pinned over OBS
* **Automation page** with a command builder that generates `mystreamtimer://` URLs for Stream Deck and scripts
* Output folder management (choose, test access, open in Explorer), per‑timer **+1 / −1 minute**, keyboard shortcuts (Space start/stop, P pause, R reset, Ctrl+Shift+1…7 switch timers)
* Pro **subscriptions** (monthly / 6 months) in addition to the lifetime tiers
* New automation host: `mystreamtimer://time/?start` and `?stop` for the clock timer
* Keeps your PC awake while a timer is running — no more "do not minimize" warning

## Integrating into OBS/SLOBS

Open My Stream Timer and tap the copy icon to copy the location on disk where My Stream Timer saves output files.

![](Art/CopyLocation.png)

Next, Open OBS/SLOBS and add a **Text** source. Check "Read from file" and click browse and navigate location that was copied to the clipboard. Select on of the text files for count down, up, or giveaway. That's it! When you start the countdown it will show up!

![](Art/SelectFromFile.png)

If you are on macOS when you set click "Browse" in OBS/SLOBS the file picker will come up. To browse to a folder use the following command on your keyboard: (CMD + SHIFT + G) and then paste the directory from My Stream Timer

## Integrating into Stream Deck

You can integrate a **Website** command under **System** to launch My Stream Timer and start a countdown from a specific amount of time. You don't need to browse for a file location at all as you can input a protocol url:

* Count down from X minutes: mystreamtimer://countdown/?mins=6
* Count down to specific time (24 hour clock): mystreamtimer://countdown/?to=15:30
* Count down to top of the hour: mystreamtimer://countdown/?topofhour

## Integrating into Command Line

My Stream Timer uses standard protocals to work via the command line. For example you can call the following on the Windows command line:

```
start mystreamtimer://countdown/?mins=6
```

Here are the list of commands:
* mystreamtimer://countdown/?mins=6
* mystreamtimer://countdown/?secs=90
* mystreamtimer://countdown/?topofhour
* mystreamtimer://countdown/?to=15:30
* mystreamtimer://countdown/?addmins=1 · ?addsecs=30 · ?subtractmins=1 · ?subtractsecs=30
* mystreamtimer://countdown/?pause · ?resume · ?reset · ?stop
* mystreamtimer://time/?start · ?stop (clock timer, Pro)

**countdown** can be replaced with: **countdown2**, **countdown3**, **countdown4**, **countup**, **countup2** depending on which one you would like to control. The Automation page in the app builds these URLs for you.

## Integrating into Deckboard (using an Extension App for Windows)
If you do not own a Stream Deck but use other apps to control your stream, [Dara Oladapo](https://twitter.com/daraoladapo) created an extension app for Windows that he uses for Deckboard. You can check out the project [here](https://github.com/DaraOladapo/stream-deckboard) and web link [here](https://daraoladapo.github.io/stream-deckboard/).

## In Action

View the walkthrough on [YouTube](https://youtu.be/j_GdGIdDRxI)

## Building from source (Windows)

Requires the .NET 10 SDK, the [WinApp CLI](https://aka.ms/winapp) and Developer Mode.

```
dotnet test tests\MyStreamTimer.Core.Tests
dotnet build src\MyStreamTimer.WinUI\MyStreamTimer.WinUI.csproj -p:Platform=x64
cd src\MyStreamTimer.WinUI && dotnet run
```

Debug builds install side‑by‑side with the Store app under a `*.Dev` identity (protocol `mystreamtimer-dev://`). Release builds use the Store identity. The migration plan, design spec and upgrade‑test checklist live in `winui-migration/`.

## Troubleshooting

My Stream Timer should work out of the box, but if it doesn't here are some tips and tricks.

### macOS: Files can't be saved
In some instances My Stream Timer may need full file accessed based on your setup (This is rare). Head to **Preferences > Security & Privacy > Full Disk Access** Unlock to add My Stream Timer from your application folder.

![Adding my stream timer to full disk access](macossettings.png)

### macOS: I dont' hear any "beeps"
My Stream Timer uses the native device sound effects. This means you can make the beeps whatever you would like, but you have to turn them on. Head to **Preferences > Sound > Sound Effects**. Ensure that **Play user interface sound effects** is turned on and that it is set to playback through the speaker you would like to use.



