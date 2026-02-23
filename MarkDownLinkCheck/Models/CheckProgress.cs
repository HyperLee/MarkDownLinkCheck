using System.Text.Json.Serialization;

namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents a Server-Sent Event for check progress.
/// </summary>
public class CheckProgress
{
    /// <summary>
    /// Event type: "progress", "file-result", "complete", "error"
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of links checked so far (for "progress" event)
    /// </summary>
    [JsonPropertyName("checkedCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CheckedCount { get; set; }
    
    /// <summary>
    /// Total number of links (for "progress" event)
    /// </summary>
    [JsonPropertyName("totalCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; set; }
    
    /// <summary>
    /// Currently processing file (for "progress" event)
    /// </summary>
    [JsonPropertyName("currentFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentFile { get; set; }
    
    /// <summary>
    /// Completed file result (for "file-result" event)
    /// </summary>
    [JsonPropertyName("fileResult")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FileCheckResult? FileResult { get; set; }
    
    /// <summary>
    /// Final report (for "complete" event)
    /// </summary>
    [JsonPropertyName("report")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CheckReport? Report { get; set; }
    
    /// <summary>
    /// Error message (for "error" event)
    /// </summary>
    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}
