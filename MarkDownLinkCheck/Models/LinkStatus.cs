namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents the validation status of a link.
/// </summary>
public enum LinkStatus
{
    /// <summary>
    /// Link is valid (HTTP 2xx or file exists) ✅
    /// </summary>
    Healthy = 0,
    
    /// <summary>
    /// Link is broken (HTTP 4xx/5xx, file not found, SSRF blocked) ❌
    /// </summary>
    Broken = 1,
    
    /// <summary>
    /// Link needs attention (timeout, HTTP 429, 301 redirect, too many redirects) ⚠️
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Link was skipped (relative path in Markdown-only mode) ⏭️
    /// </summary>
    Skipped = 3
}
