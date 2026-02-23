using MarkDownLinkCheck.Models;
using System.Runtime.CompilerServices;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Orchestrator service that coordinates the entire link checking process.
/// </summary>
public class LinkCheckOrchestrator : ILinkCheckOrchestrator
{
    private readonly IMarkdownParserService _markdownParserService;
    private readonly ILinkValidatorService _linkValidatorService;
    private readonly IReportGeneratorService _reportGeneratorService;
    private readonly IGitHubRepoService? _gitHubRepoService;
    private readonly LinkCheckOptions _options;

    public LinkCheckOrchestrator(
        IMarkdownParserService markdownParserService,
        ILinkValidatorService linkValidatorService,
        IReportGeneratorService reportGeneratorService,
        IGitHubRepoService? gitHubRepoService,
        LinkCheckOptions options)
    {
        _markdownParserService = markdownParserService;
        _linkValidatorService = linkValidatorService;
        _reportGeneratorService = reportGeneratorService;
        _gitHubRepoService = gitHubRepoService;
        _options = options;
    }

    public async IAsyncEnumerable<CheckProgress> ExecuteAsync(
        CheckRequest request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (request.Mode == CheckMode.MarkdownSource)
            {
                await foreach (var progress in ExecuteMarkdownSourceModeAsync(request, cancellationToken))
                {
                    yield return progress;
                }
            }
            else if (request.Mode == CheckMode.RepoUrl)
            {
                await foreach (var progress in ExecuteRepoUrlModeAsync(request, cancellationToken))
                {
                    yield return progress;
                }
            }
            else
            {
                yield return new CheckProgress
                {
                    EventType = "error",
                    ErrorMessage = "Unknown check mode"
                };
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async IAsyncEnumerable<CheckProgress> ExecuteMarkdownSourceModeAsync(
        CheckRequest request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Validate content length
        if (string.IsNullOrEmpty(request.MarkdownContent))
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = "Markdown content is required"
            };
            yield break;
        }

        if (request.MarkdownContent.Length > _options.MaxMarkdownLength)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = $"Markdown content exceeds maximum length of {_options.MaxMarkdownLength} characters"
            };
            yield break;
        }

        // Create single Markdown file
        var markdownFile = new MarkdownFile
        {
            FileName = "input.md",
            RelativePath = "input.md",
            Content = request.MarkdownContent,
            Links = Array.Empty<Link>()
        };

        // Parse links
        var links = _markdownParserService.ParseLinks(request.MarkdownContent, "input.md");
        
        // Check link count limit
        if (links.Count > _options.MaxLinksPerCheck)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = $"Total link count ({links.Count}) exceeds maximum of {_options.MaxLinksPerCheck} links"
            };
            yield break;
        }

        // Extract anchors for validation
        var anchors = _markdownParserService.ExtractAnchors(request.MarkdownContent);
        var anchorsMap = new Dictionary<string, IReadOnlyList<string>>
        {
            { "input.md", anchors }
        };

        // Deduplicate external URLs
        var uniqueLinks = DeduplicateExternalUrls(links);

        var totalCount = uniqueLinks.Count;
        var checkedCount = 0;
        var linkResults = new List<LinkResult>();

        // Validate all links
        foreach (var link in uniqueLinks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _linkValidatorService.ValidateAsync(link, null, anchorsMap, cancellationToken);
            linkResults.Add(result);

            checkedCount++;

            // Emit progress event
            yield return new CheckProgress
            {
                EventType = "progress",
                CheckedCount = checkedCount,
                TotalCount = totalCount,
                CurrentFile = "input.md"
            };
        }

        // Create file result
        var fileResult = new FileCheckResult
        {
            File = markdownFile,
            LinkResults = linkResults,
            HealthyCount = linkResults.Count(r => r.Status == LinkStatus.Healthy),
            BrokenCount = linkResults.Count(r => r.Status == LinkStatus.Broken),
            WarningCount = linkResults.Count(r => r.Status == LinkStatus.Warning),
            SkippedCount = linkResults.Count(r => r.Status == LinkStatus.Skipped)
        };

        // Emit file-result event
        yield return new CheckProgress
        {
            EventType = "file-result",
            FileResult = fileResult
        };

        // Generate final report
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fileResults = new List<FileCheckResult> { fileResult };
        var report = _reportGeneratorService.GenerateReport(fileResults, stopwatch.Elapsed);

        // Emit complete event
        yield return new CheckProgress
        {
            EventType = "complete",
            Report = report
        };
    }

    /// <summary>
    /// Deduplicates external URLs to avoid checking the same URL multiple times.
    /// Non-external links are not deduplicated as they need individual validation.
    /// </summary>
    private List<Link> DeduplicateExternalUrls(IReadOnlyList<Link> links)
    {
        var uniqueLinks = new List<Link>();
        var seenExternalUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (link.Type == LinkType.ExternalUrl || link.Type == LinkType.Image)
            {
                // Deduplicate external URLs
                if (!seenExternalUrls.Contains(link.TargetUrl))
                {
                    seenExternalUrls.Add(link.TargetUrl);
                    uniqueLinks.Add(link);
                }
            }
            else
            {
                // Don't deduplicate anchors, emails, or relative paths
                uniqueLinks.Add(link);
            }
        }

        return uniqueLinks;
    }

    /// <summary>
    /// Safely validates a repository URL, returning error message instead of throwing
    /// </summary>
    private async Task<(string Owner, string Repo, string? Error)> ValidateRepoUrlSafeAsync(
        string? repoUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repoUrl))
        {
            return (string.Empty, string.Empty, "請輸入 GitHub Repository URL");
        }

        try
        {
            var (owner, repo) = await _gitHubRepoService!.ValidateRepoUrlAsync(repoUrl, cancellationToken);
            return (owner, repo, null);
        }
        catch (ArgumentException ex)
        {
            return (string.Empty, string.Empty, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return (string.Empty, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Safely lists Markdown files, returning error message instead of throwing
    /// </summary>
    private async Task<(IReadOnlyList<string> Files, string? Error)> ListMarkdownFilesSafeAsync(
        string owner, string repo, string branch, CancellationToken cancellationToken)
    {
        try
        {
            var files = await _gitHubRepoService!.ListMarkdownFilesAsync(owner, repo, branch, cancellationToken);
            return (files, null);
        }
        catch (InvalidOperationException ex)
        {
            return (Array.Empty<string>(), $"無法掃描 Repository: {ex.Message}");
        }
    }

    private async IAsyncEnumerable<CheckProgress> ExecuteRepoUrlModeAsync(
        CheckRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_gitHubRepoService == null)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = "GitHub Repo Service is not configured"
            };
            yield break;
        }

        // Validate repo URL
        string owner, repo;
        (owner, repo, var validateError) = await ValidateRepoUrlSafeAsync(request.RepoUrl, cancellationToken);
        
        if (validateError != null)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = validateError
            };
            yield break;
        }

        // Get branch (use default if not specified)
        (var branch, var branchError) = await GetDefaultBranchSafeAsync(owner, repo, request.Branch, cancellationToken);
        
        if (branchError != null)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = branchError
            };
            yield break;
        }

        // List Markdown files
        (var filePaths, var scanError) = await ListMarkdownFilesSafeAsync(owner, repo, branch, cancellationToken);
        
        if (scanError != null)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = scanError
            };
            yield break;
        }

        if (filePaths.Count == 0)
        {
            yield return new CheckProgress
            {
                EventType = "complete",
                Report = new CheckReport
                {
                    FileCount = 0,
                    TotalLinkCount = 0,
                    HealthyCount = 0,
                    BrokenCount = 0,
                    WarningCount = 0,
                    SkippedCount = 0,
                    TotalDuration = TimeSpan.Zero,
                    FileResults = Array.Empty<FileCheckResult>()
                }
            };
            yield break;
        }

        // Check file count limit
        if (filePaths.Count > _options.MaxFilesPerRepo)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = $"Repository 包含 {filePaths.Count} 個 Markdown 檔案，超過上限 {_options.MaxFilesPerRepo} 個檔案"
            };
            yield break;
        }

        // Emit progress: scanning complete
        yield return new CheckProgress
        {
            EventType = "progress",
            CheckedCount = 0,
            TotalCount = 0,
            CurrentFile = $"掃描完成：找到 {filePaths.Count} 個 Markdown 檔案"
        };

        // Build repoFiles set and anchorsMap
        var repoFiles = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
        var anchorsMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var markdownFiles = new List<MarkdownFile>();
        var allLinks = new List<Link>();

        // Fetch and parse all files
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await _gitHubRepoService.GetFileContentAsync(owner, repo, branch, filePath, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Skip files that can't be fetched
                continue;
            }

            var markdownFile = new MarkdownFile
            {
                FileName = System.IO.Path.GetFileName(filePath),
                RelativePath = filePath,
                Content = content,
                Links = Array.Empty<Link>()
            };
            markdownFiles.Add(markdownFile);

            // Parse links and anchors
            var links = _markdownParserService.ParseLinks(content, filePath);
            allLinks.AddRange(links);

            var anchors = _markdownParserService.ExtractAnchors(content);
            anchorsMap[filePath] = anchors;
        }

        // Check total link count limit
        if (allLinks.Count > _options.MaxLinksPerCheck)
        {
            yield return new CheckProgress
            {
                EventType = "error",
                ErrorMessage = $"Repository 總連結數 ({allLinks.Count}) 超過上限 {_options.MaxLinksPerCheck} 個連結"
            };
            yield break;
        }

        // Deduplicate external URLs
        var uniqueLinks = DeduplicateExternalUrls(allLinks);
        var totalCount = uniqueLinks.Count;
        var checkedCount = 0;

        // Process files and validate links
        var fileResults = new List<FileCheckResult>();

        foreach (var markdownFile in markdownFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get links for this file
            var fileLinks = allLinks.Where(l => l.SourceFile.RelativePath == markdownFile.RelativePath).ToList();
            var fileLinkResults = new List<LinkResult>();

            foreach (var link in fileLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if already validated (for deduplication)
                var existingResult = fileLinkResults.FirstOrDefault(r => 
                    r.Link.TargetUrl == link.TargetUrl && 
                    (link.Type == LinkType.ExternalUrl || link.Type == LinkType.Image));

                if (existingResult != null)
                {
                    // Reuse existing result for deduplicated external URLs
                    fileLinkResults.Add(existingResult);
                }
                else
                {
                    var result = await _linkValidatorService.ValidateAsync(link, repoFiles, anchorsMap, cancellationToken);
                    fileLinkResults.Add(result);

                    if (link.Type == LinkType.ExternalUrl || link.Type == LinkType.Image)
                    {
                        checkedCount++;

                        // Emit progress event
                        yield return new CheckProgress
                        {
                            EventType = "progress",
                            CheckedCount = checkedCount,
                            TotalCount = totalCount,
                            CurrentFile = markdownFile.RelativePath
                        };
                    }
                }
            }

            // Create file result
            var fileResult = new FileCheckResult
            {
                File = markdownFile,
                LinkResults = fileLinkResults,
                HealthyCount = fileLinkResults.Count(r => r.Status == LinkStatus.Healthy),
                BrokenCount = fileLinkResults.Count(r => r.Status == LinkStatus.Broken),
                WarningCount = fileLinkResults.Count(r => r.Status == LinkStatus.Warning),
                SkippedCount = fileLinkResults.Count(r => r.Status == LinkStatus.Skipped)
            };
            fileResults.Add(fileResult);

            // Emit file-result event
            yield return new CheckProgress
            {
                EventType = "file-result",
                FileResult = fileResult
            };
        }

        // Generate final report
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var report = _reportGeneratorService.GenerateReport(fileResults, stopwatch.Elapsed);

        // Emit complete event
        yield return new CheckProgress
        {
            EventType = "complete",
            Report = report
        };
    }

    /// <summary>
    /// Safely gets the default branch, returning error message instead of throwing
    /// </summary>
    private async Task<(string Branch, string? Error)> GetDefaultBranchSafeAsync(
        string owner, string repo, string? requestedBranch, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(requestedBranch))
        {
            return (requestedBranch, null);
        }

        try
        {
            var branch = await _gitHubRepoService!.GetDefaultBranchAsync(owner, repo, cancellationToken);
            return (branch, null);
        }
        catch (InvalidOperationException ex)
        {
            return (string.Empty, ex.Message);
        }
    }
}
