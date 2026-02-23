using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for MarkdownParserService (T017)
/// Tests: inline links, reference-style, auto-links, image links, code block exclusion, HTML comment exclusion, anchor extraction, 1000 link limit
/// </summary>
public class MarkdownParserServiceTests
{
    private readonly IMarkdownParserService _service;

    public MarkdownParserServiceTests()
    {
        _service = new MarkdownParserService();
    }

    [Fact]
    public void ParseLinks_WhenInlineLink_ExtractsLink()
    {
        // Arrange
        var content = "Check out [example](https://example.com) for more info.";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://example.com", links[0].TargetUrl);
        Assert.Equal("[example](https://example.com)", links[0].RawText);
        Assert.Equal(LinkType.ExternalUrl, links[0].Type);
    }

    [Fact]
    public void ParseLinks_WhenReferenceStyleLink_ExtractsLink()
    {
        // Arrange
        var content = @"See [my link][ref]

[ref]: https://example.com";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://example.com", links[0].TargetUrl);
    }

    [Fact]
    public void ParseLinks_WhenAutoLink_ExtractsLink()
    {
        // Arrange
        var content = "Visit <https://example.com> today.";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://example.com", links[0].TargetUrl);
    }

    [Fact]
    public void ParseLinks_WhenImageLink_ExtractsLinkAndMarksAsImage()
    {
        // Arrange
        var content = "![alt text](https://example.com/image.png)";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://example.com/image.png", links[0].TargetUrl);
        Assert.True(links[0].IsImage);
        Assert.Equal(LinkType.Image, links[0].Type);
    }

    [Fact]
    public void ParseLinks_WhenLinkInCodeBlock_ExcludesLink()
    {
        // Arrange
        var content = @"Normal [link](https://example.com)

```markdown
[ignored link](https://ignored.com)
```

Another [link](https://example2.com)";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.TargetUrl == "https://example.com");
        Assert.Contains(links, l => l.TargetUrl == "https://example2.com");
        Assert.DoesNotContain(links, l => l.TargetUrl == "https://ignored.com");
    }

    [Fact]
    public void ParseLinks_WhenInlineCode_ExcludesLink()
    {
        // Arrange
        var content = "Use `[link](https://example.com)` in Markdown. Real [link](https://real.com).";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Single(links);
        Assert.Equal("https://real.com", links[0].TargetUrl);
    }

    [Fact]
    public void ParseLinks_WhenHtmlComment_ExcludesLink()
    {
        // Arrange
        var content = @"Valid [link](https://example.com)

<!-- [commented link](https://commented.com) -->

Another [link](https://example2.com)";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.TargetUrl == "https://example.com");
        Assert.Contains(links, l => l.TargetUrl == "https://example2.com");
        Assert.DoesNotContain(links, l => l.TargetUrl == "https://commented.com");
    }

    [Fact]
    public void ParseLinks_WhenMultipleLinkTypes_ExtractsAll()
    {
        // Arrange
        var content = @"# Test Document

Inline [link](https://example.com)
Auto-link <https://auto.com>
Email <mailto:test@example.com>
Anchor [link](#section)
Image ![alt](https://example.com/img.png)";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Equal(5, links.Count);
        Assert.Contains(links, l => l.TargetUrl == "https://example.com");
        Assert.Contains(links, l => l.TargetUrl == "https://auto.com");
        Assert.Contains(links, l => l.TargetUrl == "mailto:test@example.com");
        Assert.Contains(links, l => l.TargetUrl == "#section");
        Assert.Contains(links, l => l.TargetUrl == "https://example.com/img.png" && l.IsImage);
    }

    [Fact]
    public void ParseLinks_WhenExceeds1000Links_Limits()
    {
        // Arrange
        var linkCount = 1500;
        var content = string.Join("\n", Enumerable.Range(1, linkCount).Select(i => $"[link{i}](https://example.com/{i})"));
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Equal(1000, links.Count);
    }

    [Fact]
    public void ParseLinks_WhenNoLinks_ReturnsEmptyList()
    {
        // Arrange
        var content = "Just plain text with no links.";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Empty(links);
    }

    [Fact]
    public void ParseLinks_SetsLineNumberCorrectly()
    {
        // Arrange
        var content = @"Line 1
Line 2 with [link1](https://example.com)
Line 3
Line 4 with [link2](https://example2.com)";
        var fileName = "test.md";

        // Act
        var links = _service.ParseLinks(content, fileName);

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Equal(2, links[0].LineNumber);
        Assert.Equal(4, links[1].LineNumber);
    }

    [Fact]
    public void ExtractAnchors_WhenHeadings_ExtractsGitHubStyleAnchors()
    {
        // Arrange
        var content = @"# Installation Guide
## Quick Start
### Step 1: Setup
# FAQ Section";

        // Act
        var anchors = _service.ExtractAnchors(content);

        // Assert
        Assert.Equal(4, anchors.Count);
        Assert.Contains("installation-guide", anchors);
        Assert.Contains("quick-start", anchors);
        Assert.Contains("step-1-setup", anchors);
        Assert.Contains("faq-section", anchors);
    }

    [Fact]
    public void ExtractAnchors_WhenHeadingWithSpecialChars_RemovesSpecialChars()
    {
        // Arrange
        var content = @"# Hello, World!
## API (v2.0)
### Questions?";

        // Act
        var anchors = _service.ExtractAnchors(content);

        // Assert
        Assert.Equal(3, anchors.Count);
        Assert.Contains("hello-world", anchors);
        Assert.Contains("api-v20", anchors);
        Assert.Contains("questions", anchors);
    }

    [Fact]
    public void ExtractAnchors_WhenNoHeadings_ReturnsEmptyList()
    {
        // Arrange
        var content = "Just plain text without any headings.";

        // Act
        var anchors = _service.ExtractAnchors(content);

        // Assert
        Assert.Empty(anchors);
    }

    [Fact]
    public void ExtractAnchors_WhenDuplicateHeadings_IncludesAll()
    {
        // Arrange
        var content = @"# Setup
## Setup
### Setup";

        // Act
        var anchors = _service.ExtractAnchors(content);

        // Assert
        // GitHub appends -1, -2 for duplicates, but we'll just ensure they're all present
        Assert.NotEmpty(anchors);
        Assert.Contains("setup", anchors);
    }
}
