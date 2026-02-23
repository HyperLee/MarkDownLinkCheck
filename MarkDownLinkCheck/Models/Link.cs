namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents a link found in Markdown content.
/// </summary>
public class Link
{
    /// <summary>
    /// Type of link
    /// </summary>
    public LinkType Type { get; set; }
    
    /// <summary>
    /// Original Markdown text (e.g., [text](url))
    /// </summary>
    public string RawText { get; set; } = string.Empty;
    
    /// <summary>
    /// Target URL or path
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Line number in source file (1-based)
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// Source Markdown file containing this link
    /// </summary>
    public MarkdownFile SourceFile { get; set; } = null!;
    
    /// <summary>
    /// Indicates if this is an image link
    /// </summary>
    public bool IsImage { get; set; }

    /// <summary>
    /// Determines link type based on target URL
    /// </summary>
    public static LinkType DetermineLinkType(string targetUrl, bool isImage)
    {
        if (targetUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return LinkType.Email;
        
        if (targetUrl.StartsWith("#"))
            return LinkType.Anchor;
        
        if (targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return isImage ? LinkType.Image : LinkType.ExternalUrl;
        
        return LinkType.RelativePath;
    }
}
