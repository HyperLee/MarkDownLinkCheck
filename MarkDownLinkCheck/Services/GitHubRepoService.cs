using System.Text.Json;
using System.Text.RegularExpressions;
using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for interacting with GitHub REST API v3 to scan repositories for Markdown files
/// </summary>
public partial class GitHubRepoService : IGitHubRepoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const int MaxFilesPerRepo = 500;

    [GeneratedRegex(@"^https://github\.com/([^/]+)/([^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubUrlRegex();

    public GitHubRepoService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Validates GitHub repository URL and extracts owner and repo name
    /// </summary>
    /// <param name="repoUrl">GitHub repository URL (e.g., https://github.com/owner/repo)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (owner, repo)</returns>
    /// <exception cref="ArgumentException">Invalid URL format</exception>
    /// <exception cref="InvalidOperationException">Private repository or not found</exception>
    public async Task<(string Owner, string Repo)> ValidateRepoUrlAsync(string repoUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            throw new ArgumentException("Repository URL cannot be empty", nameof(repoUrl));
        }

        var match = GitHubUrlRegex().Match(repoUrl.Trim());
        if (!match.Success)
        {
            throw new ArgumentException("Invalid GitHub repository URL format. Expected: https://github.com/owner/repo", nameof(repoUrl));
        }

        string owner = match.Groups[1].Value;
        string repo = match.Groups[2].Value;

        // Verify repository exists and is public by making a request to GitHub API
        var httpClient = _httpClientFactory.CreateClient("LinkChecker");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarkDownLinkCheck/1.0");

        try
        {
            var response = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("目前僅支援公開 Repository，或該 Repository 不存在");
            }

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("目前僅支援公開 Repository，或該 Repository 不存在", ex);
        }

        return (owner, repo);
    }

    /// <summary>
    /// Gets the default branch name for a repository
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Default branch name (e.g., "main" or "master")</returns>
    /// <exception cref="InvalidOperationException">Repository not found or API error</exception>
    public async Task<string> GetDefaultBranchAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("LinkChecker");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarkDownLinkCheck/1.0");

        var response = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Repository {owner}/{repo} not found");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (content.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GitHub API rate limit exceeded. Please try again later.");
            }
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty("default_branch", out var branchElement))
        {
            return branchElement.GetString() ?? "main";
        }

        return "main";
    }

    /// <summary>
    /// Lists all Markdown files in a repository
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="branch">Branch name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Markdown file paths (max 500 files)</returns>
    public async Task<IReadOnlyList<string>> ListMarkdownFilesAsync(string owner, string repo, string branch, 
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("LinkChecker");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarkDownLinkCheck/1.0");

        // Use Git Tree API to recursively get all files
        var response = await httpClient.GetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}?recursive=1", 
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var markdownFiles = new List<string>();

        if (doc.RootElement.TryGetProperty("tree", out var tree) && tree.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tree.EnumerateArray())
            {
                if (markdownFiles.Count >= MaxFilesPerRepo)
                {
                    break;
                }

                if (item.TryGetProperty("type", out var typeElement) && 
                    typeElement.GetString() == "blob" &&
                    item.TryGetProperty("path", out var pathElement))
                {
                    var path = pathElement.GetString();
                    if (!string.IsNullOrEmpty(path) && 
                        (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || 
                         path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
                    {
                        markdownFiles.Add(path);
                    }
                }
            }
        }

        return markdownFiles.AsReadOnly();
    }

    /// <summary>
    /// Gets the raw content of a file from a repository
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="branch">Branch name</param>
    /// <param name="filePath">File path relative to repository root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    /// <exception cref="InvalidOperationException">File not found</exception>
    public async Task<string> GetFileContentAsync(string owner, string repo, string branch, string filePath, 
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("LinkChecker");
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarkDownLinkCheck/1.0");

        // Use raw.githubusercontent.com for direct file content access
        var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{filePath}";
        
        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"File {filePath} not found in {owner}/{repo} on branch {branch}");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
