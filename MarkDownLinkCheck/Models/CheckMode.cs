namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents the mode of link checking operation.
/// </summary>
public enum CheckMode
{
    /// <summary>
    /// Check links from Markdown source text
    /// </summary>
    MarkdownSource = 0,
    
    /// <summary>
    /// Check links from GitHub repository URL
    /// </summary>
    RepoUrl = 1
}
