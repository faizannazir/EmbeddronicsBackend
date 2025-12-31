using System.Collections.Concurrent;
using EmbeddronicsBackend.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EmbeddronicsBackend.Services;

/// <summary>
/// Interface for rate limiting chat messages
/// </summary>
public interface IChatRateLimitService
{
    /// <summary>
    /// Check if user can send a message (rate limit check)
    /// </summary>
    Task<(bool Allowed, string? Reason, int? RetryAfterSeconds)> CanSendMessageAsync(int userId);

    /// <summary>
    /// Record a message sent by user
    /// </summary>
    Task RecordMessageSentAsync(int userId);

    /// <summary>
    /// Get current rate limit status for a user
    /// </summary>
    Task<RateLimitStatusDto> GetRateLimitStatusAsync(int userId);

    /// <summary>
    /// Reset rate limit for a user (admin action)
    /// </summary>
    Task ResetRateLimitAsync(int userId);

    /// <summary>
    /// Configure rate limits for a specific user (e.g., VIP users)
    /// </summary>
    Task SetUserRateLimitAsync(int userId, int messagesPerMinute, int messagesPerHour);
}

/// <summary>
/// DTO for rate limit status
/// </summary>
public class RateLimitStatusDto
{
    public int UserId { get; set; }
    public int MessagesInLastMinute { get; set; }
    public int MessagesInLastHour { get; set; }
    public int MaxMessagesPerMinute { get; set; }
    public int MaxMessagesPerHour { get; set; }
    public bool IsLimited { get; set; }
    public DateTime? LimitedUntil { get; set; }
    public int? RetryAfterSeconds { get; set; }
}

/// <summary>
/// Service for rate limiting chat messages to prevent spam
/// </summary>
public class ChatRateLimitService : IChatRateLimitService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    // Default rate limits
    private const int DefaultMessagesPerMinute = 20;
    private const int DefaultMessagesPerHour = 200;
    private const int AdminMessagesPerMinute = 60;
    private const int AdminMessagesPerHour = 600;
    
    // Track message timestamps per user
    private static readonly ConcurrentDictionary<int, List<DateTime>> _userMessageTimestamps = new();
    
    // Track user-specific rate limits
    private static readonly ConcurrentDictionary<int, (int PerMinute, int PerHour)> _userRateLimits = new();
    
    // Track temporary blocks (e.g., for repeated violations)
    private static readonly ConcurrentDictionary<int, DateTime> _temporaryBlocks = new();

    public ChatRateLimitService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<(bool Allowed, string? Reason, int? RetryAfterSeconds)> CanSendMessageAsync(int userId)
    {
        // Check for temporary block
        if (_temporaryBlocks.TryGetValue(userId, out var blockedUntil))
        {
            if (DateTime.UtcNow < blockedUntil)
            {
                var retryAfter = (int)(blockedUntil - DateTime.UtcNow).TotalSeconds;
                return (false, "You have been temporarily blocked due to excessive messaging. Please wait.", retryAfter);
            }
            else
            {
                _temporaryBlocks.TryRemove(userId, out _);
            }
        }

        // Get rate limits for user
        var (maxPerMinute, maxPerHour) = await GetUserRateLimitsAsync(userId);

        // Get current message count
        var now = DateTime.UtcNow;
        var timestamps = _userMessageTimestamps.GetOrAdd(userId, _ => new List<DateTime>());

        // Clean old timestamps
        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < now.AddHours(-1));
        }

        // Count messages
        int messagesInLastMinute;
        int messagesInLastHour;
        
        lock (timestamps)
        {
            messagesInLastMinute = timestamps.Count(t => t > now.AddMinutes(-1));
            messagesInLastHour = timestamps.Count;
        }

        // Check per-minute limit
        if (messagesInLastMinute >= maxPerMinute)
        {
            var oldestInMinute = timestamps.Where(t => t > now.AddMinutes(-1)).Min();
            var retryAfter = (int)(oldestInMinute.AddMinutes(1) - now).TotalSeconds + 1;
            
            Log.Warning("User {UserId} exceeded per-minute rate limit: {Count}/{Max}", 
                userId, messagesInLastMinute, maxPerMinute);
            
            return (false, $"Rate limit exceeded. Maximum {maxPerMinute} messages per minute.", retryAfter);
        }

        // Check per-hour limit
        if (messagesInLastHour >= maxPerHour)
        {
            var oldestInHour = timestamps.Min();
            var retryAfter = (int)(oldestInHour.AddHours(1) - now).TotalSeconds + 1;
            
            // Apply temporary block for repeated violations
            if (messagesInLastHour >= maxPerHour * 1.5)
            {
                _temporaryBlocks[userId] = now.AddMinutes(15);
                Log.Warning("User {UserId} temporarily blocked for excessive messaging", userId);
            }
            
            Log.Warning("User {UserId} exceeded per-hour rate limit: {Count}/{Max}", 
                userId, messagesInLastHour, maxPerHour);
            
            return (false, $"Rate limit exceeded. Maximum {maxPerHour} messages per hour.", retryAfter);
        }

        return (true, null, null);
    }

    public Task RecordMessageSentAsync(int userId)
    {
        var timestamps = _userMessageTimestamps.GetOrAdd(userId, _ => new List<DateTime>());
        
        lock (timestamps)
        {
            timestamps.Add(DateTime.UtcNow);
        }

        return Task.CompletedTask;
    }

    public async Task<RateLimitStatusDto> GetRateLimitStatusAsync(int userId)
    {
        var (maxPerMinute, maxPerHour) = await GetUserRateLimitsAsync(userId);
        var now = DateTime.UtcNow;
        
        var timestamps = _userMessageTimestamps.GetOrAdd(userId, _ => new List<DateTime>());
        
        int messagesInLastMinute;
        int messagesInLastHour;
        
        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < now.AddHours(-1));
            messagesInLastMinute = timestamps.Count(t => t > now.AddMinutes(-1));
            messagesInLastHour = timestamps.Count;
        }

        var isLimited = messagesInLastMinute >= maxPerMinute || messagesInLastHour >= maxPerHour;
        DateTime? limitedUntil = null;
        int? retryAfter = null;

        if (_temporaryBlocks.TryGetValue(userId, out var blockedUntil) && blockedUntil > now)
        {
            isLimited = true;
            limitedUntil = blockedUntil;
            retryAfter = (int)(blockedUntil - now).TotalSeconds;
        }
        else if (messagesInLastMinute >= maxPerMinute && timestamps.Any())
        {
            var oldestInMinute = timestamps.Where(t => t > now.AddMinutes(-1)).Min();
            limitedUntil = oldestInMinute.AddMinutes(1);
            retryAfter = (int)(limitedUntil.Value - now).TotalSeconds + 1;
        }

        return new RateLimitStatusDto
        {
            UserId = userId,
            MessagesInLastMinute = messagesInLastMinute,
            MessagesInLastHour = messagesInLastHour,
            MaxMessagesPerMinute = maxPerMinute,
            MaxMessagesPerHour = maxPerHour,
            IsLimited = isLimited,
            LimitedUntil = limitedUntil,
            RetryAfterSeconds = retryAfter
        };
    }

    public Task ResetRateLimitAsync(int userId)
    {
        _userMessageTimestamps.TryRemove(userId, out _);
        _temporaryBlocks.TryRemove(userId, out _);
        
        Log.Information("Rate limit reset for user {UserId}", userId);
        
        return Task.CompletedTask;
    }

    public Task SetUserRateLimitAsync(int userId, int messagesPerMinute, int messagesPerHour)
    {
        _userRateLimits[userId] = (messagesPerMinute, messagesPerHour);
        
        Log.Information("Custom rate limit set for user {UserId}: {PerMinute}/min, {PerHour}/hour", 
            userId, messagesPerMinute, messagesPerHour);
        
        return Task.CompletedTask;
    }

    private async Task<(int PerMinute, int PerHour)> GetUserRateLimitsAsync(int userId)
    {
        // Check for custom rate limits
        if (_userRateLimits.TryGetValue(userId, out var customLimits))
        {
            return customLimits;
        }

        // Get user role to determine default limits
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmbeddronicsDbContext>();

        var user = await context.Users.FindAsync(userId);
        
        if (user?.Role == "admin")
        {
            return (AdminMessagesPerMinute, AdminMessagesPerHour);
        }

        return (DefaultMessagesPerMinute, DefaultMessagesPerHour);
    }
}
