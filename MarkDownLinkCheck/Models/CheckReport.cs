namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents a complete check report.
/// </summary>
public class CheckReport
{
    /// <summary>
    /// Number of files scanned
    /// </summary>
    public int FileCount { get; set; }
    
    /// <summary>
    /// Total number of links checked
    /// </summary>
    public int TotalLinkCount { get; set; }
    
    /// <summary>
    /// Number of healthy links
    /// </summary>
    public int HealthyCount { get; set; }
    
    /// <summary>
    /// Number of broken links
    /// </summary>
    public int BrokenCount { get; set; }
    
    /// <summary>
    /// Number of warnings
    /// </summary>
    public int WarningCount { get; set; }
    
    /// <summary>
    /// Number of skipped links
    /// </summary>
    public int SkippedCount { get; set; }
    
    /// <summary>
    /// Total duration for the check
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
    
    /// <summary>
    /// Results grouped by file
    /// </summary>
    public IReadOnlyList<FileCheckResult> FileResults { get; set; } = Array.Empty<FileCheckResult>();
}

/// <summary>
/// Represents check results for a single file.
/// </summary>
public class FileCheckResult
{
    /// <summary>
    /// The Markdown file
    /// </summary>
    public MarkdownFile File { get; set; } = null!;
    
    /// <summary>
    /// Link validation results for this file
    /// </summary>
    public IReadOnlyList<LinkResult> LinkResults { get; set; } = Array.Empty<LinkResult>();
    
    /// <summary>
    /// Number of broken links in this file
    /// </summary>
    public int BrokenCount { get; set; }
    
    /// <summary>
    /// Number of warnings in this file
    /// </summary>
    public int WarningCount { get; set; }
    
    /// <summary>
    /// Number of healthy links in this file
    /// </summary>
    public int HealthyCount { get; set; }
    
    /// <summary>
    /// Number of skipped links in this file
    /// </summary>
    public int SkippedCount { get; set; }
}
