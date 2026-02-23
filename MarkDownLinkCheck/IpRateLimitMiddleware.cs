using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace MarkDownLinkCheck;

/// <summary>
/// Middleware for IP-based rate limiting (FR-036: max 5 requests per minute per IP)
/// </summary>
public class IpRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, RequestLog> _requestLogs = new();
    private const int MaxRequestsPerMinute = 5;
    private const int TimeWindowSeconds = 60;

    public IpRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply rate limiting to the /api/check/sse endpoint
        if (!context.Request.Path.StartsWithSegments("/api/check/sse"))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        if (string.IsNullOrEmpty(clientIp))
        {
            await _next(context);
            return;
        }

        // Clean up old logs periodically
        CleanupOldLogs();

        var now = DateTimeOffset.UtcNow;
        var requestLog = _requestLogs.GetOrAdd(clientIp, _ => new RequestLog());

        bool isRateLimited;
        int retryAfter = 0;

        lock (requestLog)
        {
            // Remove requests older than the time window
            requestLog.Timestamps.RemoveAll(t => (now - t).TotalSeconds > TimeWindowSeconds);

            // Check if rate limit is exceeded
            if (requestLog.Timestamps.Count >= MaxRequestsPerMinute)
            {
                var oldestRequest = requestLog.Timestamps[0];
                retryAfter = (int)Math.Ceiling(TimeWindowSeconds - (now - oldestRequest).TotalSeconds);
                isRateLimited = true;
            }
            else
            {
                // Add current request timestamp
                requestLog.Timestamps.Add(now);
                isRateLimited = false;
            }
        }

        if (isRateLimited)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = "請求過於頻繁，請稍後再試",
                message = $"已超過速率限制（每分鐘最多 {MaxRequestsPerMinute} 次請求）",
                retryAfter = retryAfter
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return;
        }

        await _next(context);
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return ips[0].Trim();
        }

        // Fallback to remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static void CleanupOldLogs()
    {
        // Clean up every 100 requests (simple approach)
        if (_requestLogs.Count > 100 && Random.Shared.Next(100) == 0)
        {
            var now = DateTimeOffset.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _requestLogs)
            {
                lock (kvp.Value)
                {
                    kvp.Value.Timestamps.RemoveAll(t => (now - t).TotalSeconds > TimeWindowSeconds * 2);
                    
                    if (kvp.Value.Timestamps.Count == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _requestLogs.TryRemove(key, out _);
            }
        }
    }

    private class RequestLog
    {
        public List<DateTimeOffset> Timestamps { get; } = new();
    }
}
