using MarkDownLinkCheck.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace MarkDownLinkCheck.Tests.Integration.Endpoints;

/// <summary>
/// Performance benchmark tests for LinkCheckSseEndpoint (T055a)
/// Tests: SC-001 (50 links Markdown mode < 30s), SC-002 (10 files / 200 links Repo mode < 2min)
/// </summary>
public class LinkCheckSsePerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public LinkCheckSsePerformanceTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    /// <summary>
    /// SC-001: Markdown mode with 50 links should complete in less than 30 seconds
    /// </summary>
    [Fact]
    public async Task PostCheckSse_MarkdownMode_50Links_CompletesInLessThan30Seconds()
    {
        // Arrange
        var markdown = GenerateMarkdownWithLinks(50);
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = markdown,
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);
        var sseContent = await response.Content.ReadAsStringAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var events = ParseSseEvents(sseContent);
        Assert.Contains(events, e => e.EventType == "complete");

        var elapsed = stopwatch.Elapsed;
        _output.WriteLine($"SC-001: 50 links completed in {elapsed.TotalSeconds:F2} seconds");
        
        // Performance requirement: should complete in less than 30 seconds
        Assert.True(elapsed.TotalSeconds < 30, 
            $"Expected completion in < 30 seconds, but took {elapsed.TotalSeconds:F2} seconds");
    }

    /// <summary>
    /// SC-002: Repo mode with 10 files and 200 links should complete in less than 2 minutes
    /// Note: This test uses a mock/test repo since we can't guarantee external GitHub availability
    /// In practice, this would test against a real public repository
    /// </summary>
    [Fact(Skip = "Requires external GitHub API access - run manually for actual performance testing")]
    public async Task PostCheckSse_RepoMode_10Files200Links_CompletesInLessThan2Minutes()
    {
        // Arrange
        // Note: Replace with actual test repository URL that has ~10 files and ~200 links
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/test-repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.1"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);
        var sseContent = await response.Content.ReadAsStringAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var events = ParseSseEvents(sseContent);
        Assert.Contains(events, e => e.EventType == "complete");

        var elapsed = stopwatch.Elapsed;
        _output.WriteLine($"SC-002: Repo mode completed in {elapsed.TotalSeconds:F2} seconds");
        
        // Performance requirement: should complete in less than 2 minutes (120 seconds)
        Assert.True(elapsed.TotalSeconds < 120, 
            $"Expected completion in < 120 seconds, but took {elapsed.TotalSeconds:F2} seconds");
    }

    /// <summary>
    /// Baseline test: Small markdown (5 links) should complete quickly
    /// </summary>
    [Fact]
    public async Task PostCheckSse_MarkdownMode_5Links_CompletesInLessThan10Seconds()
    {
        // Arrange
        var markdown = GenerateMarkdownWithLinks(5);
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = markdown,
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.2"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);
        var sseContent = await response.Content.ReadAsStringAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var events = ParseSseEvents(sseContent);
        Assert.Contains(events, e => e.EventType == "complete");

        var elapsed = stopwatch.Elapsed;
        _output.WriteLine($"Baseline: 5 links completed in {elapsed.TotalSeconds:F2} seconds");
        
        // Baseline: should be fast
        Assert.True(elapsed.TotalSeconds < 10, 
            $"Expected baseline completion in < 10 seconds, but took {elapsed.TotalSeconds:F2} seconds");
    }

    /// <summary>
    /// Stress test: 100 links (beyond SC-001 benchmark)
    /// </summary>
    [Fact(Skip = "Stress test - run manually")]
    public async Task PostCheckSse_MarkdownMode_100Links_PerformanceTest()
    {
        // Arrange
        var markdown = GenerateMarkdownWithLinks(100);
        var checkRequest = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = markdown,
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "203.0.113.3"
        };

        var json = JsonSerializer.Serialize(checkRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsync("/api/check/sse", content);
        var sseContent = await response.Content.ReadAsStringAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var events = ParseSseEvents(sseContent);
        Assert.Contains(events, e => e.EventType == "complete");

        var elapsed = stopwatch.Elapsed;
        _output.WriteLine($"Stress test: 100 links completed in {elapsed.TotalSeconds:F2} seconds");
        _output.WriteLine($"Average time per link: {elapsed.TotalMilliseconds / 100:F2} ms");
    }

    #region Helper Methods

    private string GenerateMarkdownWithLinks(int linkCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Performance Test Document");
        sb.AppendLine();
        sb.AppendLine("This document contains multiple links for performance testing.");
        sb.AppendLine();

        // Generate links to various public domains
        var domains = new[]
        {
            "example.com",
            "example.org",
            "example.net",
            "www.iana.org",
            "www.rfc-editor.org"
        };

        for (int i = 0; i < linkCount; i++)
        {
            var domain = domains[i % domains.Length];
            sb.AppendLine($"- [Link {i + 1}](https://{domain}/page{i})");
        }

        return sb.ToString();
    }

    private class SseEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string? Data { get; set; }
    }

    private List<SseEvent> ParseSseEvents(string sseContent)
    {
        var events = new List<SseEvent>();
        var lines = sseContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        string? currentEventType = null;
        var dataLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("event:"))
            {
                currentEventType = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataLines.Add(line.Substring(5).Trim());
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                // End of event
                if (currentEventType != null)
                {
                    events.Add(new SseEvent
                    {
                        EventType = currentEventType,
                        Data = dataLines.Count > 0 ? string.Join("\n", dataLines) : null
                    });
                }
                currentEventType = null;
                dataLines.Clear();
            }
        }

        // Handle last event if no trailing newline
        if (currentEventType != null)
        {
            events.Add(new SseEvent
            {
                EventType = currentEventType,
                Data = dataLines.Count > 0 ? string.Join("\n", dataLines) : null
            });
        }

        return events;
    }

    #endregion
}
