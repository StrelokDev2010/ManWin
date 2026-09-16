using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ManWin.Sensors;

/// <summary>
/// Experimental FPS capture from DXGI and D3D9 ETW Present_Start events.
/// This counts presents submitted by the foreground process; it is not a full PresentMon analysis pipeline.
/// </summary>
public sealed class FpsCaptureProvider : IDisposable
{
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private const ulong DxgiKeywords = 0x8000000000000002;
    private readonly ConcurrentDictionary<int, PresentTimestampBuffer> _presentTimestampsByProcess = new();
    private readonly object _sync = new();
    private TraceEventSession? _session;
    private Task? _processingTask;
    private long _providerEventCount;
    private long _presentEventCount;

    public void Start()
    {
        lock (_sync)
        {
            if (_session is not null) return;

            var session = new TraceEventSession($"ManWin-FPS-{Environment.ProcessId}")
            {
                StopOnDispose = true,
                BufferSizeMB = 4,
                BufferQuantumKB = 64
            };

            try
            {
                // DXGI and D3D9 are manifest-based ETW providers. We only need
                // common event-header fields, so consume raw events directly.
                session.Source.AllEvents += OnEvent;
                session.EnableProvider(DxgiProvider, TraceEventLevel.Verbose, DxgiKeywords);
                session.EnableProvider(D3D9Provider, TraceEventLevel.Verbose, DxgiKeywords);
                _session = session;
                _processingTask = Task.Factory.StartNew(
                    () => session.Source.Process(), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }
    }

    public SensorReading Read()
    {
        var foregroundProcessId = GetForegroundProcessId();
        if (_processingTask is { IsFaulted: true } processingTask)
            return StatusReading($"ETW failed: {processingTask.Exception?.GetBaseException().Message}");

        PruneOldPresentEvents(foregroundProcessId);

        if (foregroundProcessId == 0 || !_presentTimestampsByProcess.TryGetValue(foregroundProcessId, out var timestamps))
            return WaitingReading(foregroundProcessId);

        var fps = timestamps.Count;
        return fps == 0 || !timestamps.HasRecentEvents
            ? WaitingReading(foregroundProcessId)
            : new("fps", "FPS", fps, "FPS", "FPS", "FrameRate", $"{fps} FPS");
    }

    private SensorReading WaitingReading(int processId)
    {
        var providerEvents = Interlocked.Read(ref _providerEventCount);
        var presentEvents = Interlocked.Read(ref _presentEventCount);
        var otherRenderer = _presentTimestampsByProcess
            .Where(entry => entry.Key != processId && entry.Key != Environment.ProcessId &&
                entry.Value.Count > 0 && entry.Value.HasRecentEvents)
            .OrderByDescending(entry => entry.Value.Count)
            .FirstOrDefault();
        var status = providerEvents == 0
            ? "ETW: no DXGI/D3D9 events"
            : presentEvents == 0
                ? "ETW active; no Present events"
                : processId == 0
                    ? "Select a game window"
                    : otherRenderer.Value is not null
                        ? $"No frames for PID {processId}; ETW sees {GetProcessName(otherRenderer.Key)} ({otherRenderer.Key})"
                        : processId == Environment.ProcessId
                            ? "ManWin is foreground; focus the game"
                            : $"ETW saw presents, not PID {processId}";
        return StatusReading(status);
    }

    private void PruneOldPresentEvents(int foregroundProcessId)
    {
        foreach (var entry in _presentTimestampsByProcess)
        {
            entry.Value.PruneToLatestSecond();
            if (entry.Value.IsEmpty && !entry.Value.HasRecentEvents && entry.Key != foregroundProcessId)
                _presentTimestampsByProcess.TryRemove(entry.Key, out _);
        }
    }

    private static string GetProcessName(int processId)
    {
        try { return Process.GetProcessById(processId).ProcessName; }
        catch (ArgumentException) { return "exited process"; }
        catch (InvalidOperationException) { return "unknown process"; }
    }

    private static SensorReading StatusReading(string status) =>
        new("fps", "FPS", 0, "FPS", "FPS", "FrameRate", status);

    private void OnEvent(TraceEvent data)
    {
        if (data.ProviderGuid != DxgiProvider && data.ProviderGuid != D3D9Provider) return;
        Interlocked.Increment(ref _providerEventCount);

        var isDxgiPresent = data.ProviderGuid == DxgiProvider && ((int)data.ID is 0x2A or 0x37);
        var isD3D9Present = data.ProviderGuid == D3D9Provider && (int)data.ID == 0x01;
        if (!isDxgiPresent && !isD3D9Present) return;
        Interlocked.Increment(ref _presentEventCount);

        var timestamps = _presentTimestampsByProcess.GetOrAdd(data.ProcessID, _ => new PresentTimestampBuffer());
        // ETW realtime delivery is buffered. Use the monotonic session clock for
        // the FPS window instead of comparing event time to the wall clock.
        var timestamp = data.TimeStampRelativeMSec;
        timestamps.Add(timestamp);

        // Keep the per-process map bounded when applications start and exit.
        if (_presentTimestampsByProcess.Count > 32)
        {
            foreach (var entry in _presentTimestampsByProcess)
            {
                entry.Value.PruneToLatestSecond();
                if (entry.Value.IsEmpty && !entry.Value.HasRecentEvents)
                    _presentTimestampsByProcess.TryRemove(entry.Key, out _);
            }
        }
    }

    private static int GetForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    public void Dispose()
    {
        TraceEventSession? session;
        Task? processingTask;
        lock (_sync)
        {
            session = _session;
            processingTask = _processingTask;
            _session = null;
            _processingTask = null;
        }

        if (session is null) return;
        try { session.Dispose(); }
        finally
        {
            try { processingTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    private sealed class PresentTimestampBuffer
    {
        private readonly ConcurrentQueue<double> _timestamps = new();
        private int _count;
        private double _latestTimestamp;
        private long _lastReceiptTimestamp;

        public int Count => Volatile.Read(ref _count);
        public bool IsEmpty => Count == 0;
        public bool HasRecentEvents
        {
            get
            {
                var lastReceipt = Interlocked.Read(ref _lastReceiptTimestamp);
                return lastReceipt != 0 && Stopwatch.GetElapsedTime(lastReceipt) < TimeSpan.FromSeconds(5);
            }
        }

        public void Add(double timestamp)
        {
            _timestamps.Enqueue(timestamp);
            Interlocked.Increment(ref _count);
            Volatile.Write(ref _latestTimestamp, timestamp);
            Interlocked.Exchange(ref _lastReceiptTimestamp, Stopwatch.GetTimestamp());
            PruneToLatestSecond();
            while (Count > 2000) TryDequeue(out _);
        }

        public void PruneToLatestSecond()
        {
            var cutoff = Volatile.Read(ref _latestTimestamp) - 1000;
            while (_timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                TryDequeue(out _);
        }

        public bool TryPeek(out double timestamp) => _timestamps.TryPeek(out timestamp);

        public bool TryDequeue(out double timestamp)
        {
            if (!_timestamps.TryDequeue(out timestamp)) return false;
            Interlocked.Decrement(ref _count);
            return true;
        }
    }
}
