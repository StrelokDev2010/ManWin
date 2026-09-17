# ManWin

<a href="https://buymeacoffee.com/strelok_dev_8612"><img src="bmc.png" alt="Buy me a coffee" width="150"></a>
<a href="https://www.patreon.com/cw/Strelok8612"><img src="patreon.png" alt="Support ManWin on Patreon" width="90"></a>

ManWin is a lightweight Windows hardware-monitoring app with a configurable on-screen display (OSD). Choose the metrics you want to see, then keep the OSD running while the Metrics window is minimized to the notification area.

## Screenshots

The screenshots show ManWin's OSD over a game. FPS capture is now available as an experimental option.

![ManWin OSD over a game](docs/screenshots/manwin-overlay-in-game.png)

![Close-up of the ManWin OSD](docs/screenshots/manwin-overlay-close-up.png)

![ManWin OSD in another game scene](docs/screenshots/manwin-overlay-in-game-2.png)

## Features

- Select available CPU, GPU, and memory metrics.
- Show selected readings in a transparent, click-through OSD.
- Customize OSD opacity, background color, per-section text colors, and vertical or horizontal layout.
- Separate statistics with line breaks in vertical mode or `|` in horizontal mode.
- Minimize the Metrics window to the Windows notification area while the OSD and sensor polling continue.
- Restore Metrics by double-clicking the notification-area icon or choosing **Open Metrics**.
- Save metric selections between runs.
- Experimentally count DXGI or D3D9 present events from the foreground app as an FPS estimate.

Available hardware readings depend on the sensors exposed by OpenHardwareMonitorLib. ManWin polls hardware sensors once per second. The experimental FPS counter listens for DXGI or D3D9 ETW present events from the foreground app; it is not the full PresentMon analysis pipeline and may not work with every graphics API or game. It counts submitted presents, which can differ from frames actually displayed.

## Requirements

- Windows 10 or Windows 11, x64.
- Microsoft Edge WebView2 Evergreen Runtime. It is commonly installed on Windows 11. If it is missing, install it from the [official WebView2 page](https://developer.microsoft.com/microsoft-edge/webview2/).
- .NET 9 SDK to build and run from source.
- Administrator approval at startup. ManWin requests elevation to create the ETW session used for FPS capture.

### Requirements for the small release download

The small release ZIP does not bundle .NET. Before running ManWin, install the **.NET 9 Desktop Runtime for Windows x64** from the [.NET 9 download page](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (under **Run apps → .NET Desktop Runtime**). The WebView2 Evergreen Runtime is also required. Windows will ask for administrator approval when ManWin starts.

## Build and run from source

Run these commands from the repository root.

### PowerShell

```powershell
dotnet restore .\ManWin\ManWin.csproj
dotnet build .\ManWin.sln
dotnet run --project .\ManWin\ManWin.csproj
```

### Git Bash

Use forward slashes in paths:

```bash
dotnet restore ./ManWin/ManWin.csproj
dotnet build ./ManWin.sln
dotnet run --project ./ManWin/ManWin.csproj
```

You can also open `ManWin.sln` in Visual Studio 2022 with the **.NET desktop development** workload and run the `ManWin` project.

## Create Windows x64 release builds

The current release is available in two ZIP variants:

- `ManWin-win-x64-small.zip` — smaller download; requires the .NET 9 Desktop Runtime and WebView2 Runtime.
- `ManWin-win-x64-portable.zip` — includes the .NET runtime; WebView2 Runtime is still required.

Both variants include the configurable OSD, notification-area minimization, saved settings, and experimental FPS capture. Windows asks for administrator approval at startup because FPS capture uses an ETW session.

### Small download (requires .NET Desktop Runtime)

From PowerShell, run:

```powershell
dotnet publish .\ManWin\ManWin.csproj -c Release -r win-x64 --no-self-contained -o .\artifacts\ManWin-small
Compress-Archive -Path .\artifacts\ManWin-small\* -DestinationPath .\artifacts\ManWin-win-x64-small.zip -Force
```

From Git Bash, use forward slashes for paths:

```bash
dotnet publish ./ManWin/ManWin.csproj -c Release -r win-x64 --no-self-contained -o ./artifacts/ManWin-small
powershell.exe -NoProfile -Command "Compress-Archive -Path './artifacts/ManWin-small/*' -DestinationPath './artifacts/ManWin-win-x64-small.zip' -Force"
```

The small ZIP contains ManWin and its app dependencies, but not .NET. Users must install the .NET Desktop Runtime 9 x64 and WebView2 Evergreen Runtime before launching it.

### Self-contained portable build (does not require installing .NET)

To include .NET in the download, publish with `--self-contained true` instead:

```powershell
dotnet publish .\ManWin\ManWin.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\ManWin-portable
Compress-Archive -Path .\artifacts\ManWin-portable\* -DestinationPath .\artifacts\ManWin-win-x64-portable.zip -Force
```

This larger ZIP includes the .NET runtime. WebView2 Evergreen Runtime is still required. Windows will ask for administrator approval when ManWin starts. The app stores preferences in `%LOCALAPPDATA%\ManWin\settings.json`.

## Usage

1. Toggle the metrics you want to display. **FPS (experimental)** listens to DXGI events from the foreground app and may not be available for every game or graphics API.
2. Select **Save** to store your selections.
3. Minimize the Metrics window or click **X** to hide it in the notification area. The OSD and sensor polling continue running.
4. Double-click the ManWin notification-area icon or select **Open Metrics** to restore the window. Select **Exit ManWin** from the tray menu to close the app and stop the OSD.
5. Drag the top bar to move the Metrics window.

## Project structure

- `ManWin/` — WPF app and WebView2 host.
- `ManWin/Sensors/` — sensor-reading contract, OpenHardwareMonitorLib provider, and ETW-based FPS capture.
- `ManWin/wwwroot/` — HTML, CSS, and JavaScript for the Metrics page.
- `docs/screenshots/` — screenshots shown above.

## Dependencies and notices

The app uses `OpenHardwareMonitorLib` 1.0.9513, `Microsoft.Web.WebView2`, and `Microsoft.Diagnostics.Tracing.TraceEvent`, referenced through NuGet. OpenHardwareMonitorLib is licensed under MPL-2.0 and includes third-party dependencies; see the package and upstream project for applicable notices. ManWin requests administrator privileges at startup to create an ETW session for FPS capture.
