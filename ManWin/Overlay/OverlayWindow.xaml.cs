using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ManWin.Sensors;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace ManWin.Overlay;

public partial class OverlayWindow : Window
{
    private static readonly string[] KnownMetrics =
    [
        "cpu-load", "cpu-core-load", "cpu-freq", "cpu-temp", "cpu-power", "cpu-model",
        "gpu-load", "gpu-model", "gpu-core-freq", "gpu-mem-freq", "gpu-temp",
        "memory-temp", "junction", "gpu-fan", "gpu-power",
        "vram", "ram", "ram-used", "ram-total", "fps"
    ];
    private HashSet<string> _selected = [];
    private string? _sensorStatus;
    private IReadOnlyList<SensorReading> _latestReadings = [];
    private MediaColor _backgroundColor = MediaColor.FromRgb(0x10, 0x12, 0x1A);
    private int _backgroundOpacityPercent = 70;
    private string _layoutMode = "vertical";
    private readonly Dictionary<string, MediaColor> _sectionTextColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cpu"] = MediaColor.FromRgb(255, 255, 255),
        ["gpu"] = MediaColor.FromRgb(255, 255, 255),
        ["ram"] = MediaColor.FromRgb(255, 255, 255)
    };

    public OverlayWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Left + 12;
        Top = SystemParameters.WorkArea.Top + 12;
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void SetMetrics(IEnumerable<string> metrics)
    {
        _selected = metrics.Intersect(KnownMetrics, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        Visibility = _selected.Count == 0 ? Visibility.Hidden : Visibility.Visible;
        RefreshRows(_latestReadings);
    }

    public void SetBackgroundOpacity(int opacityPercent)
    {
        _backgroundOpacityPercent = Math.Clamp(opacityPercent, 20, 100);
        ApplyBackgroundColor();
    }

    public void SetBackgroundColor(string colorHex)
    {
        if (System.Windows.Media.ColorConverter.ConvertFromString(colorHex) is MediaColor color)
            _backgroundColor = color;
        ApplyBackgroundColor();
    }

    public void SetSectionTextColor(string section, string colorHex)
    {
        if (!new[] { "cpu", "gpu", "ram" }.Contains(section, StringComparer.OrdinalIgnoreCase)) return;
        if (System.Windows.Media.ColorConverter.ConvertFromString(colorHex) is not MediaColor color) return;
        _sectionTextColors[section] = color;
        RefreshRows(_latestReadings);
    }

    public void SetLayoutMode(string mode)
    {
        _layoutMode = string.Equals(mode, "horizontal", StringComparison.OrdinalIgnoreCase) ? "horizontal" : "vertical";
        if (_layoutMode == "horizontal")
        {
            Width = double.NaN;
            MaxWidth = Math.Max(270, SystemParameters.WorkArea.Width * 0.72);
            SizeToContent = SizeToContent.WidthAndHeight;
        }
        else
        {
            MaxWidth = double.PositiveInfinity;
            Width = 270;
            SizeToContent = SizeToContent.Height;
        }
        RefreshRows(_latestReadings);
    }

    private void ApplyBackgroundColor()
    {
        var alpha = (byte)Math.Round(_backgroundOpacityPercent * byte.MaxValue / 100d, MidpointRounding.AwayFromZero);
        BackgroundSurface.Background = new SolidColorBrush(MediaColor.FromArgb(alpha, _backgroundColor.R, _backgroundColor.G, _backgroundColor.B));
    }

    public void RefreshRows(IReadOnlyList<SensorReading> readings)
    {
        _latestReadings = readings;
        Rows.Children.Clear();
        if (_selected.Count == 0) return;

        var rows = BuildRows(readings);
        if (rows.Count == 0)
        {
            AddRow("", _sensorStatus ?? "Waiting for sensor data…", false, null);
            return;
        }

        if (_layoutMode == "horizontal")
        {
            AddHorizontalRows(rows);
            return;
        }

        foreach (var row in rows) AddRow(row.Label, row.Value, row.Header, row.Section);
    }

    private void AddHorizontalRows(IReadOnlyList<DisplayRow> rows)
    {
        var line = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var sections = rows.Where(row => row.Header).Select(row => row.Section).ToArray();
        for (var index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            if (index > 0)
                line.Inlines.Add(new System.Windows.Documents.Run("  |  ") { Foreground = new SolidColorBrush(MediaColor.FromRgb(150, 155, 170)) });

            var brush = _sectionTextColors.TryGetValue(section, out var color) ? new SolidColorBrush(color) : MediaBrushes.White;
            line.Inlines.Add(new System.Windows.Documents.Run($"{section.ToUpperInvariant()}: ") { Foreground = brush, FontWeight = FontWeights.SemiBold });
            var values = rows.Where(row => row.Section == section && !row.Header)
                .SelectMany(row => row.Value.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries));
            var valueIndex = 0;
            foreach (var value in values)
            {
                if (valueIndex++ > 0)
                    line.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = new SolidColorBrush(MediaColor.FromRgb(150, 155, 170)) });
                line.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = brush });
            }
        }
        Rows.Children.Add(line);
    }

    public void SetSensorStatus(string? status)
    {
        _sensorStatus = status;
        RefreshRows([]);
    }

    private List<DisplayRow> BuildRows(IReadOnlyList<SensorReading> readings)
    {
        var result = new List<DisplayRow>();
        var cpu = readings.Where(r => r.Category == "CPU").ToList();
        var gpu = readings.Where(r => r.Category == "GPU").ToList();
        var ram = readings.Where(r => r.Category == "RAM").ToList();

        var cpuValues = new List<string>();
        if (_selected.Contains("cpu-model") && cpu.FirstOrDefault(r => r.SensorType == "Model")?.TextValue is { Length: > 0 } cpuModel)
            cpuValues.Add($"Model: {cpuModel}");
        if (_selected.Contains("cpu-load") && Find(cpu, "Load", "Total") is { } cpuLoad) cpuValues.Add(Format(cpuLoad));
        if (_selected.Contains("cpu-temp") && Find(cpu, "Temperature", "Package", "Core Average") is { } cpuTemp) cpuValues.Add(Format(cpuTemp));
        if (_selected.Contains("cpu-freq") && Find(cpu, "Clock", "Average", "Core") is { } cpuClock) cpuValues.Add(Format(cpuClock));
        if (_selected.Contains("cpu-power") && Find(cpu, "Power", "Package", "CPU") is { } cpuPower) cpuValues.Add(Format(cpuPower));
        if (_selected.Contains("cpu-core-load"))
        {
            var cores = cpu.Where(r => r.SensorType == "Load" && r.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))
                .Take(4).Select(Format).ToArray();
            if (cores.Length > 0) cpuValues.Add(string.Join(Environment.NewLine, cores));
        }
        if (cpuValues.Count > 0) { result.Add(new("CPU", "", true, "cpu")); result.Add(new("", string.Join(Environment.NewLine, cpuValues), false, "cpu")); }

        var gpuValues = new List<string>();
        if (_selected.Contains("gpu-load") && Find(gpu, "Load", "GPU Core", "Core") is { } gpuLoad) gpuValues.Add(Format(gpuLoad));
        if (_selected.Contains("gpu-temp") && Find(gpu, "Temperature", "GPU Core", "Core") is { } gpuTemp) gpuValues.Add(Format(gpuTemp));
        if (_selected.Contains("gpu-core-freq") && Find(gpu, "Clock", "Core") is { } gpuClock) gpuValues.Add(Format(gpuClock));
        if (_selected.Contains("gpu-mem-freq") && Find(gpu, "Clock", "Memory") is { } gpuMemClock) gpuValues.Add(Format(gpuMemClock));
        if (_selected.Contains("memory-temp") && Find(gpu, "Temperature", "Memory", "Mem") is { } memoryTemp) gpuValues.Add($"Mem {Format(memoryTemp)}");
        if (_selected.Contains("junction") && Find(gpu, "Temperature", "Junction", "Hot Spot") is { } junction) gpuValues.Add($"Hotspot {Format(junction)}");
        if (_selected.Contains("gpu-fan") && gpu.FirstOrDefault(r => r.SensorType == "Fan") is { } fan) gpuValues.Add(Format(fan));
        if (_selected.Contains("gpu-power") && gpu.FirstOrDefault(r => r.SensorType == "Power") is { } gpuPower) gpuValues.Add(Format(gpuPower));
        if (_selected.Contains("gpu-model") && gpu.FirstOrDefault(r => r.SensorType == "Model")?.TextValue is { Length: > 0 } model) gpuValues.Add(model);
        if (_selected.Contains("vram"))
        {
            // GPU Memory is also the name of the memory clock sensor. Accept only
            // capacity sensors so VRAM can never accidentally display a frequency.
            var memorySensors = gpu.Where(r => r.SensorType is "Data" or "SmallData").ToArray();
            var used = memorySensors.FirstOrDefault(r => r.SensorName.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
                ?? memorySensors.FirstOrDefault(r => r.SensorName.Equals("D3D Dedicated Memory Used", StringComparison.OrdinalIgnoreCase));
            var total = memorySensors.FirstOrDefault(r => r.SensorName.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase));
            if (used is not null) gpuValues.Add(total is null ? FormatCapacity(used) : $"VRAM {FormatCapacity(used)}/{FormatCapacity(total)}");
        }
        if (gpuValues.Count > 0) { result.Add(new("GPU", "", true, "gpu")); result.Add(new("", string.Join(Environment.NewLine, gpuValues), false, "gpu")); }

        var physicalMemory = ram.Where(r => r.HardwareName.Equals("Physical Memory", StringComparison.OrdinalIgnoreCase)).ToList();
        var ramValues = new List<string>();
        if (_selected.Contains("ram") && Find(physicalMemory, "Load", "Memory") is { } ramLoad)
            ramValues.Add($"Usage: {ramLoad.Value:0}%");
        if (_selected.Contains("ram-used") && physicalMemory.FirstOrDefault(r => r.SensorType == "Data" && r.SensorName.Equals("Used", StringComparison.OrdinalIgnoreCase)) is { } ramUsed)
            ramValues.Add($"Used: {FormatCapacity(ramUsed)}");
        if (_selected.Contains("ram-total") && physicalMemory.FirstOrDefault(r => r.SensorType == "Data" && r.SensorName.Equals("Total", StringComparison.OrdinalIgnoreCase)) is { } ramTotal)
            ramValues.Add($"Total: {FormatCapacity(ramTotal)}");
        if (ramValues.Count > 0)
        {
            result.Add(new("RAM", "", true, "ram"));
            result.Add(new("", string.Join(Environment.NewLine, ramValues), false, "ram"));
        }

        if (_selected.Contains("fps"))
        {
            var fpsReading = readings.FirstOrDefault(r => r.Category == "FPS");
            var fpsValue = fpsReading is { Value: > 0 }
                ? $"{fpsReading.Value:0}"
                : "---";
            result.Add(new("FPS", "", true, "fps"));
            result.Add(new("", fpsValue, false, "fps"));
        }
        return result;
    }

    private static SensorReading? Find(IEnumerable<SensorReading> readings, string type, params string[] nameHints) =>
        readings.Where(r => r.SensorType == type)
            .OrderByDescending(r => nameHints.Any(h => r.SensorName.Contains(h, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault();

    private static string Format(SensorReading reading)
    {
        var value = reading.Unit == "°C" ? $"{reading.Value:0}°C" : reading.Unit == "%" ? $"{reading.Value:0}%" : reading.Unit == "MHz" ? $"{reading.Value / 1000:0.0} GHz" : $"{reading.Value:0.#} {reading.Unit}".Trim();
        return string.IsNullOrWhiteSpace(reading.SensorName) ? value : $"{reading.SensorName}: {value}";
    }

    private static string FormatCapacity(SensorReading reading)
    {
        var gigabytes = reading.Unit == "MB" ? reading.Value / 1024 : reading.Value;
        return $"{gigabytes:0.0} GB";
    }

    private void AddRow(string label, string value, bool header, string? section)
    {
        var foreground = section is not null && _sectionTextColors.TryGetValue(section, out var sectionColor)
            ? new SolidColorBrush(sectionColor)
            : MediaBrushes.White;
        var text = new TextBlock
        {
            Text = header ? label : string.IsNullOrEmpty(label) ? value : $"{label}  {value}",
            Foreground = foreground,
            FontSize = header ? 11 : 13,
            FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = header ? new Thickness(0, Rows.Children.Count == 0 ? 0 : 9, 0, 2) : new Thickness(0, 0, 0, 1)
        };
        Rows.Children.Add(text);
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x20L | 0x80L | 0x08000000L)); // transparent, toolwindow, noactivate
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    private sealed record DisplayRow(string Label, string Value, bool Header, string Section);
}
