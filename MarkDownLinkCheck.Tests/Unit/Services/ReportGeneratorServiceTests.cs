using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for ReportGeneratorService (T020)
/// Tests: status counts, file grouping, Broken-first sorting
/// </summary>
public class ReportGeneratorServiceTests
{
    private readonly IReportGeneratorService _service;

    public ReportGeneratorServiceTests()
    {
        _service = new ReportGeneratorService();
    }

    [Fact]
    public void GenerateReport_WhenCalled_CalculatesCorrectStatusCounts()
    {
        // Arrange
        var fileResults = new List<FileCheckResult>
        {
            CreateFileResult("file1.md", 
                healthyCount: 5, 
                brokenCount: 2, 
                warningCount: 1, 
                skippedCount: 0),
            CreateFileResult("file2.md", 
                healthyCount: 3, 
                brokenCount: 1, 
                warningCount: 0, 
                skippedCount: 2)
        };
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        Assert.Equal(2, report.FileCount);
        Assert.Equal(14, report.TotalLinkCount);
        Assert.Equal(8, report.HealthyCount);
        Assert.Equal(3, report.BrokenCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(2, report.SkippedCount);
        Assert.Equal(duration, report.TotalDuration);
    }

    [Fact]
    public void GenerateReport_WhenNoFiles_ReturnsZeroCounts()
    {
        // Arrange
        var fileResults = new List<FileCheckResult>();
        var duration = TimeSpan.FromSeconds(0);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        Assert.Equal(0, report.FileCount);
        Assert.Equal(0, report.TotalLinkCount);
        Assert.Equal(0, report.HealthyCount);
        Assert.Equal(0, report.BrokenCount);
        Assert.Equal(0, report.WarningCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Fact]
    public void GenerateReport_WhenMultipleFiles_GroupsByFile()
    {
        // Arrange
        var fileResults = new List<FileCheckResult>
        {
            CreateFileResult("file1.md", healthyCount: 5, brokenCount: 0, warningCount: 0, skippedCount: 0),
            CreateFileResult("file2.md", healthyCount: 3, brokenCount: 1, warningCount: 0, skippedCount: 0),
            CreateFileResult("file3.md", healthyCount: 2, brokenCount: 2, warningCount: 1, skippedCount: 0)
        };
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        Assert.Equal(3, report.FileCount);
        Assert.Equal(3, report.FileResults.Count);
        Assert.Contains(report.FileResults, fr => fr.File.FileName == "file1.md");
        Assert.Contains(report.FileResults, fr => fr.File.FileName == "file2.md");
        Assert.Contains(report.FileResults, fr => fr.File.FileName == "file3.md");
    }

    [Fact]
    public void GenerateReport_SortsFilesBySeverity_BrokenFirst()
    {
        // Arrange
        var fileResults = new List<FileCheckResult>
        {
            CreateFileResult("all-healthy.md", healthyCount: 10, brokenCount: 0, warningCount: 0, skippedCount: 0),
            CreateFileResult("has-broken.md", healthyCount: 5, brokenCount: 3, warningCount: 0, skippedCount: 0),
            CreateFileResult("has-warnings.md", healthyCount: 5, brokenCount: 0, warningCount: 2, skippedCount: 0)
        };
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        Assert.Equal("has-broken.md", report.FileResults[0].File.FileName);
        Assert.Equal("has-warnings.md", report.FileResults[1].File.FileName);
        Assert.Equal("all-healthy.md", report.FileResults[2].File.FileName);
    }

    [Fact]
    public void GenerateReport_FileResult_ContainsSortedLinkResults()
    {
        // Arrange
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };

        var linkResults = new List<LinkResult>
        {
            CreateLinkResult("https://healthy.com", LinkStatus.Healthy),
            CreateLinkResult("https://broken.com", LinkStatus.Broken),
            CreateLinkResult("https://warning.com", LinkStatus.Warning),
            CreateLinkResult("https://skipped.com", LinkStatus.Skipped),
            CreateLinkResult("https://broken2.com", LinkStatus.Broken)
        };

        var markdownFileForResult = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };

        var fileResult = new FileCheckResult
        {
            File = markdownFileForResult,
            LinkResults = linkResults,
            HealthyCount = 1,
            BrokenCount = 2,
            WarningCount = 1,
            SkippedCount = 1
        };

        var fileResults = new List<FileCheckResult> { fileResult };
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        var sortedLinks = report.FileResults[0].LinkResults.ToList();
        Assert.Equal(LinkStatus.Broken, sortedLinks[0].Status);
        Assert.Equal(LinkStatus.Broken, sortedLinks[1].Status);
        Assert.Equal(LinkStatus.Warning, sortedLinks[2].Status);
        // Healthy and Skipped can be in any order
    }

    [Fact]
    public void GenerateReport_CalculatesTotalDuration()
    {
        // Arrange
        var fileResults = new List<FileCheckResult>
        {
            CreateFileResult("file1.md", healthyCount: 1, brokenCount: 0, warningCount: 0, skippedCount: 0)
        };
        var duration = TimeSpan.FromMilliseconds(12345);

        // Act
        var report = _service.GenerateReport(fileResults, duration);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(12345), report.TotalDuration);
    }

    private FileCheckResult CreateFileResult(string fileName, int healthyCount, int brokenCount, int warningCount, int skippedCount)
    {
        var linkResults = new List<LinkResult>();
        
        for (int i = 0; i < healthyCount; i++)
            linkResults.Add(CreateLinkResult($"https://healthy{i}.com", LinkStatus.Healthy));
        
        for (int i = 0; i < brokenCount; i++)
            linkResults.Add(CreateLinkResult($"https://broken{i}.com", LinkStatus.Broken));
        
        for (int i = 0; i < warningCount; i++)
            linkResults.Add(CreateLinkResult($"https://warning{i}.com", LinkStatus.Warning));
        
        for (int i = 0; i < skippedCount; i++)
            linkResults.Add(CreateLinkResult($"https://skipped{i}.com", LinkStatus.Skipped));

        var markdownFile = new MarkdownFile
        {
            FileName = fileName,
            RelativePath = fileName,
            Content = ""
        };

        return new FileCheckResult
        {
            File = markdownFile,
            LinkResults = linkResults,
            HealthyCount = healthyCount,
            BrokenCount = brokenCount,
            WarningCount = warningCount,
            SkippedCount = skippedCount
        };
    }

    private LinkResult CreateLinkResult(string url, LinkStatus status)
    {
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };

        return new LinkResult
        {
            Link = new Link
            {
                TargetUrl = url,
                RawText = $"[link]({url})",
                Type = LinkType.ExternalUrl,
                LineNumber = 1,
                SourceFile = markdownFile
            },
            Status = status,
            Duration = TimeSpan.FromMilliseconds(100)
        };
    }
}
