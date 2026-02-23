using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using Moq;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for LinkCheckOrchestrator (T021)
/// Tests: MarkdownSource mode, progress events, URL deduplication, 5000 link limit, 100000 char limit
/// </summary>
public class LinkCheckOrchestratorTests
{
    private readonly Mock<IMarkdownParserService> _parserMock;
    private readonly Mock<ILinkValidatorService> _validatorMock;
    private readonly Mock<IReportGeneratorService> _reportGeneratorMock;
    private readonly Mock<LinkCheckOptions> _optionsMock;
    private readonly ILinkCheckOrchestrator _orchestrator;

    public LinkCheckOrchestratorTests()
    {
        _parserMock = new Mock<IMarkdownParserService>();
        _validatorMock = new Mock<ILinkValidatorService>();
        _reportGeneratorMock = new Mock<IReportGeneratorService>();
        _optionsMock = new Mock<LinkCheckOptions>();
        
        _optionsMock.Object.MaxMarkdownLength = 100000;
        _optionsMock.Object.MaxLinksPerFile = 1000;
        _optionsMock.Object.MaxLinksPerCheck = 5000;
        
        _orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            _reportGeneratorMock.Object,
            null!, // IGitHubRepoService not needed for MarkdownSource mode
            _optionsMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMarkdownSourceMode_EmitsProgressEvents()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link](https://example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link>
        {
            CreateLink("https://example.com", LinkType.ExternalUrl)
        };

        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLinkResult(links[0], LinkStatus.Healthy));

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.EventType == "progress");
        Assert.Contains(events, e => e.EventType == "file-result");
        Assert.Contains(events, e => e.EventType == "complete");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMarkdownSourceMode_CreatesInputMdFile()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link](https://example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link> { CreateLink("https://example.com", LinkType.ExternalUrl) };
        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLinkResult(links[0], LinkStatus.Healthy));

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            if (progress.EventType == "file-result" && progress.FileResult != null)
            {
                // Assert
                Assert.Equal("input.md", progress.FileResult.File.FileName);
                Assert.Equal("input.md", progress.FileResult.File.RelativePath);
            }
        }

        // Verify parser was called with correct fileName
        _parserMock.Verify(p => p.ParseLinks(It.IsAny<string>(), "input.md"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeduplicateExternalUrls_ValidatesOnlyOnce()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link1](https://example.com)\n[link2](https://example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link>
        {
            CreateLink("https://example.com", LinkType.ExternalUrl),
            CreateLink("https://example.com", LinkType.ExternalUrl)
        };

        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLinkResult(links[0], LinkStatus.Healthy));

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            // Just consume the events
        }

        // Assert
        // Validator should be called only once for the duplicate URL
        _validatorMock.Verify(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceeds5000Links_EmitsError()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = Enumerable.Range(1, 5001)
            .Select(i => CreateLink($"https://example.com/{i}", LinkType.ExternalUrl))
            .ToList();

        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "error" && e.ErrorMessage != null && e.ErrorMessage.Contains("5000"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceeds100000Chars_EmitsError()
    {
        // Arrange
        var content = new string('a', 100001);
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = content,
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "error" && e.ErrorMessage != null && e.ErrorMessage.Contains("100000"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationComplete_EmitsCompleteEvent()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link](https://example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link> { CreateLink("https://example.com", LinkType.ExternalUrl) };
        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLinkResult(links[0], LinkStatus.Healthy));

        var report = new CheckReport
        {
            FileCount = 1,
            TotalLinkCount = 1,
            HealthyCount = 1,
            BrokenCount = 0,
            WarningCount = 0,
            SkippedCount = 0,
            TotalDuration = TimeSpan.FromSeconds(1)
        };
        _reportGeneratorMock.Setup(r => r.GenerateReport(It.IsAny<IReadOnlyList<FileCheckResult>>(), It.IsAny<TimeSpan>()))
            .Returns(report);

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        var completeEvent = events.LastOrDefault(e => e.EventType == "complete");
        Assert.NotNull(completeEvent);
        Assert.NotNull(completeEvent.Report);
        Assert.Equal(1, completeEvent.Report.FileCount);
        Assert.Equal(1, completeEvent.Report.TotalLinkCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_StopsProcessing()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link](https://example.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link> { CreateLink("https://example.com", LinkType.ExternalUrl) };
        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var progress in _orchestrator.ExecuteAsync(request, cts.Token))
            {
                // Should not get here
            }
        });
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesProgressWithCheckedCount()
    {
        // Arrange
        var request = new CheckRequest
        {
            Mode = CheckMode.MarkdownSource,
            MarkdownContent = "# Test\n[link1](https://example1.com)\n[link2](https://example2.com)",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var links = new List<Link>
        {
            CreateLink("https://example1.com", LinkType.ExternalUrl),
            CreateLink("https://example2.com", LinkType.ExternalUrl)
        };

        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), "input.md")).Returns(links);
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), null, It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Link l, IReadOnlySet<string>? r, IReadOnlyDictionary<string, IReadOnlyList<string>>? a, CancellationToken ct) => CreateLinkResult(l, LinkStatus.Healthy));

        var progressEvents = new List<CheckProgress>();

        // Act
        await foreach (var progress in _orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            if (progress.EventType == "progress")
            {
                progressEvents.Add(progress);
            }
        }

        // Assert
        Assert.NotEmpty(progressEvents);
        // Should have progress updates
        Assert.Contains(progressEvents, p => p.TotalCount == 2);
    }

    private Link CreateLink(string url, LinkType type)
    {
        var markdownFile = new MarkdownFile
        {
            FileName = "input.md",
            RelativePath = "input.md",
            Content = ""
        };

        return new Link
        {
            TargetUrl = url,
            RawText = $"[link]({url})",
            Type = type,
            LineNumber = 1,
            SourceFile = markdownFile,
            IsImage = type == LinkType.Image
        };
    }

    private LinkResult CreateLinkResult(Link link, LinkStatus status)
    {
        return new LinkResult
        {
            Link = link,
            Status = status,
            Duration = TimeSpan.FromMilliseconds(100)
        };
    }

    #region T040 - RepoUrl Mode Tests

    [Fact]
    public async Task ExecuteAsync_WhenRepoUrlMode_ScansMultipleFiles()
    {
        // Arrange
        var gitHubRepoServiceMock = new Mock<IGitHubRepoService>();
        var orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            _reportGeneratorMock.Object,
            gitHubRepoServiceMock.Object,
            _optionsMock.Object);

        var request = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        gitHubRepoServiceMock.Setup(g => g.ValidateRepoUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("owner", "repo"));
        gitHubRepoServiceMock.Setup(g => g.GetDefaultBranchAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        gitHubRepoServiceMock.Setup(g => g.ListMarkdownFilesAsync("owner", "repo", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "README.md", "docs/guide.md" });
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# README\n[link](https://example.com)");
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "docs/guide.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Guide\n[link2](https://example.org)");

        _parserMock.Setup(p => p.ParseLinks(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string content, string fileName) => new List<Link>
            {
                CreateLink($"https://{(fileName.Contains("README") ? "example.com" : "example.org")}", LinkType.ExternalUrl)
            });
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Link l, IReadOnlySet<string>? r, IReadOnlyDictionary<string, IReadOnlyList<string>>? a, CancellationToken ct) => CreateLinkResult(l, LinkStatus.Healthy));

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "progress" && e.CurrentFile != null && e.CurrentFile.Contains("找到"));
        Assert.Contains(events, e => e.EventType == "complete");
        gitHubRepoServiceMock.Verify(g => g.GetFileContentAsync("owner", "repo", "main", "README.md", It.IsAny<CancellationToken>()), Times.Once);
        gitHubRepoServiceMock.Verify(g => g.GetFileContentAsync("owner", "repo", "main", "docs/guide.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepoUrlMode_ValidatesRelativePathInContext()
    {
        // Arrange
        var gitHubRepoServiceMock = new Mock<IGitHubRepoService>();
        var reportGenMock = new Mock<IReportGeneratorService>();
        var orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            reportGenMock.Object,
            gitHubRepoServiceMock.Object,
            _optionsMock.Object);

        var request = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        gitHubRepoServiceMock.Setup(g => g.ValidateRepoUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("owner", "repo"));
        gitHubRepoServiceMock.Setup(g => g.GetDefaultBranchAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        gitHubRepoServiceMock.Setup(g => g.ListMarkdownFilesAsync("owner", "repo", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "README.md", "docs/guide.md" });
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# README\n[guide](./docs/guide.md)");
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "docs/guide.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Guide");

        var relativeLinkCalled = false;
        _parserMock.Setup(p => p.ParseLinks("# README\n[guide](./docs/guide.md)", "README.md"))
            .Returns(() =>
            {
                relativeLinkCalled = true;
                return new List<Link> { CreateLink("./docs/guide.md", LinkType.RelativePath) };
            });
        _parserMock.Setup(p => p.ParseLinks("# Guide", "docs/guide.md"))
            .Returns(new List<Link>());
        _parserMock.Setup(p => p.ExtractAnchors(It.IsAny<string>())).Returns(new List<string>());
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Link l, IReadOnlySet<string>? r, IReadOnlyDictionary<string, IReadOnlyList<string>>? a, CancellationToken ct) => CreateLinkResult(l, LinkStatus.Healthy));
        reportGenMock.Setup(r => r.GenerateReport(It.IsAny<IReadOnlyList<FileCheckResult>>(), It.IsAny<TimeSpan>()))
            .Returns(new CheckReport { FileCount = 2, TotalLinkCount = 1 });

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "complete");
        Assert.True(relativeLinkCalled, "ParseLinks should be called for relative link validation");
        // Verify the orchestrator fetched both files
        gitHubRepoServiceMock.Verify(g => g.GetFileContentAsync("owner", "repo", "main", "README.md", It.IsAny<CancellationToken>()), Times.Once);
        gitHubRepoServiceMock.Verify(g => g.GetFileContentAsync("owner", "repo", "main", "docs/guide.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepoUrlMode_ValidatesCrossFileAnchor()
    {
        // Arrange
        var gitHubRepoServiceMock = new Mock<IGitHubRepoService>();
        var reportGenMock = new Mock<IReportGeneratorService>();
        var orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            reportGenMock.Object,
            gitHubRepoServiceMock.Object,
            _optionsMock.Object);

        var request = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        gitHubRepoServiceMock.Setup(g => g.ValidateRepoUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("owner", "repo"));
        gitHubRepoServiceMock.Setup(g => g.GetDefaultBranchAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        gitHubRepoServiceMock.Setup(g => g.ListMarkdownFilesAsync("owner", "repo", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "README.md", "docs/guide.md" });
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# README\n[guide](./docs/guide.md#installation)");
        gitHubRepoServiceMock.Setup(g => g.GetFileContentAsync("owner", "repo", "main", "docs/guide.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Guide\n## Installation");

        var anchorsExtracted = 0;
        _parserMock.Setup(p => p.ParseLinks("# README\n[guide](./docs/guide.md#installation)", "README.md"))
            .Returns(new List<Link> { CreateLink("./docs/guide.md#installation", LinkType.RelativePath) });
        _parserMock.Setup(p => p.ParseLinks("# Guide\n## Installation", "docs/guide.md"))
            .Returns(new List<Link>());
        _parserMock.Setup(p => p.ExtractAnchors("# README\n[guide](./docs/guide.md#installation)"))
            .Returns(() => { anchorsExtracted++; return new List<string> { "readme" }; });
        _parserMock.Setup(p => p.ExtractAnchors("# Guide\n## Installation"))
            .Returns(() => { anchorsExtracted++; return new List<string> { "guide", "installation" }; });
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Link>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Link l, IReadOnlySet<string>? r, IReadOnlyDictionary<string, IReadOnlyList<string>>? a, CancellationToken ct) => CreateLinkResult(l, LinkStatus.Healthy));
        reportGenMock.Setup(r => r.GenerateReport(It.IsAny<IReadOnlyList<FileCheckResult>>(), It.IsAny<TimeSpan>()))
            .Returns(new CheckReport { FileCount = 2, TotalLinkCount = 1 });

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "complete");
        Assert.Equal(2, anchorsExtracted); // Should extract anchors from both files
        // Verify the orchestrator processed both files
        _parserMock.Verify(p => p.ExtractAnchors("# README\n[guide](./docs/guide.md#installation)"), Times.Once);
        _parserMock.Verify(p => p.ExtractAnchors("# Guide\n## Installation"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepoUrlMode_ExceedsFileLimit_EmitsError()
    {
        // Arrange
        var gitHubRepoServiceMock = new Mock<IGitHubRepoService>();
        var orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            _reportGeneratorMock.Object,
            gitHubRepoServiceMock.Object,
            _optionsMock.Object);

        var request = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        var files = Enumerable.Range(1, 501).Select(i => $"file{i}.md").ToList();

        gitHubRepoServiceMock.Setup(g => g.ValidateRepoUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("owner", "repo"));
        gitHubRepoServiceMock.Setup(g => g.GetDefaultBranchAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        gitHubRepoServiceMock.Setup(g => g.ListMarkdownFilesAsync("owner", "repo", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "error" && e.ErrorMessage != null && e.ErrorMessage.Contains("500"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepoUrlMode_GitHubRateLimitError_EmitsError()
    {
        // Arrange
        var gitHubRepoServiceMock = new Mock<IGitHubRepoService>();
        var orchestrator = new LinkCheckOrchestrator(
            _parserMock.Object,
            _validatorMock.Object,
            _reportGeneratorMock.Object,
            gitHubRepoServiceMock.Object,
            _optionsMock.Object);

        var request = new CheckRequest
        {
            Mode = CheckMode.RepoUrl,
            RepoUrl = "https://github.com/owner/repo",
            RequestedAt = DateTimeOffset.Now,
            SourceIp = "127.0.0.1"
        };

        gitHubRepoServiceMock.Setup(g => g.ValidateRepoUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("owner", "repo"));
        gitHubRepoServiceMock.Setup(g => g.GetDefaultBranchAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("main");
        gitHubRepoServiceMock.Setup(g => g.ListMarkdownFilesAsync("owner", "repo", "main", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub API rate limit exceeded"));

        var events = new List<CheckProgress>();

        // Act
        await foreach (var progress in orchestrator.ExecuteAsync(request, CancellationToken.None))
        {
            events.Add(progress);
        }

        // Assert
        Assert.Contains(events, e => e.EventType == "error" && e.ErrorMessage != null && e.ErrorMessage.Contains("rate limit"));
    }

    #endregion
}
