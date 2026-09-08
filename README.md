# ManWin Performance Overlay

ManWin is a Windows performance overlay inspired by MangoHud. It displays useful hardware and frame performance information while you play, without requiring you to leave the game.

## Features

- Transparent, click-through overlay that stays above the game.
- Manual game or application selection.
- CPU usage and temperature.
- GPU usage and temperature.
- Physical system RAM usage and total memory.
- FPS and frametime measured through Windows ETW presentation events.
- Horizontal or vertical overlay layout.
- Material Design 3-inspired configuration interface with Spanish and English support.
- Hardware monitoring through LibreHardwareMonitor.
- Optional PawnIO installation for sensors that require low-level access.

## Screenshots

### Horizontal overlay

![ManWin horizontal overlay](docs/screenshots/overlay-horizontal.png)

### Vertical overlay

![ManWin vertical overlay](docs/screenshots/overlay-vertical.png)

## Requirements

- Windows 10 or later.
- Run `ManWin.exe` as administrator.
- Windowed or borderless games are recommended for the best overlay compatibility.

## Usage

1. Download the package from the **Releases** page.
2. Extract all files to a folder.
3. Run `ManWin.exe` as administrator.
4. Select the target game from the Dashboard and press the link button.
5. Open **OSD Design** to enable metrics and choose the overlay layout.

PawnIO is optional. If the CPU temperature is unavailable, ManWin can offer to install it after showing a warning about its permissions and possible anti-cheat compatibility issues.

## Technologies

- **C# and .NET 9 WPF** for the Windows application.
- **ETW / Microsoft-Windows-DxgKrnl** for presentation events and FPS/frametime calculation.
- **LibreHardwareMonitor** for CPU, GPU and memory metrics.
- **PawnIO** as an optional low-level sensor provider.
- **WebView2** for the configuration interface.
- **Beer CSS / Material Design** as the visual foundation of the interface.

## Distribution

This repository publishes the compiled end-user package in `release/` and as a ZIP attachment on each GitHub release. The public distribution does not include the C# source code.

## Support the project

If ManWin is useful to you, you can support its development on [Buy Me a Coffee](https://buymeacoffee.com/strelok_dev_8612).

## Licenses

ManWin uses third-party components, including LibreHardwareMonitor, Microsoft WebView2 and Beer CSS. Please review their respective licenses and notices before redistributing the package.
