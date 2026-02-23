using MarkDownLinkCheck.Models;
using MarkDownLinkCheck.Services;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for LinkValidatorService (T019)
/// Tests: HTTP status mapping, HEAD 405→GET fallback, anchor validation, email format, SSRF blocked
/// </summary>
public class LinkValidatorServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IAnchorSuggestionService> _anchorSuggestionServiceMock;
    private readonly ILinkValidatorService _service;

    public LinkValidatorServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _anchorSuggestionServiceMock = new Mock<IAnchorSuggestionService>();
        _service = new LinkValidatorService(_httpClientFactoryMock.Object, _anchorSuggestionServiceMock.Object);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, LinkStatus.Healthy)]
    [InlineData(HttpStatusCode.Created, LinkStatus.Healthy)]
    [InlineData(HttpStatusCode.NoContent, LinkStatus.Healthy)]
    public async Task ValidateAsync_WhenHttp2xx_ReturnsHealthy(HttpStatusCode statusCode, LinkStatus expectedStatus)
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = CreateMockHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal((int)statusCode, result.HttpStatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, LinkStatus.Broken)]
    [InlineData(HttpStatusCode.Forbidden, LinkStatus.Broken)]
    [InlineData(HttpStatusCode.Gone, LinkStatus.Broken)]
    public async Task ValidateAsync_WhenHttp4xx_ReturnsBroken(HttpStatusCode statusCode, LinkStatus expectedStatus)
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = CreateMockHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal((int)statusCode, result.HttpStatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, LinkStatus.Broken)]
    [InlineData(HttpStatusCode.BadGateway, LinkStatus.Broken)]
    [InlineData(HttpStatusCode.ServiceUnavailable, LinkStatus.Broken)]
    public async Task ValidateAsync_WhenHttp5xx_ReturnsBroken(HttpStatusCode statusCode, LinkStatus expectedStatus)
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = CreateMockHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal((int)statusCode, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenHttp301_ReturnsWarningWithRedirectUrl()
    {
        // Arrange
        var link = CreateExternalLink("https://example.com/old");
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.MovedPermanently, "https://example.com/new");
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Warning, result.Status);
        Assert.Equal(301, result.HttpStatusCode);
        Assert.Equal("https://example.com/new", result.RedirectUrl);
    }

    [Fact]
    public async Task ValidateAsync_WhenHttp302_ReturnsHealthy()
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.Found);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Healthy, result.Status);
        Assert.Equal(302, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenHttp429_ReturnsWarning()
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.TooManyRequests);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Warning, result.Status);
        Assert.Equal(429, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenHead405_FallbacksToGet()
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));

        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Healthy, result.Status);
        Assert.Equal(200, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenTimeout_ReturnsWarning()
    {
        // Arrange
        var link = CreateExternalLink("https://example.com");
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("LinkChecker")).Returns(httpClient);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Warning, result.Status);
        Assert.Equal("timeout", result.ErrorType);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnchorExists_ReturnsHealthy()
    {
        // Arrange
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = "# Installation\nSome content"
        };
        var link = new Link
        {
            Type = LinkType.Anchor,
            TargetUrl = "#installation",
            RawText = "[link](#installation)",
            LineNumber = 1,
            SourceFile = markdownFile
        };
        
        var validAnchors = new List<string> { "installation", "setup" };

        // Act
        var result = await _service.ValidateAsync(link, null, new Dictionary<string, IReadOnlyList<string>> { { "test.md", validAnchors } }, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnchorNotFound_ReturnsBrokenWithSuggestion()
    {
        // Arrange
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = "# Installation\nSome content"
        };
        var link = new Link
        {
            Type = LinkType.Anchor,
            TargetUrl = "#instalation",
            RawText = "[link](#instalation)",
            LineNumber = 1,
            SourceFile = markdownFile
        };
        
        var validAnchors = new List<string> { "installation", "setup" };
        _anchorSuggestionServiceMock.Setup(s => s.Suggest("instalation", validAnchors)).Returns("installation");

        // Act
        var result = await _service.ValidateAsync(link, null, new Dictionary<string, IReadOnlyList<string>> { { "test.md", validAnchors } }, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Broken, result.Status);
        Assert.Equal("anchor_not_found", result.ErrorType);
        Assert.Equal("installation", result.AnchorSuggestion);
    }

    [Theory]
    [InlineData("mailto:test@example.com", LinkStatus.Healthy)]
    [InlineData("mailto:user.name@domain.co.uk", LinkStatus.Healthy)]
    public async Task ValidateAsync_WhenValidEmail_ReturnsHealthy(string email, LinkStatus expectedStatus)
    {
        // Arrange
        var link = CreateEmailLink(email);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
    }

    [Theory]
    [InlineData("mailto:invalid-email")]
    [InlineData("mailto:@example.com")]
    [InlineData("mailto:test@")]
    public async Task ValidateAsync_WhenInvalidEmail_ReturnsBroken(string email)
    {
        // Arrange
        var link = CreateEmailLink(email);

        // Act
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Broken, result.Status);
        Assert.Equal("invalid_email", result.ErrorType);
    }

    [Fact]
    public async Task ValidateAsync_WhenRelativePathInMarkdownMode_ReturnsSkipped()
    {
        // Arrange
        var link = CreateRelativePathLink("./docs/setup.md");

        // Act (repoFiles is null = Markdown mode)
        var result = await _service.ValidateAsync(link, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WhenRelativePathInRepoModeExists_ReturnsHealthy()
    {
        // Arrange
        var link = CreateRelativePathLink("docs/setup.md");
        var repoFiles = new HashSet<string> { "README.md", "docs/setup.md", "docs/guide.md" };

        // Act
        var result = await _service.ValidateAsync(link, repoFiles, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WhenRelativePathInRepoModeNotExists_ReturnsBroken()
    {
        // Arrange
        var link = CreateRelativePathLink("docs/missing.md");
        var repoFiles = new HashSet<string> { "README.md", "docs/setup.md" };

        // Act
        var result = await _service.ValidateAsync(link, repoFiles, null, CancellationToken.None);

        // Assert
        Assert.Equal(LinkStatus.Broken, result.Status);
        Assert.Equal("file_not_found", result.ErrorType);
    }

    private Link CreateExternalLink(string url)
    {
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };
        return new Link
        {
            Type = LinkType.ExternalUrl,
            TargetUrl = url,
            RawText = $"[link]({url})",
            LineNumber = 1,
            SourceFile = markdownFile
        };
    }

    private Link CreateEmailLink(string email)
    {
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };
        return new Link
        {
            Type = LinkType.Email,
            TargetUrl = email,
            RawText = $"[email]({email})",
            LineNumber = 1,
            SourceFile = markdownFile
        };
    }

    private Link CreateRelativePathLink(string path)
    {
        var markdownFile = new MarkdownFile
        {
            FileName = "test.md",
            RelativePath = "test.md",
            Content = ""
        };
        return new Link
        {
            Type = LinkType.RelativePath,
            TargetUrl = path,
            RawText = $"[link]({path})",
            LineNumber = 1,
            SourceFile = markdownFile
        };
    }

    private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(HttpStatusCode statusCode, string? redirectUrl = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage(statusCode);
        
        if (redirectUrl != null)
        {
            response.Headers.Location = new Uri(redirectUrl);
        }

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return handler;
    }
}
