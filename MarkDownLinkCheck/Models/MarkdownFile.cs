namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents a Markdown file being scanned.
/// </summary>
public class MarkdownFile
{
    /// <summary>
    /// File name (e.g., README.md)
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Relative path (e.g., docs/setup.md)
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Full file content
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// List of links found in this file
    /// </summary>
    public IReadOnlyList<Link> Links { get; set; } = Array.Empty<Link>();
}
