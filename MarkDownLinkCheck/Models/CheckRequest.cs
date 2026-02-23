using System.ComponentModel.DataAnnotations;

namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents a user request to check Markdown links.
/// </summary>
public class CheckRequest
{
    /// <summary>
    /// Check mode
    /// </summary>
    [Required]
    public CheckMode Mode { get; set; }
    
    /// <summary>
    /// Markdown source text (required when Mode == MarkdownSource)
    /// </summary>
    public string? MarkdownContent { get; set; }
    
    /// <summary>
    /// GitHub repository URL (required when Mode == RepoUrl)
    /// </summary>
    public string? RepoUrl { get; set; }
    
    /// <summary>
    /// Branch name (optional, uses default branch if not specified)
    /// </summary>
    public string? Branch { get; set; }
    
    /// <summary>
    /// Request timestamp (set by server)
    /// </summary>
    public DateTimeOffset RequestedAt { get; set; }
    
    /// <summary>
    /// Source IP address (set by server)
    /// </summary>
    public string SourceIp { get; set; } = string.Empty;

    /// <summary>
    /// Validates the request based on the selected mode
    /// </summary>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();
        
        if (Mode == CheckMode.MarkdownSource)
        {
            if (string.IsNullOrWhiteSpace(MarkdownContent))
                errors.Add("請輸入 Markdown 內容");
            else if (MarkdownContent.Length > 100000)
                errors.Add($"Markdown 內容長度不可超過 100,000 字元（目前 {MarkdownContent.Length} 字元）");
        }
        else if (Mode == CheckMode.RepoUrl)
        {
            if (string.IsNullOrWhiteSpace(RepoUrl))
                errors.Add("請輸入 GitHub Repository URL");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(
                RepoUrl, @"^https://github\.com/[a-zA-Z0-9\-_.]+/[a-zA-Z0-9\-_.]+$"))
                errors.Add("請輸入合法的 GitHub Repository URL");
        }
        
        return errors;
    }
}
