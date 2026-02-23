using MarkDownLinkCheck.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MarkDownLinkCheck.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for LinkCheckSseEndpoint (T045b, T051)
/// Tests: SSRF blocking via POST /api/check/sse, full SSE pipeline, validation errors
/// </summary>
public class LinkCheckSseEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LinkCheckSseEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region T045b - SSRF Protection Integration Tests

    [Theory]
    [InlineData("http://127.0.0.1/admin", "127.0.0.1")]
    [InlineData("http://localhost/config", "localhost")]
    [InlineData("http://10.0.0.1/internal", "10.0.0.1")]
    [InlineData("http://192.168.1.1/router", "192.168.1.1")]
    [InlineData("http://172.16.0.1/service", "172.16.0.1")]
    public async Task PostCheckSse_WhenMarkdownContainsPrivateIpUrl_ReturnsSSRFBlockedError(string privateUrl, string description)
    {
        // Arrange
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = $"# Test\n[private link]({privateUrl})",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var sseContent = await response.Content.ReadAsStringAsync();
        
        // Should receive events indicating SSRF was blocked
        Assert.Contains("data:", sseContent);
        
        // Parse SSE events
        var events = ParseSseEvents(sseContent);
        
        // Should have at least one file-result or error event mentioning the block
        var hasBlockedOrBroken = events.Any(e => 
            (e.EventType == "file-result" && e.Data != null && 
             (e.Data.Contains("\"status\":\"Broken\"") || 
              e.Data.Contains("ssrf") || 
              e.Data.Contains("private") ||
              e.Data.Contains("blocked"))) ||
            (e.EventType == "error" && e.Data != null && 
             (e.Data.Contains("private") || e.Data.Contains("blocked")))
        );

        Assert.True(hasBlockedOrBroken, 
            $"Expected SSRF blocking for {description}, but events did not indicate blocking. Events: {string.Join("; ", events.Select(e => e.EventType))}");
    }

    [Fact]
    public async Task PostCheckSse_WhenMarkdownContainsPublicUrl_ProcessesSuccessfully()
    {
        // Arrange
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[public link](https://www.example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var sseContent = await response.Content.ReadAsStringAsync();
        var events = ParseSseEvents(sseContent);

        // Should receive progress, file-result, and complete events
        Assert.Contains(events, e => e.EventType == "progress");
        Assert.Contains(events, e => e.EventType == "file-result");
        Assert.Contains(events, e => e.EventType == "complete");
    }

    #endregion

    #region T051 - SSE Pipeline Integration Tests

    [Fact]
    public async Task PostCheckSse_WhenValidRequest_ReturnsSSEStream()
    {
        // Arrange
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link](https://www.example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache ?? false);
    }

    [Fact]
    public async Task PostCheckSse_WhenEmptyMarkdownContent_ReturnsValidationError()
    {
        // Arrange
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        // Validation errors return 400 before SSE streaming starts
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("Markdown", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCheckSse_WhenContentExceedsLimit_ReturnsValidationError()
    {
        // Arrange
        var longContent = new string('a', 100001); // Exceeds 100000 char limit
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = longContent,
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        // Validation errors return 400 before SSE streaming starts
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        // Error message may contain "100,000" with comma formatting
        Assert.Contains("100", responseBody);
    }

    [Fact]
    public async Task PostCheckSse_WhenMultipleLinks_EmitsProgressEvents()
    {
        // Arrange
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link1](https://www.example.com)\n[link2](https://www.example.org)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var sseContent = await response.Content.ReadAsStringAsync();
        var events = ParseSseEvents(sseContent);

        // Should have multiple progress events
        var progressEvents = events.Where(e => e.EventType == "progress").ToList();
        Assert.NotEmpty(progressEvents);
        
        // Should have file-result event
        Assert.Contains(events, e => e.EventType == "file-result");
        
        // Should have complete event
        Assert.Contains(events, e => e.EventType == "complete");
    }

    #endregion

    #region Helper Methods

    private class SseEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string? Data { get; set; }
    }

    private List<SseEvent> ParseSseEvents(string sseContent)
    {
        var events = new List<SseEvent>();
        var lines = sseContent.Split('\n');
        
        string? currentEventType = null;
        var dataLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim('\r'); // Handle \r\n line endings
            
            if (trimmedLine.StartsWith("event:"))
            {
                currentEventType = trimmedLine.Substring(6).Trim();
            }
            else if (trimmedLine.StartsWith("data:"))
            {
                dataLines.Add(trimmedLine.Substring(5).Trim());
            }
            else if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // End of event (empty line)
                if (currentEventType != null && dataLines.Count > 0)
                {
                    events.Add(new SseEvent
                    {
                        EventType = currentEventType,
                        Data = string.Join("\n", dataLines)
                    });
                    currentEventType = null;
                    dataLines.Clear();
                }
            }
        }

        // Handle last event if no trailing empty line
        if (currentEventType != null && dataLines.Count > 0)
        {
            events.Add(new SseEvent
            {
                EventType = currentEventType,
                Data = string.Join("\n", dataLines)
            });
        }

        return events;
    }

    #endregion
}
