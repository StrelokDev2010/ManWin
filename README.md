# ManWin

## Requisitos

- Windows 10/11 x64
- .NET 9 SDK
- Runtime Evergreen de Microsoft Edge WebView2. Suele estar instalado en Windows 11; si falta en Windows 10, instálalo desde la [página oficial de WebView2](https://developer.microsoft.com/microsoft-edge/webview2/).

## Restaurar, compilar y ejecutar

Desde **PowerShell**:

```powershell
dotnet restore .\ManWin\ManWin.csproj
dotnet build .\ManWin.sln
dotnet run --project .\ManWin\ManWin.csproj
```

Desde **Git Bash** (usa `/` como separador de carpetas):

```bash
dotnet restore ./ManWin/ManWin.csproj
dotnet build ./ManWin.sln
dotnet run --project ./ManWin/ManWin.csproj
```

Para compilar una versión optimizada de Release:

```powershell
dotnet build .\ManWin.sln -c Release
```

Para generar un Release portable en carpeta para Windows x64, que incluye .NET:

```powershell
dotnet publish .\ManWin\ManWin.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\ManWin-portable
```

Distribuye toda la carpeta `artifacts\ManWin-portable`; el usuario no necesita instalar .NET, pero sí debe tener instalado el runtime Evergreen de WebView2. La carpeta incluye el ejecutable, dependencias, icono y recursos HTML/CSS/JavaScript. Las preferencias se guardan en `%LOCALAPPDATA%\ManWin\settings.json`.

También puedes abrir `ManWin.sln` en Visual Studio 2022 con el workload **Desarrollo de escritorio de .NET** y ejecutar el proyecto `ManWin`.

## Prototype usage

1. Toggle the metrics you want to include.
2. Select **Save** to persist the selection under `%LOCALAPPDATA%\ManWin\settings.json`.
3. Use the minimize or **X** button to hide Metrics in the Windows notification area; the OSD and sensor polling keep running. Double-click the tray icon or choose **Open Metrics** to restore the window. Choose **Exit ManWin** from the tray menu to stop the OSD and close the app. Drag the top bar to move the window.

The current screen is a metrics-selection settings page, styled after the supplied reference. Its options reflect readings or metadata exposed by OpenHardwareMonitorLib: CPU/GPU load, clocks, temperatures, power, GPU memory, fans, RAM usage, physical RAM used and total. GPU-specific readings are hardware-dependent. The app initializes OpenHardwareMonitorLib, polls available sensors once per second, and shows selected values in a transparent, click-through OSD.

Display-only controls (load color, core bars/graphs), throttling state, Vulkan driver details, per-process stats and disk I/O throughput were removed from this sensor selection because this library package does not provide them as corresponding readings. They could be added later using app-side presentation logic or separate Windows APIs.

## Estructura

- `ManWin/`: aplicación WPF y host de WebView2.
- `ManWin/Sensors/`: contrato de lectura y proveedor de OpenHardwareMonitorLib.
- `ManWin/wwwroot/`: pantalla HTML/CSS/JavaScript; recibe las lecturas por mensajería WebView2.

The app currently has one Metrics page, with options grouped under GPU, CPU and Other. Sensor readings are passed to the WebView2 page and the OSD. Physical memory capacity values are exposed in GB by OpenHardwareMonitorLib.

## Dependencias y distribución

WebView2 and OpenHardwareMonitorLib are referenced through NuGet. The pinned package is `OpenHardwareMonitorLib` 1.0.9513 (MPL-2.0; includes third-party dependencies/notices). NuGet currently lists 1.0.9513, while the GitHub repository has newer 3.0.x releases; this app follows the repository's NuGet integration instructions, so compare the package with upstream before a public release if newer hardware support is needed. Some low-level sensors may require administrator rights, and upstream notes that hardware-monitoring drivers can trigger antivirus detections. The app remains `asInvoker` and does not automatically request elevation.
