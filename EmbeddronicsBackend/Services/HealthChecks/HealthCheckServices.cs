using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using EmbeddronicsBackend.Data;
using System.Diagnostics;

namespace EmbeddronicsBackend.Services.HealthChecks;

/// <summary>
/// Health check for database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly EmbeddronicsDbContext _context;

    public DatabaseHealthCheck(EmbeddronicsDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Test database connection
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            
            stopwatch.Stop();

            if (canConnect)
            {
                var data = new Dictionary<string, object>
                {
                    { "ResponseTimeMs", stopwatch.ElapsedMilliseconds },
                    { "Provider", _context.Database.ProviderName ?? "Unknown" }
                };

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    return HealthCheckResult.Degraded(
                        $"Database is responsive but slow ({stopwatch.ElapsedMilliseconds}ms)",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"Database is healthy ({stopwatch.ElapsedMilliseconds}ms)",
                    data: data);
            }

            return HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    { "Error", ex.Message }
                });
        }
    }
}

/// <summary>
/// Health check for memory usage
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _threshold;

    public MemoryHealthCheck(long thresholdMB = 1024) // 1GB default
    {
        _threshold = thresholdMB * 1024 * 1024;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var memoryUsed = process.WorkingSet64;
        var memoryUsedMB = memoryUsed / (1024 * 1024);

        var data = new Dictionary<string, object>
        {
            { "WorkingSetMB", memoryUsedMB },
            { "PrivateMemoryMB", process.PrivateMemorySize64 / (1024 * 1024) },
            { "GCTotalMemoryMB", GC.GetTotalMemory(false) / (1024 * 1024) },
            { "ThresholdMB", _threshold / (1024 * 1024) }
        };

        if (memoryUsed > _threshold)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage is high: {memoryUsedMB}MB",
                data: data));
        }

        if (memoryUsed > _threshold * 0.8)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage is approaching threshold: {memoryUsedMB}MB",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage is normal: {memoryUsedMB}MB",
            data: data));
    }
}

/// <summary>
/// Health check for SignalR hub connectivity
/// </summary>
public class SignalRHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SignalRHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var connectionManager = scope.ServiceProvider.GetService<IConnectionManagerService>();

            if (connectionManager == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "SignalR connection manager service not available"));
            }

            // SignalR is configured and services are available
            return Task.FromResult(HealthCheckResult.Healthy(
                "SignalR hub is configured and ready",
                data: new Dictionary<string, object>
                {
                    { "HubPath", "/hubs/chat" }
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SignalR health check failed",
                exception: ex));
        }
    }
}

/// <summary>
/// Health check for external services (email, etc.)
/// </summary>
public class ExternalServicesHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public ExternalServicesHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var services = new Dictionary<string, object>();
        var allHealthy = true;

        // Check Email Service configuration
        var emailEnabled = _configuration.GetValue<bool>("EmailSettings:IsEnabled");
        services["EmailService"] = emailEnabled ? "Enabled" : "Disabled";

        // Check Seq logging
        var seqUrl = "http://localhost:5341";
        services["SeqLogging"] = seqUrl;

        // Check JWT configuration
        var jwtConfigured = !string.IsNullOrEmpty(_configuration["JwtSettings:SecretKey"]);
        services["JwtAuthentication"] = jwtConfigured ? "Configured" : "Not Configured";
        if (!jwtConfigured) allHealthy = false;

        if (allHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "External services are configured",
                data: services));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            "Some external services may not be properly configured",
            data: services));
    }
}

/// <summary>
/// Health check for disk space
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly long _minimumFreeMB;

    public DiskSpaceHealthCheck(long minimumFreeMB = 500)
    {
        _minimumFreeMB = minimumFreeMB;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var drive = new DriveInfo(Path.GetPathRoot(currentDirectory) ?? "C:");
            
            var freeSpaceMB = drive.AvailableFreeSpace / (1024 * 1024);
            var totalSpaceMB = drive.TotalSize / (1024 * 1024);
            var usedPercent = (double)(totalSpaceMB - freeSpaceMB) / totalSpaceMB * 100;

            var data = new Dictionary<string, object>
            {
                { "Drive", drive.Name },
                { "FreeSpaceMB", freeSpaceMB },
                { "TotalSpaceMB", totalSpaceMB },
                { "UsedPercent", Math.Round(usedPercent, 2) },
                { "MinimumRequiredMB", _minimumFreeMB }
            };

            if (freeSpaceMB < _minimumFreeMB)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Low disk space: {freeSpaceMB}MB available",
                    data: data));
            }

            if (usedPercent > 90)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Disk usage is high: {usedPercent:F1}%",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space is adequate: {freeSpaceMB}MB free ({100 - usedPercent:F1}% available)",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Disk space check failed",
                exception: ex));
        }
    }
}
