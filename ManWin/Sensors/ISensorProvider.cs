namespace ManWin.Sensors;

public interface ISensorProvider
{
    IReadOnlyList<SensorReading> Read();
}
