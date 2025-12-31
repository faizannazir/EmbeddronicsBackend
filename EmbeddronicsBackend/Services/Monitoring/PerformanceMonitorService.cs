using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;

namespace EmbeddronicsBackend.Services.Monitoring;

/// <summary>
/// Interface for application performance monitoring
/// </summary>
public interface IPerformanceMonitorService
{
    /// <summary>
    /// Start tracking an operation
    /// </summary>
    IDisposable TrackOperation(string operationName, Dictionary<string, object>? properties = null);

    /// <summary>
    /// Record a metric value
    /// </summary>
    void RecordMetric(string metricName, double value, Dictionary<string, object>? properties = null);

    /// <summary>
    /// Increment a counter
    /// </summary>
    void IncrementCounter(string counterName, int value = 1);

    /// <summary>
    /// Get current performance statistics
    /// </summary>
    PerformanceStatistics GetStatistics();

    /// <summary>
    /// Get metrics for a specific time period
    /// </summary>
    List<MetricDataPoint> GetMetrics(string metricName, TimeSpan period);
}

/// <summary>
/// Performance statistics summary
/// </summary>
public class PerformanceStatistics
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; set; }
    public long TotalRequests { get; set; }
    public long ActiveConnections { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double RequestsPerSecond { get; set; }
    public long MemoryUsedMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ThreadCount { get; set; }
    public Dictionary<string, long> Counters { get; set; } = new();
    public Dictionary<string, OperationStatistics> Operations { get; set; } = new();
}

/// <summary>
/// Statistics for a specific operation type
/// </summary>
public class OperationStatistics
{
    public string OperationName { get; set; } = string.Empty;
    public long TotalCalls { get; set; }
    public double AverageDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double P95DurationMs { get; set; }
    public long ErrorCount { get; set; }
    public DateTime LastCallAt { get; set; }
}

/// <summary>
/// Single metric data point
/// </summary>
public class MetricDataPoint
{
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

/// <summary>
/// Service for tracking application performance metrics
/// </summary>
public class PerformanceMonitorService : IPerformanceMonitorService
{
    private static readonly DateTime _startTime = DateTime.UtcNow;
    private static readonly ConcurrentDictionary<string, long> _counters = new();
    private static readonly ConcurrentDictionary<string, ConcurrentBag<OperationTiming>> _operationTimings = new();
    private static readonly ConcurrentQueue<MetricDataPoint> _metricsHistory = new();
    private const int MaxMetricsHistory = 10000;

    public IDisposable TrackOperation(string operationName, Dictionary<string, object>? properties = null)
    {
        return new OperationTracker(operationName, properties, this);
    }

    public void RecordMetric(string metricName, double value, Dictionary<string, object>? properties = null)
    {
        var dataPoint = new MetricDataPoint
        {
            Timestamp = DateTime.UtcNow,
            MetricName = metricName,
            Value = value,
            Properties = properties
        };

        _metricsHistory.Enqueue(dataPoint);

        // Trim old metrics
        while (_metricsHistory.Count > MaxMetricsHistory)
        {
            _metricsHistory.TryDequeue(out _);
        }

        Log.Debug("Metric recorded: {MetricName} = {Value}", metricName, value);
    }

    public void IncrementCounter(string counterName, int value = 1)
    {
        _counters.AddOrUpdate(counterName, value, (_, existing) => existing + value);
    }

    public PerformanceStatistics GetStatistics()
    {
        var process = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var uptime = now - _startTime;

        var stats = new PerformanceStatistics
        {
            GeneratedAt = now,
            Uptime = uptime,
            TotalRequests = _counters.GetValueOrDefault("http_requests_total", 0),
            MemoryUsedMB = process.WorkingSet64 / (1024 * 1024),
            ThreadCount = process.Threads.Count,
            Counters = new Dictionary<string, long>(_counters)
        };

        // Calculate average response time
        var recentTimings = GetRecentTimings(TimeSpan.FromMinutes(5));
        if (recentTimings.Any())
        {
            stats.AverageResponseTimeMs = recentTimings.Average(t => t.DurationMs);
        }

        // Calculate requests per second (last minute)
        var lastMinuteRequests = _counters.GetValueOrDefault("http_requests_last_minute", 0);
        stats.RequestsPerSecond = lastMinuteRequests / 60.0;

        // Build operation statistics
        foreach (var (opName, timings) in _operationTimings)
        {
            var recentOpTimings = timings.Where(t => t.Timestamp > now.AddMinutes(-15)).ToList();
            if (recentOpTimings.Any())
            {
                var durations = recentOpTimings.Select(t => t.DurationMs).OrderBy(d => d).ToList();
                
                stats.Operations[opName] = new OperationStatistics
                {
                    OperationName = opName,
                    TotalCalls = recentOpTimings.Count,
                    AverageDurationMs = durations.Average(),
                    MinDurationMs = durations.Min(),
                    MaxDurationMs = durations.Max(),
                    P95DurationMs = GetPercentile(durations, 95),
                    ErrorCount = recentOpTimings.Count(t => t.HasError),
                    LastCallAt = recentOpTimings.Max(t => t.Timestamp)
                };
            }
        }

        return stats;
    }

    public List<MetricDataPoint> GetMetrics(string metricName, TimeSpan period)
    {
        var cutoff = DateTime.UtcNow - period;
        return _metricsHistory
            .Where(m => m.MetricName == metricName && m.Timestamp > cutoff)
            .OrderBy(m => m.Timestamp)
            .ToList();
    }

    internal void RecordOperationTiming(string operationName, double durationMs, bool hasError)
    {
        var timing = new OperationTiming
        {
            Timestamp = DateTime.UtcNow,
            DurationMs = durationMs,
            HasError = hasError
        };

        var timings = _operationTimings.GetOrAdd(operationName, _ => new ConcurrentBag<OperationTiming>());
        timings.Add(timing);

        // Log slow operations
        if (durationMs > 1000)
        {
            Log.Warning("Slow operation detected: {OperationName} took {DurationMs}ms", operationName, durationMs);
        }
    }

    private List<OperationTiming> GetRecentTimings(TimeSpan period)
    {
        var cutoff = DateTime.UtcNow - period;
        return _operationTimings
            .SelectMany(kvp => kvp.Value)
            .Where(t => t.Timestamp > cutoff)
            .ToList();
    }

    private static double GetPercentile(List<double> sortedValues, int percentile)
    {
        if (!sortedValues.Any()) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private class OperationTiming
    {
        public DateTime Timestamp { get; set; }
        public double DurationMs { get; set; }
        public bool HasError { get; set; }
    }

    private class OperationTracker : IDisposable
    {
        private readonly string _operationName;
        private readonly Dictionary<string, object>? _properties;
        private readonly PerformanceMonitorService _monitor;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        private bool _hasError;

        public OperationTracker(string operationName, Dictionary<string, object>? properties, PerformanceMonitorService monitor)
        {
            _operationName = operationName;
            _properties = properties;
            _monitor = monitor;
            _stopwatch = Stopwatch.StartNew();
        }

        public void MarkError()
        {
            _hasError = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopwatch.Stop();
            var durationMs = _stopwatch.Elapsed.TotalMilliseconds;

            _monitor.RecordOperationTiming(_operationName, durationMs, _hasError);
            _monitor.RecordMetric($"operation.{_operationName}.duration", durationMs, _properties);
            _monitor.IncrementCounter($"operation.{_operationName}.count");

            if (_hasError)
            {
                _monitor.IncrementCounter($"operation.{_operationName}.errors");
            }
        }
    }
}
