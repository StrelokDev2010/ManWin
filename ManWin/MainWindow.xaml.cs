using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using Forms = System.Windows.Forms;
using ManWin.Overlay;
using ManWin.Sensors;
using Microsoft.Web.WebView2.Core;

namespace ManWin;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string DefaultOsdBackgroundColor = "#10121A";
    private const string DefaultOsdTextColor = "#FFFFFF";
    private readonly PeriodicTimer _sensorTimer = new(TimeSpan.FromSeconds(1));
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ManWin", "settings.json");
    private readonly OpenHardwareMonitorSensorProvider _sensorProvider = new();
    private readonly FpsCaptureProvider _fpsCaptureProvider = new();
    private readonly CancellationTokenSource _sensorCancellation = new();
    private Task? _sensorPollingTask;
    private string? _sensorInitializationError;
    private OverlayWindow? _overlay;
    private string[] _selectedMetrics = [];
    private int _osdOpacityPercent = 70;
    private string _osdBackgroundColorHex = DefaultOsdBackgroundColor;
    private string _osdCpuTextColorHex = DefaultOsdTextColor;
    private string _osdGpuTextColorHex = DefaultOsdTextColor;
    private string _osdRamTextColorHex = DefaultOsdTextColor;
    private string _osdLayoutMode = "vertical";
    private IReadOnlyList<SensorReading> _latestReadings = [];
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "mango_ico.ico")));
        _trayIcon = CreateTrayIcon();
        Loaded += OnLoaded;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
        LoadSettings();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        WebView.Source = new Uri(page);

        ApplyOverlayMetrics();
        try
        {
            _sensorProvider.Open();
            _sensorPollingTask = PublishSensorsAsync(_sensorCancellation.Token);
        }
        catch (Exception ex)
        {
            _sensorInitializationError = ex.Message;
            _overlay?.SetSensorStatus($"Sensor initialization failed: {ex.Message}");
            _sensorPollingTask = PublishSensorsAsync(_sensorCancellation.Token);
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _sensorCancellation.Cancel();
        if (_sensorPollingTask is not null)
        {
            try { await _sensorPollingTask; }
            catch (OperationCanceledException) { }
        }
        _sensorProvider.Dispose();
        _fpsCaptureProvider.Dispose();
        _overlay?.Close();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon.Icon?.Dispose();
        _sensorCancellation.Dispose();
        _sensorTimer.Dispose();
        if (_exitRequested) System.Windows.Application.Current.Shutdown();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var document = JsonDocument.Parse(e.TryGetWebMessageAsString());
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeElement)) return;

        switch (typeElement.GetString())
        {
            case "ready":
                SendSavedSettings();
                break;
            case "save":
                SaveSettings(root);
                WebView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"saved\"}");
                break;
            case "osdOpacityChanged":
                SetOsdOpacity(root);
                break;
            case "osdBackgroundColorChanged":
                SetOsdBackgroundColor(root);
                break;
            case "osdSectionTextColorChanged":
                SetOsdSectionTextColor(root);
                break;
            case "osdLayoutChanged":
                SetOsdLayout(root);
                break;
            case "close":
                HideToTray();
                break;
            case "minimize":
                HideToTray();
                break;
            case "beginDrag":
                try { DragMove(); }
                catch (InvalidOperationException) { /* The pointer was released before the drag began. */ }
                break;
        }
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Metrics", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Exit ManWin", null, (_, _) => ExitApplication());

        var icon = new Forms.NotifyIcon
        {
            Text = "ManWin — OSD is running",
            Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "mango_ico.ico")),
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => RestoreFromTray();
        return icon;
    }

    private void HideToTray()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) HideToTray();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        _trayIcon.Visible = false;
        Close();
    }

    private void SaveSettings(JsonElement message)
    {
        if (!message.TryGetProperty("metrics", out var metrics) || metrics.ValueKind != JsonValueKind.Array)
            return;

        var selectedMetrics = metrics.Deserialize<string[]>(JsonOptions);
        if (selectedMetrics is null) return;
        _selectedMetrics = selectedMetrics.Distinct(StringComparer.Ordinal).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        if (message.TryGetProperty("opacityPercent", out var opacity) && opacity.TryGetInt32(out var opacityPercent))
            _osdOpacityPercent = Math.Clamp(opacityPercent, 20, 100);
        if (message.TryGetProperty("backgroundColorHex", out var color) && color.ValueKind == JsonValueKind.String)
            _osdBackgroundColorHex = NormalizeBackgroundColor(color.GetString());
        _osdCpuTextColorHex = GetColorFromMessage(message, "cpuTextColorHex", _osdCpuTextColorHex);
        _osdGpuTextColorHex = GetColorFromMessage(message, "gpuTextColorHex", _osdGpuTextColorHex);
        _osdRamTextColorHex = GetColorFromMessage(message, "ramTextColorHex", _osdRamTextColorHex);
        if (message.TryGetProperty("layoutMode", out var layout) && layout.ValueKind == JsonValueKind.String)
            _osdLayoutMode = NormalizeLayoutMode(layout.GetString());
        SaveSettingsFile();
        ApplyOverlayMetrics();
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(_settingsPath), JsonOptions);
            _selectedMetrics = settings?.Metrics ?? [];
            _osdOpacityPercent = Math.Clamp(settings?.OpacityPercent ?? 70, 20, 100);
            _osdBackgroundColorHex = NormalizeBackgroundColor(settings?.BackgroundColorHex);
            _osdCpuTextColorHex = NormalizeTextColor(settings?.CpuTextColorHex);
            _osdGpuTextColorHex = NormalizeTextColor(settings?.GpuTextColorHex);
            _osdRamTextColorHex = NormalizeTextColor(settings?.RamTextColorHex);
            _osdLayoutMode = NormalizeLayoutMode(settings?.LayoutMode);
        }
        catch (JsonException)
        {
            _selectedMetrics = [];
            _osdOpacityPercent = 70;
            _osdBackgroundColorHex = DefaultOsdBackgroundColor;
            _osdCpuTextColorHex = DefaultOsdTextColor;
            _osdGpuTextColorHex = DefaultOsdTextColor;
            _osdRamTextColorHex = DefaultOsdTextColor;
            _osdLayoutMode = "vertical";
        }
    }

    private void SetOsdOpacity(JsonElement message)
    {
        if (!message.TryGetProperty("opacityPercent", out var opacity) || !opacity.TryGetInt32(out var opacityPercent))
            return;

        _osdOpacityPercent = Math.Clamp(opacityPercent, 20, 100);
        _overlay?.SetBackgroundOpacity(_osdOpacityPercent);
    }

    private void SetOsdBackgroundColor(JsonElement message)
    {
        if (!message.TryGetProperty("backgroundColorHex", out var color) || color.ValueKind != JsonValueKind.String)
            return;

        _osdBackgroundColorHex = NormalizeBackgroundColor(color.GetString());
        _overlay?.SetBackgroundColor(_osdBackgroundColorHex);
    }

    private void SetOsdSectionTextColor(JsonElement message)
    {
        if (!message.TryGetProperty("section", out var sectionElement) || sectionElement.ValueKind != JsonValueKind.String)
            return;
        var section = sectionElement.GetString();
        if (!message.TryGetProperty("colorHex", out var colorElement) || colorElement.ValueKind != JsonValueKind.String)
            return;

        var color = NormalizeTextColor(colorElement.GetString());
        switch (section)
        {
            case "cpu": _osdCpuTextColorHex = color; break;
            case "gpu": _osdGpuTextColorHex = color; break;
            case "ram": _osdRamTextColorHex = color; break;
            default: return;
        }
        _overlay?.SetSectionTextColor(section, color);
    }

    private void SetOsdLayout(JsonElement message)
    {
        if (!message.TryGetProperty("layoutMode", out var layout) || layout.ValueKind != JsonValueKind.String)
            return;
        _osdLayoutMode = NormalizeLayoutMode(layout.GetString());
        _overlay?.SetLayoutMode(_osdLayoutMode);
    }

    private static string GetColorFromMessage(JsonElement message, string propertyName, string fallback) =>
        message.TryGetProperty(propertyName, out var color) && color.ValueKind == JsonValueKind.String
            ? NormalizeTextColor(color.GetString())
            : fallback;

    private void SaveSettingsFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(
            new SavedSettings(_selectedMetrics, _osdOpacityPercent, _osdBackgroundColorHex,
                _osdCpuTextColorHex, _osdGpuTextColorHex, _osdRamTextColorHex, _osdLayoutMode), JsonOptions));
    }

    private static string NormalizeBackgroundColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' &&
        int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? color.ToUpperInvariant()
            : DefaultOsdBackgroundColor;

    private static string NormalizeTextColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' &&
        int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? color.ToUpperInvariant()
            : DefaultOsdTextColor;

    private static string NormalizeLayoutMode(string? mode) =>
        string.Equals(mode, "horizontal", StringComparison.OrdinalIgnoreCase) ? "horizontal" : "vertical";

    private void ApplyOverlayMetrics()
    {
        if (_selectedMetrics.Contains("fps", StringComparer.Ordinal))
        {
            try
            {
                _fpsCaptureProvider.Start();
            }
            catch (Exception ex)
            {
                _fpsCaptureError = $"FPS capture failed: {ex.Message}";
            }
        }
        else
        {
            _fpsCaptureError = null;
            _fpsCaptureProvider.Dispose();
        }

        if (_selectedMetrics.Length == 0)
        {
            _overlay?.Hide();
            return;
        }

        _overlay ??= new OverlayWindow();
        _overlay.SetBackgroundOpacity(_osdOpacityPercent);
        _overlay.SetBackgroundColor(_osdBackgroundColorHex);
        _overlay.SetSectionTextColor("cpu", _osdCpuTextColorHex);
        _overlay.SetSectionTextColor("gpu", _osdGpuTextColorHex);
        _overlay.SetSectionTextColor("ram", _osdRamTextColorHex);
        _overlay.SetLayoutMode(_osdLayoutMode);
        _overlay.SetMetrics(_selectedMetrics);
        _overlay.RefreshRows(_latestReadings);
        if (!_overlay.IsVisible) _overlay.Show();
    }

    private void SendSavedSettings()
    {
        if (_sensorInitializationError is not null)
        {
            var errorJson = JsonSerializer.Serialize(new { type = "sensorError", message = _sensorInitializationError }, JsonOptions);
            WebView.CoreWebView2.PostWebMessageAsJson(errorJson);
        }
        var json = JsonSerializer.Serialize(
            new { type = "load", metrics = _selectedMetrics, opacityPercent = _osdOpacityPercent, backgroundColorHex = _osdBackgroundColorHex,
                cpuTextColorHex = _osdCpuTextColorHex, gpuTextColorHex = _osdGpuTextColorHex, ramTextColorHex = _osdRamTextColorHex,
                layoutMode = _osdLayoutMode },
            JsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private async Task PublishSensorsAsync(CancellationToken cancellationToken)
    {
        while (await _sensorTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var readings = _sensorInitializationError is null
                    ? await Task.Run(_sensorProvider.Read, cancellationToken)
                    : _latestReadings.Where(r => r.Category != "FPS").ToArray();
                if (_selectedMetrics.Contains("fps", StringComparer.Ordinal))
                {
                    var fps = _fpsCaptureError is null
                        ? _fpsCaptureProvider.Read()
                        : new SensorReading("fps", "FPS", 0, "FPS", "FPS", "FrameRate", _fpsCaptureError);
                    readings = readings.Where(r => r.Category != "FPS").Append(fps).ToArray();
                }
                _latestReadings = readings;
                var json = JsonSerializer.Serialize(new { type = "sensorUpdate", readings }, JsonOptions);
                await Dispatcher.InvokeAsync(() =>
                {
                    _overlay?.SetSensorStatus(readings.Count == 0 ? "No sensor values returned by OpenHardwareMonitor" : null);
                    if (WebView.CoreWebView2 is not null) WebView.CoreWebView2.PostWebMessageAsJson(json);
                    _overlay?.RefreshRows(readings);
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var json = JsonSerializer.Serialize(new { type = "sensorError", message = ex.Message }, JsonOptions);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (WebView.CoreWebView2 is not null) WebView.CoreWebView2.PostWebMessageAsJson(json);
                    _overlay?.SetSensorStatus($"Sensor read failed: {ex.Message}");
                });
                break;
            }
        }
    }

    private string? _fpsCaptureError;

    private sealed record SavedSettings(string[] Metrics, int? OpacityPercent = null, string? BackgroundColorHex = null,
        string? CpuTextColorHex = null, string? GpuTextColorHex = null, string? RamTextColorHex = null, string? LayoutMode = null);
}
