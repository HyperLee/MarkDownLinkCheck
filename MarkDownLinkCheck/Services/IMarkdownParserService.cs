using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for parsing Markdown content and extracting links and anchors.
/// </summary>
public interface IMarkdownParserService
{
    /// <summary>
    /// Parses Markdown content and extracts all links.
    /// </summary>
    /// <param name="content">Markdown content</param>
    /// <param name="fileName">File name for the Markdown file</param>
    /// <returns>List of extracted links</returns>
    IReadOnlyList<Link> ParseLinks(string content, string fileName);
    
    /// <summary>
    /// Extracts all anchor IDs from Markdown headings.
    /// </summary>
    /// <param name="content">Markdown content</param>
    /// <returns>List of anchor IDs (GitHub-style)</returns>
    IReadOnlyList<string> ExtractAnchors(string content);
}
