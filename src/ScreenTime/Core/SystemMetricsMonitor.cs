using System.Diagnostics;
using System.Windows.Threading;

namespace ScreenTime.Core;

public sealed class SystemMetricsMonitor : IDisposable
{
    private const int SampleWindowSize = 5;
    private const int PublishWindowSize = 5;

    private readonly DispatcherTimer _timer;
    private readonly Queue<double> _samples = new();
    private readonly PerformanceCounter? _cpuTotalCounter;
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasPrevious;
    private int _publishCounter = PublishWindowSize;

    public SystemMetricsMonitor()
    {
        _cpuTotalCounter = TryCreateCpuTotalCounter();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler<CpuUsageSnapshot>? CpuUsageUpdated;

    public CpuUsageSnapshot Current { get; private set; } = new()
    {
        UsagePercent = 0,
        LoadLevel = CpuLoadLevel.Low
    };

    public void Start()
    {
        Capture();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Capture();
    }

    private void Capture()
    {
        var usage = CaptureCpuUsage();
        if (!usage.HasValue)
        {
            return;
        }

        _samples.Enqueue(usage.Value);
        while (_samples.Count > SampleWindowSize)
        {
            _samples.Dequeue();
        }

        _publishCounter++;
        if (_publishCounter < PublishWindowSize)
        {
            return;
        }

        _publishCounter = 0;
        var smoothed = _samples.Average();
        Current = new CpuUsageSnapshot
        {
            UsagePercent = smoothed,
            LoadLevel = CpuLoadClassifier.Classify(smoothed)
        };
        CpuUsageUpdated?.Invoke(this, Current);
    }

    private double? CaptureCpuUsage()
    {
        if (_cpuTotalCounter is not null)
        {
            try
            {
                return Math.Clamp(_cpuTotalCounter.NextValue(), 0, 100);
            }
            catch
            {
                return CaptureCpuUsageFromSystemTimes();
            }
        }

        return CaptureCpuUsageFromSystemTimes();
    }

    private double? CaptureCpuUsageFromSystemTimes()
    {
        if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return null;
        }

        var idle = idleTime.ToUInt64();
        var kernel = kernelTime.ToUInt64();
        var user = userTime.ToUInt64();

        if (!_hasPrevious)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _hasPrevious = true;
            return null;
        }

        var idleDelta = idle - _lastIdle;
        var kernelDelta = kernel - _lastKernel;
        var userDelta = user - _lastUser;
        var totalDelta = kernelDelta + userDelta;

        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;

        if (totalDelta == 0)
        {
            return null;
        }

        return Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
    }

    private static PerformanceCounter? TryCreateCpuTotalCounter()
    {
        try
        {
            var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = counter.NextValue();
            return counter;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Tick -= OnTick;
        _timer.Stop();
        _cpuTotalCounter?.Close();
        _cpuTotalCounter?.Dispose();
    }
}
