namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents the validation result for a single link.
/// </summary>
public class LinkResult
{
    /// <summary>
    /// The original link
    /// </summary>
    public Link Link { get; set; } = null!;
    
    /// <summary>
    /// Validation status
    /// </summary>
    public LinkStatus Status { get; set; }
    
    /// <summary>
    /// HTTP status code (for external URLs only)
    /// </summary>
    public int? HttpStatusCode { get; set; }
    
    /// <summary>
    /// Error type (e.g., "timeout", "ssrf_blocked", "file_not_found")
    /// </summary>
    public string? ErrorType { get; set; }
    
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// New URL after 301 permanent redirect
    /// </summary>
    public string? RedirectUrl { get; set; }
    
    /// <summary>
    /// Suggested anchor for typos (Levenshtein distance ≤ 2)
    /// </summary>
    public string? AnchorSuggestion { get; set; }
    
    /// <summary>
    /// Time taken to validate this link
    /// </summary>
    public TimeSpan Duration { get; set; }
}
