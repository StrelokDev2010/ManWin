using System.IO;
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
    private readonly PeriodicTimer _sensorTimer = new(TimeSpan.FromSeconds(1));
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ManWin", "settings.json");
    private readonly OpenHardwareMonitorSensorProvider _sensorProvider = new();
    private readonly CancellationTokenSource _sensorCancellation = new();
    private Task? _sensorPollingTask;
    private string? _sensorInitializationError;
    private OverlayWindow? _overlay;
    private string[] _selectedMetrics = [];
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
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new SavedSettings(_selectedMetrics), JsonOptions));
        ApplyOverlayMetrics();
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(_settingsPath), JsonOptions);
            _selectedMetrics = settings?.Metrics ?? [];
        }
        catch (JsonException)
        {
            _selectedMetrics = [];
        }
    }

    private void ApplyOverlayMetrics()
    {
        if (_selectedMetrics.Length == 0)
        {
            _overlay?.Hide();
            return;
        }

        _overlay ??= new OverlayWindow();
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
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(_settingsPath), JsonOptions);
            if (settings is not null)
            {
                var json = JsonSerializer.Serialize(new { type = "load", metrics = settings.Metrics }, JsonOptions);
                WebView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }
        catch (JsonException)
        {
            // Ignore an invalid settings file and let the user save a fresh selection.
        }
    }

    private async Task PublishSensorsAsync(CancellationToken cancellationToken)
    {
        while (await _sensorTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var readings = await Task.Run(_sensorProvider.Read, cancellationToken);
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

    private sealed record SavedSettings(string[] Metrics);
}
