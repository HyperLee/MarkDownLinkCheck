using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for generating check reports.
/// </summary>
public class ReportGeneratorService : IReportGeneratorService
{
    public CheckReport GenerateReport(IReadOnlyList<FileCheckResult> files, TimeSpan duration)
    {
        var report = new CheckReport
        {
            FileCount = files.Count,
            TotalDuration = duration,
            FileResults = SortFileResults(files)
        };

        // Calculate aggregate counts
        foreach (var file in files)
        {
            report.TotalLinkCount += file.LinkResults.Count;
            report.HealthyCount += file.HealthyCount;
            report.BrokenCount += file.BrokenCount;
            report.WarningCount += file.WarningCount;
            report.SkippedCount += file.SkippedCount;
        }

        return report;
    }

    public string GenerateMarkdownReport(CheckReport report)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Markdown Link Check Report");
        sb.AppendLine();
        
        // Summary section
        sb.AppendLine("## 📊 Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Files Scanned**: {report.FileCount}");
        sb.AppendLine($"- **Total Links**: {report.TotalLinkCount}");
        sb.AppendLine($"- **✅ Healthy**: {report.HealthyCount}");
        sb.AppendLine($"- **❌ Broken**: {report.BrokenCount}");
        sb.AppendLine($"- **⚠️ Warning**: {report.WarningCount}");
        sb.AppendLine($"- **⏭️ Skipped**: {report.SkippedCount}");
        sb.AppendLine($"- **⏱️ Duration**: {report.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();
        
        // File results
        foreach (var fileResult in report.FileResults)
        {
            if (fileResult.LinkResults.Count == 0) continue;
            
            sb.AppendLine($"## 📄 {fileResult.File.RelativePath}");
            sb.AppendLine();
            sb.AppendLine($"**Stats**: ✅ {fileResult.HealthyCount} | ❌ {fileResult.BrokenCount} | ⚠️ {fileResult.WarningCount} | ⏭️ {fileResult.SkippedCount}");
            sb.AppendLine();
            
            // Create table
            sb.AppendLine("| Status | Target URL | Line | Error |");
            sb.AppendLine("|--------|------------|------|-------|");
            
            foreach (var linkResult in fileResult.LinkResults)
            {
                var statusEmoji = linkResult.Status switch
                {
                    LinkStatus.Healthy => "✅",
                    LinkStatus.Broken => "❌",
                    LinkStatus.Warning => "⚠️",
                    LinkStatus.Skipped => "⏭️",
                    _ => "❓"
                };
                
                var targetUrl = EscapeMarkdown(linkResult.Link.TargetUrl);
                var lineNumber = linkResult.Link.LineNumber > 0 ? linkResult.Link.LineNumber.ToString() : "-";
                var errorMessage = linkResult.ErrorMessage ?? "";
                
                // Add anchor suggestion if available
                if (!string.IsNullOrEmpty(linkResult.AnchorSuggestion))
                {
                    errorMessage += $" (did you mean `{linkResult.AnchorSuggestion}`?)";
                }
                
                // Add redirect URL if available
                if (!string.IsNullOrEmpty(linkResult.RedirectUrl))
                {
                    errorMessage += $" → `{linkResult.RedirectUrl}`";
                }
                
                // Add HTTP status code if available
                if (linkResult.HttpStatusCode.HasValue)
                {
                    errorMessage += $" [{linkResult.HttpStatusCode.Value}]";
                }
                
                errorMessage = EscapeMarkdown(errorMessage);
                
                sb.AppendLine($"| {statusEmoji} | `{targetUrl}` | {lineNumber} | {errorMessage} |");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Escapes special Markdown characters in text
    /// </summary>
    private string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        return text
            .Replace("|", "\\|")
            .Replace("\n", " ")
            .Replace("\r", "");
    }

    /// <summary>
    /// Sorts file results by severity: Broken-first, then Warning, then Healthy/Skipped.
    /// Also sorts link results within each file by severity.
    /// </summary>
    private IReadOnlyList<FileCheckResult> SortFileResults(IReadOnlyList<FileCheckResult> files)
    {
        var sorted = files
            .Select(f => new FileCheckResult
            {
                File = f.File,
                HealthyCount = f.HealthyCount,
                BrokenCount = f.BrokenCount,
                WarningCount = f.WarningCount,
                SkippedCount = f.SkippedCount,
                LinkResults = SortLinkResults(f.LinkResults)
            })
            .OrderByDescending(f => f.BrokenCount)
            .ThenByDescending(f => f.WarningCount)
            .ToList();

        return sorted;
    }

    /// <summary>
    /// Sorts link results by severity: Broken → Warning → Healthy/Skipped.
    /// </summary>
    private IReadOnlyList<LinkResult> SortLinkResults(IReadOnlyList<LinkResult> linkResults)
    {
        return linkResults
            .OrderBy(l => l.Status switch
            {
                LinkStatus.Broken => 0,
                LinkStatus.Warning => 1,
                LinkStatus.Healthy => 2,
                LinkStatus.Skipped => 3,
                _ => 4
            })
            .ToList();
    }
}
