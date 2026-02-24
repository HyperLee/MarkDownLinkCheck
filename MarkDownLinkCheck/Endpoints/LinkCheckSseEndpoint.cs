using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MarkDownLinkCheck;

/// <summary>
/// SSE endpoint for link checking
/// </summary>
public static class LinkCheckSseEndpoint
{
    public static async Task HandleAsync(HttpContext context, ILinkCheckOrchestrator orchestrator)
    {
        // Only accept POST requests
        if (context.Request.Method != "POST")
        {
            context.Response.StatusCode = 405; // Method Not Allowed
            await context.Response.WriteAsJsonAsync(new { message = "Only POST method is allowed" });
            return;
        }

        CheckRequest? request;

        try
        {
            // Deserialize request body
            request = await context.Request.ReadFromJsonAsync<CheckRequest>(context.RequestAborted);
            
            if (request == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid request body" });
                return;
            }

            // Set system fields
            request.RequestedAt = DateTimeOffset.UtcNow;
            request.SourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Validate request
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(request);
            
            if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            {
                var errors = string.Join("; ", validationResults.Select(v => v.ErrorMessage));
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { message = errors });
                return;
            }

            // Additional conditional validation using custom Validate method
            var customErrors = request.Validate().ToList();
            if (customErrors.Any())
            {
                var errorMessage = string.Join("; ", customErrors);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { message = errorMessage });
                return;
            }
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid JSON format" });
            return;
        }

        // Set SSE headers
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            // Stream progress events
            await foreach (var progress in orchestrator.ExecuteAsync(request, context.RequestAborted))
            {
                await WriteSSEEvent(context, progress.EventType, progress, jsonOptions);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - this is normal, just log and return
            return;
        }
        catch (Exception)
        {
            // Send error event
            var errorProgress = new CheckProgress
            {
                EventType = "error",
                ErrorMessage = "系統發生錯誤，請稍後再試"
            };

            await WriteSSEEvent(context, "error", errorProgress, jsonOptions);
            await context.Response.Body.FlushAsync();
        }
    }

    private static async Task WriteSSEEvent(HttpContext context, string eventType, CheckProgress data, JsonSerializerOptions jsonOptions)
    {
        await context.Response.WriteAsync($"event: {eventType}\n");
        
        var json = JsonSerializer.Serialize(data, jsonOptions);
        await context.Response.WriteAsync($"data: {json}\n\n");
    }
}
