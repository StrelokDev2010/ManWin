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
        "cpu-load", "cpu-core-load", "cpu-freq", "cpu-temp", "cpu-power",
        "gpu-load", "gpu-model", "gpu-core-freq", "gpu-mem-freq", "gpu-temp",
        "memory-temp", "junction", "gpu-fan", "gpu-power",
        "vram", "ram", "ram-used", "ram-total"
    ];
    private HashSet<string> _selected = [];
    private string? _sensorStatus;

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
        RefreshRows([]);
    }

    public void RefreshRows(IReadOnlyList<SensorReading> readings)
    {
        Rows.Children.Clear();
        if (_selected.Count == 0) return;

        var rows = BuildRows(readings);
        if (rows.Count == 0)
        {
            AddRow("MANWIN", _sensorStatus ?? "Waiting for sensor data…", false);
            return;
        }

        foreach (var row in rows) AddRow(row.Label, row.Value, row.Header);
        var footer = new TextBlock
        {
            Text = "MANWIN", Foreground = new SolidColorBrush(MediaColor.FromRgb(120, 133, 155)),
            FontSize = 9, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0)
        };
        Rows.Children.Add(footer);
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
        if (_selected.Contains("cpu-load") && Find(cpu, "Load", "Total") is { } cpuLoad) cpuValues.Add(Format(cpuLoad));
        if (_selected.Contains("cpu-temp") && Find(cpu, "Temperature", "Package", "Core Average") is { } cpuTemp) cpuValues.Add(Format(cpuTemp));
        if (_selected.Contains("cpu-freq") && Find(cpu, "Clock", "Average", "Core") is { } cpuClock) cpuValues.Add(Format(cpuClock));
        if (_selected.Contains("cpu-power") && Find(cpu, "Power", "Package", "CPU") is { } cpuPower) cpuValues.Add(Format(cpuPower));
        if (_selected.Contains("cpu-core-load"))
        {
            var cores = cpu.Where(r => r.SensorType == "Load" && r.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))
                .Take(4).Select(Format).ToArray();
            if (cores.Length > 0) cpuValues.Add(string.Join(" | ", cores));
        }
        if (cpuValues.Count > 0) { result.Add(new("CPU", "", true)); result.Add(new("", string.Join(" | ", cpuValues), false)); }

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
        if (gpuValues.Count > 0) { result.Add(new("GPU", "", true)); result.Add(new("", string.Join(" | ", gpuValues), false)); }

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
            result.Add(new("RAM", "", true));
            result.Add(new("", string.Join(" | ", ramValues), false));
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

    private void AddRow(string label, string value, bool header)
    {
        var text = new TextBlock
        {
            Text = header ? label : string.IsNullOrEmpty(label) ? value : $"{label}  {value}",
            Foreground = header ? new SolidColorBrush(MediaColor.FromRgb(159, 178, 220)) : MediaBrushes.White,
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
    private sealed record DisplayRow(string Label, string Value, bool Header);
}
