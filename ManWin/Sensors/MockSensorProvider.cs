namespace ManWin.Sensors;

/// <summary>Temporary provider used to develop the UI before wiring the hardware library.</summary>
public sealed class MockSensorProvider : ISensorProvider
{
    private readonly Random _random = new();

    public IReadOnlyList<SensorReading> Read() =>
    [
        new("cpu", "CPU", Math.Round(20 + _random.NextDouble() * 55, 1), "%"),
        new("gpu", "GPU", Math.Round(15 + _random.NextDouble() * 65, 1), "%"),
        new("ram", "Memoria", Math.Round(4 + _random.NextDouble() * 20, 1), "GB"),
        new("cpu-temp", "Temp. CPU", Math.Round(35 + _random.NextDouble() * 35, 1), "°C")
    ];
}
