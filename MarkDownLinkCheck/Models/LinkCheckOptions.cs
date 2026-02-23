namespace MarkDownLinkCheck.Models;

/// <summary>
/// Configuration options for link checking behavior.
/// </summary>
public class LinkCheckOptions
{
    /// <summary>
    /// Maximum Markdown content length in characters
    /// </summary>
    public int MaxMarkdownLength { get; set; } = 100000;
    
    /// <summary>
    /// Maximum number of .md files to scan per repository
    /// </summary>
    public int MaxFilesPerRepo { get; set; } = 500;
    
    /// <summary>
    /// Maximum number of links to parse per file
    /// </summary>
    public int MaxLinksPerFile { get; set; } = 1000;
    
    /// <summary>
    /// Maximum total number of links per check
    /// </summary>
    public int MaxLinksPerCheck { get; set; } = 5000;
    
    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 10;
    
    /// <summary>
    /// Maximum number of redirects to follow
    /// </summary>
    public int MaxRedirects { get; set; } = 5;
    
    /// <summary>
    /// Maximum retry attempts for timeout/5xx errors
    /// </summary>
    public int MaxRetries { get; set; } = 1;
    
    /// <summary>
    /// Global maximum concurrent HTTP requests
    /// </summary>
    public int GlobalConcurrency { get; set; } = 20;
    
    /// <summary>
    /// Maximum concurrent requests per domain
    /// </summary>
    public int PerDomainConcurrency { get; set; } = 3;
    
    /// <summary>
    /// User-Agent header for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "MarkdownLinkCheck/1.0";
}
