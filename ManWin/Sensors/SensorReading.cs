namespace ManWin.Sensors;

public sealed record SensorReading(
    string Id,
    string Label,
    double Value,
    string Unit,
    string Category = "Other",
    string SensorType = "Unknown",
    string? TextValue = null,
    string HardwareName = "",
    string HardwareType = "",
    string SensorName = "");
