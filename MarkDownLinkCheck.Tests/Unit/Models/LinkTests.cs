using MarkDownLinkCheck.Models;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Models;

/// <summary>
/// Unit tests for Link model's type determination logic (T022)
/// </summary>
public class LinkTests
{
    [Theory]
    [InlineData("mailto:test@example.com", false, LinkType.Email)]
    [InlineData("MAILTO:TEST@EXAMPLE.COM", false, LinkType.Email)]
    public void DetermineLinkType_WhenMailtoUrl_ReturnsEmail(string targetUrl, bool isImage, LinkType expected)
    {
        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("#installation", false, LinkType.Anchor)]
    [InlineData("#", false, LinkType.Anchor)]
    [InlineData("#section-1", false, LinkType.Anchor)]
    public void DetermineLinkType_WhenAnchorUrl_ReturnsAnchor(string targetUrl, bool isImage, LinkType expected)
    {
        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("http://example.com", false, LinkType.ExternalUrl)]
    [InlineData("https://example.com", false, LinkType.ExternalUrl)]
    [InlineData("HTTP://EXAMPLE.COM", false, LinkType.ExternalUrl)]
    [InlineData("HTTPS://EXAMPLE.COM", false, LinkType.ExternalUrl)]
    public void DetermineLinkType_WhenHttpUrlNotImage_ReturnsExternalUrl(string targetUrl, bool isImage, LinkType expected)
    {
        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("http://example.com/image.png", true, LinkType.Image)]
    [InlineData("https://example.com/image.jpg", true, LinkType.Image)]
    public void DetermineLinkType_WhenHttpUrlIsImage_ReturnsImage(string targetUrl, bool isImage, LinkType expected)
    {
        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("./docs/setup.md", false, LinkType.RelativePath)]
    [InlineData("../README.md", false, LinkType.RelativePath)]
    [InlineData("docs/guide.md", false, LinkType.RelativePath)]
    [InlineData("/absolute/path.md", false, LinkType.RelativePath)]
    [InlineData("file.txt", false, LinkType.RelativePath)]
    public void DetermineLinkType_WhenRelativePath_ReturnsRelativePath(string targetUrl, bool isImage, LinkType expected)
    {
        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DetermineLinkType_WhenEmptyString_ReturnsRelativePath()
    {
        // Arrange
        var targetUrl = "";
        var isImage = false;

        // Act
        var actual = Link.DetermineLinkType(targetUrl, isImage);

        // Assert
        Assert.Equal(LinkType.RelativePath, actual);
    }
}
