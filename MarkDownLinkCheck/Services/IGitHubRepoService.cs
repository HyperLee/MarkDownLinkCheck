using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for interacting with GitHub repositories.
/// </summary>
public interface IGitHubRepoService
{
    /// <summary>
    /// Validates a GitHub repository URL and extracts owner and repo name.
    /// </summary>
    /// <param name="repoUrl">Repository URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (owner, repo)</returns>
    Task<(string Owner, string Repo)> ValidateRepoUrlAsync(string repoUrl, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the default branch name for a repository.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Default branch name</returns>
    Task<string> GetDefaultBranchAsync(string owner, string repo, CancellationToken cancellationToken);
    
    /// <summary>
    /// Lists all Markdown files in a repository branch.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="branch">Branch name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Markdown file paths</returns>
    Task<IReadOnlyList<string>> ListMarkdownFilesAsync(string owner, string repo, string branch, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the content of a specific file from a repository.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="branch">Branch name</param>
    /// <param name="filePath">File path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content</returns>
    Task<string> GetFileContentAsync(string owner, string repo, string branch, string filePath, CancellationToken cancellationToken);
}
