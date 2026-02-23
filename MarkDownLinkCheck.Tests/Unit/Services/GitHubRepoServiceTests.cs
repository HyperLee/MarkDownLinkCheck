using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using Xunit;
using MarkDownLinkCheck.Services;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for GitHubRepoService
/// Tests: ValidateRepoUrlAsync, GetDefaultBranchAsync, ListMarkdownFilesAsync (500 file limit), GetFileContentAsync
/// </summary>
public class GitHubRepoServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public GitHubRepoServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
    }

    #region ValidateRepoUrlAsync Tests

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("https://github.com/test-user/test-repo", "test-user", "test-repo")]
    [InlineData("https://github.com/org123/repo-name", "org123", "repo-name")]
    public async Task ValidateRepoUrlAsync_ValidUrl_ReturnsOwnerAndRepo(string url, string expectedOwner, string expectedRepo)
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"default_branch\":\"main\"}")
            });

        // Act
        var result = await service.ValidateRepoUrlAsync(url, CancellationToken.None);

        // Assert
        Assert.Equal(expectedOwner, result.Owner);
        Assert.Equal(expectedRepo, result.Repo);
    }

    [Theory]
    [InlineData("https://github.com/owner")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("not-a-url")]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com")]
    public async Task ValidateRepoUrlAsync_InvalidUrl_ThrowsArgumentException(string url)
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ValidateRepoUrlAsync(url, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateRepoUrlAsync_PrivateRepo_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("{\"message\":\"Not Found\"}")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.ValidateRepoUrlAsync("https://github.com/owner/private-repo", CancellationToken.None));
        
        Assert.Contains("公開", ex.Message);
    }

    #endregion

    #region GetDefaultBranchAsync Tests

    [Fact]
    public async Task GetDefaultBranchAsync_ValidRepo_ReturnsDefaultBranch()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        var repoJson = "{\"default_branch\":\"main\"}";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/repos/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(repoJson)
            });

        // Act
        var result = await service.GetDefaultBranchAsync("owner", "repo", CancellationToken.None);

        // Assert
        Assert.Equal("main", result);
    }

    [Fact]
    public async Task GetDefaultBranchAsync_RepoNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.GetDefaultBranchAsync("owner", "repo", CancellationToken.None));
    }

    [Fact]
    public async Task GetDefaultBranchAsync_RateLimitExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("{\"message\":\"API rate limit exceeded\"}")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.GetDefaultBranchAsync("owner", "repo", CancellationToken.None));
        
        Assert.Contains("rate limit", ex.Message.ToLower());
    }

    #endregion

    #region ListMarkdownFilesAsync Tests

    [Fact]
    public async Task ListMarkdownFilesAsync_ValidRepo_ReturnsMarkdownFiles()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        var treeJson = @"{
            ""tree"": [
                {""path"":""README.md"",""type"":""blob""},
                {""path"":""docs/guide.md"",""type"":""blob""},
                {""path"":""docs/setup.txt"",""type"":""blob""},
                {""path"":""test.MD"",""type"":""blob""}
            ]
        }";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/git/trees/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(treeJson)
            });

        // Act
        var result = await service.ListMarkdownFilesAsync("owner", "repo", "main", CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("README.md", result);
        Assert.Contains("docs/guide.md", result);
        Assert.Contains("test.MD", result);
        Assert.DoesNotContain("docs/setup.txt", result);
    }

    [Fact]
    public async Task ListMarkdownFilesAsync_MoreThan500Files_Returns500FilesOnly()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        // Create a tree with 600 .md files
        var sb = new StringBuilder();
        sb.Append("{\"tree\":[");
        for (int i = 0; i < 600; i++)
        {
            sb.Append($"{{\"path\":\"file{i}.md\",\"type\":\"blob\"}}");
            if (i < 599) sb.Append(",");
        }
        sb.Append("]}");
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(sb.ToString())
            });

        // Act
        var result = await service.ListMarkdownFilesAsync("owner", "repo", "main", CancellationToken.None);

        // Assert
        Assert.Equal(500, result.Count);
    }

    [Fact]
    public async Task ListMarkdownFilesAsync_EmptyRepo_ReturnsEmptyList()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        var treeJson = "{\"tree\":[]}";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(treeJson)
            });

        // Act
        var result = await service.ListMarkdownFilesAsync("owner", "repo", "main", CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region GetFileContentAsync Tests

    [Fact]
    public async Task GetFileContentAsync_ValidFile_ReturnsContent()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        var expectedContent = "# Hello World\n\nThis is a test.";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.Host == "raw.githubusercontent.com"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedContent)
            });

        // Act
        var result = await service.GetFileContentAsync("owner", "repo", "main", "README.md", CancellationToken.None);

        // Assert
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public async Task GetFileContentAsync_FileNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new GitHubRepoService(_mockHttpClientFactory.Object);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.GetFileContentAsync("owner", "repo", "main", "missing.md", CancellationToken.None));
    }

    #endregion
}
