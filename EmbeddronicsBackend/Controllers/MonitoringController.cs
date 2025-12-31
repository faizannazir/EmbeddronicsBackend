using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using EmbeddronicsBackend.Services.Monitoring;
using System.Text.Json;

namespace EmbeddronicsBackend.Controllers;

/// <summary>
/// Controller for monitoring, health checks, and performance metrics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IPerformanceMonitorService _performanceMonitor;
    private readonly IConfiguration _configuration;

    public MonitoringController(
        HealthCheckService healthCheckService,
        IPerformanceMonitorService performanceMonitor,
        IConfiguration configuration)
    {
        _healthCheckService = healthCheckService;
        _performanceMonitor = performanceMonitor;
        _configuration = configuration;
    }

    /// <summary>
    /// Simple health check endpoint for load balancers
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// Detailed health check with all service statuses
    /// </summary>
    [HttpGet("health/detailed")]
    [AllowAnonymous]
    public async Task<IActionResult> DetailedHealthCheck()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        var response = new
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Timestamp = DateTime.UtcNow,
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration.TotalMilliseconds,
                Data = e.Value.Data,
                Exception = e.Value.Exception?.Message
            })
        };

        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => 200,
            HealthStatus.Degraded => 200,
            HealthStatus.Unhealthy => 503,
            _ => 500
        };

        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Liveness probe for Kubernetes
    /// </summary>
    [HttpGet("health/live")]
    [AllowAnonymous]
    public IActionResult LivenessProbe()
    {
        // Basic check that the application is running
        return Ok(new
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe for Kubernetes
    /// </summary>
    [HttpGet("health/ready")]
    [AllowAnonymous]
    public async Task<IActionResult> ReadinessProbe()
    {
        var report = await _healthCheckService.CheckHealthAsync(
            c => c.Tags.Contains("ready"));

        if (report.Status == HealthStatus.Healthy)
        {
            return Ok(new
            {
                Status = "Ready",
                Timestamp = DateTime.UtcNow
            });
        }

        return StatusCode(503, new
        {
            Status = "NotReady",
            Timestamp = DateTime.UtcNow,
            Issues = report.Entries
                .Where(e => e.Value.Status != HealthStatus.Healthy)
                .Select(e => new { Name = e.Key, Status = e.Value.Status.ToString() })
        });
    }

    /// <summary>
    /// Get performance statistics (admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult GetPerformanceStats()
    {
        var stats = _performanceMonitor.GetStatistics();
        return Ok(stats);
    }

    /// <summary>
    /// Get metrics for a specific metric name
    /// </summary>
    [HttpGet("metrics/{metricName}")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult GetMetrics(string metricName, [FromQuery] int minutes = 60)
    {
        var period = TimeSpan.FromMinutes(Math.Min(minutes, 1440)); // Max 24 hours
        var metrics = _performanceMonitor.GetMetrics(metricName, period);

        return Ok(new
        {
            MetricName = metricName,
            Period = $"Last {minutes} minutes",
            Count = metrics.Count,
            Data = metrics
        });
    }

    /// <summary>
    /// Get system information
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public IActionResult GetSystemInfo()
    {
        var assembly = GetType().Assembly;
        
        return Ok(new
        {
            Application = "Embeddronics API",
            Version = assembly.GetName().Version?.ToString() ?? "1.0.0",
            Environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production",
            Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ServerTime = DateTime.UtcNow,
            TimeZone = TimeZoneInfo.Local.DisplayName
        });
    }

    /// <summary>
    /// Prometheus-compatible metrics endpoint
    /// </summary>
    [HttpGet("prometheus")]
    [AllowAnonymous]
    public IActionResult PrometheusMetrics()
    {
        var stats = _performanceMonitor.GetStatistics();
        var metrics = new List<string>
        {
            "# HELP embeddronics_uptime_seconds Application uptime in seconds",
            "# TYPE embeddronics_uptime_seconds gauge",
            $"embeddronics_uptime_seconds {stats.Uptime.TotalSeconds:F0}",
            "",
            "# HELP embeddronics_http_requests_total Total number of HTTP requests",
            "# TYPE embeddronics_http_requests_total counter",
            $"embeddronics_http_requests_total {stats.TotalRequests}",
            "",
            "# HELP embeddronics_memory_used_mb Memory usage in MB",
            "# TYPE embeddronics_memory_used_mb gauge",
            $"embeddronics_memory_used_mb {stats.MemoryUsedMB}",
            "",
            "# HELP embeddronics_thread_count Number of threads",
            "# TYPE embeddronics_thread_count gauge",
            $"embeddronics_thread_count {stats.ThreadCount}",
            "",
            "# HELP embeddronics_average_response_time_ms Average response time in milliseconds",
            "# TYPE embeddronics_average_response_time_ms gauge",
            $"embeddronics_average_response_time_ms {stats.AverageResponseTimeMs:F2}",
            "",
            "# HELP embeddronics_requests_per_second Requests per second",
            "# TYPE embeddronics_requests_per_second gauge",
            $"embeddronics_requests_per_second {stats.RequestsPerSecond:F2}"
        };

        // Add counter metrics
        foreach (var (name, value) in stats.Counters)
        {
            var sanitizedName = name.Replace(".", "_").Replace("-", "_");
            metrics.Add("");
            metrics.Add($"# HELP embeddronics_{sanitizedName} Counter: {name}");
            metrics.Add($"# TYPE embeddronics_{sanitizedName} counter");
            metrics.Add($"embeddronics_{sanitizedName} {value}");
        }

        return Content(string.Join("\n", metrics), "text/plain");
    }

    /// <summary>
    /// Record a custom metric (admin only)
    /// </summary>
    [HttpPost("metrics")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult RecordMetric([FromBody] RecordMetricRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MetricName))
        {
            return BadRequest(new { Error = "MetricName is required" });
        }

        _performanceMonitor.RecordMetric(request.MetricName, request.Value, request.Properties);

        return Ok(new
        {
            Success = true,
            MetricName = request.MetricName,
            Value = request.Value,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class RecordMetricRequest
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}
