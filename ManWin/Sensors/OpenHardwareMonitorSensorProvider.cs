using OpenHardwareMonitor.Hardware;

namespace ManWin.Sensors;

/// <summary>Reads available hardware sensors through OpenHardwareMonitorLib.</summary>
public sealed class OpenHardwareMonitorSensorProvider : ISensorProvider, IVisitor, IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
        IsNetworkEnabled = true,
        IsStorageEnabled = true
    };
    private bool _opened;
    private bool _disposed;

    public void Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_opened) return;
        _computer.Open(false);
        _opened = true;
    }

    public IReadOnlyList<SensorReading> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_opened) Open();

        _computer.Accept(this);
        var readings = new List<SensorReading>();
        foreach (var hardware in _computer.Hardware)
            CollectHardware(hardware, readings);

        return readings;
    }

    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware) => hardware.Update();
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }

    public void Dispose()
    {
        if (_disposed) return;
        if (_opened) _computer.Close();
        _disposed = true;
    }

    private static void CollectHardware(IHardware hardware, ICollection<SensorReading> readings)
    {
        var category = GetCategory(hardware.HardwareType.ToString());
        var hardwareName = hardware.Name;
        if (category == "GPU")
        {
            readings.Add(new SensorReading(
                Id: $"{hardware.Identifier}/model",
                Label: "GPU Model",
                Value: 0,
                Unit: string.Empty,
                Category: category,
                SensorType: "Model",
                TextValue: hardwareName,
                HardwareName: hardwareName,
                HardwareType: hardware.HardwareType.ToString(),
                SensorName: "Model"));
        }

        foreach (var sensor in hardware.Sensors)
        {
            // Some drivers report NaN/Infinity while a sensor is initializing or
            // unsupported. JSON cannot represent those as numeric values, so skip them.
            if (sensor.Value is not { } value || !float.IsFinite(value)) continue;
            var sensorType = sensor.SensorType.ToString();
            readings.Add(new SensorReading(
                Id: $"{hardware.Identifier}/{sensor.Identifier}",
                Label: $"{hardwareName} · {sensor.Name}",
                Value: value,
                Unit: GetUnit(sensorType),
                Category: category,
                SensorType: sensorType,
                HardwareName: hardwareName,
                HardwareType: hardware.HardwareType.ToString(),
                SensorName: sensor.Name));
        }

        foreach (var child in hardware.SubHardware)
            CollectHardware(child, readings);
    }

    private static string GetCategory(string hardwareType) => hardwareType switch
    {
        "Cpu" => "CPU",
        "GpuNvidia" or "GpuAti" or "GpuIntel" => "GPU",
        "Memory" => "RAM",
        _ => "Other"
    };

    private static string GetUnit(string sensorType) => sensorType switch
    {
        "Temperature" => "°C",
        "Voltage" => "V",
        "Current" => "A",
        "Clock" => "MHz",
        "Load" => "%",
        "Control" or "Level" or "Humidity" => "%",
        "Frequency" => "Hz",
        "Fan" => "RPM",
        "Flow" => "L/h",
        "Power" => "W",
        "Data" => "GB",
        "SmallData" => "MB",
        "Throughput" => "B/s",
        "Energy" => "mWh",
        "Noise" => "dB",
        "Conductivity" => "µS/cm",
        "TimeSpan" => "s",
        "Timing" => "ns",
        _ => string.Empty
    };
}
