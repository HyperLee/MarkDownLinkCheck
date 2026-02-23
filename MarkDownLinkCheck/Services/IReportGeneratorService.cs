using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for generating check reports.
/// </summary>
public interface IReportGeneratorService
{
    /// <summary>
    /// Generates a check report from file results.
    /// </summary>
    /// <param name="files">File check results</param>
    /// <param name="duration">Total duration</param>
    /// <returns>Complete check report</returns>
    CheckReport GenerateReport(IReadOnlyList<FileCheckResult> files, TimeSpan duration);
    
    /// <summary>
    /// Generates a Markdown-formatted report.
    /// </summary>
    /// <param name="report">Check report</param>
    /// <returns>Markdown table format</returns>
    string GenerateMarkdownReport(CheckReport report);
}
