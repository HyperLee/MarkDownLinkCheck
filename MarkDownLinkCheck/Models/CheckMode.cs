using System.Text.Json.Serialization;

namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents the mode of link checking operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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
