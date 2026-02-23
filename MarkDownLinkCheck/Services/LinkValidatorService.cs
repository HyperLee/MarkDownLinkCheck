using MarkDownLinkCheck.Models;
using System.Text.RegularExpressions;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for validating individual links.
/// </summary>
public class LinkValidatorService : ILinkValidatorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAnchorSuggestionService _anchorSuggestionService;

    public LinkValidatorService(IHttpClientFactory httpClientFactory, IAnchorSuggestionService anchorSuggestionService)
    {
        _httpClientFactory = httpClientFactory;
        _anchorSuggestionService = anchorSuggestionService;
    }

    public async Task<LinkResult> ValidateAsync(Link link, IReadOnlySet<string>? repoFiles, 
        IReadOnlyDictionary<string, IReadOnlyList<string>>? anchorsMap, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new LinkResult { Link = link };

        try
        {
            switch (link.Type)
            {
                case LinkType.ExternalUrl:
                case LinkType.Image:
                    result = await ValidateExternalUrlAsync(link, cancellationToken);
                    break;

                case LinkType.Anchor:
                    result = ValidateAnchor(link, anchorsMap);
                    break;

                case LinkType.Email:
                    result = ValidateEmail(link);
                    break;

                case LinkType.RelativePath:
                    result = ValidateRelativePath(link, repoFiles, anchorsMap);
                    break;

                default:
                    result.Status = LinkStatus.Broken;
                    result.ErrorMessage = "Unknown link type";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.Status = LinkStatus.Broken;
            result.ErrorMessage = ex.Message;
            result.ErrorType = "exception";
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private async Task<LinkResult> ValidateExternalUrlAsync(Link link, CancellationToken cancellationToken)
    {
        var result = new LinkResult { Link = link };
        var httpClient = _httpClientFactory.CreateClient("LinkChecker");

        try
        {
            // Try HEAD request first
            var headRequest = new HttpRequestMessage(HttpMethod.Head, link.TargetUrl);
            var response = await httpClient.SendAsync(headRequest, cancellationToken);

            // If HEAD returns 405, fallback to GET
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                var getRequest = new HttpRequestMessage(HttpMethod.Get, link.TargetUrl);
                response = await httpClient.SendAsync(getRequest, cancellationToken);
            }

            result.HttpStatusCode = (int)response.StatusCode;

            // Map status codes to LinkStatus
            if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently) // 301
            {
                result.Status = LinkStatus.Warning;
                result.RedirectUrl = response.Headers.Location?.ToString();
                result.ErrorMessage = "Permanent redirect";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Found || // 302
                     response.StatusCode == System.Net.HttpStatusCode.SeeOther || // 303
                     response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect) // 307
            {
                result.Status = LinkStatus.Healthy;
            }
            else if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                result.Status = LinkStatus.Healthy;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                result.Status = LinkStatus.Warning;
                result.ErrorMessage = "Rate limited";
            }
            else if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 600)
            {
                result.Status = LinkStatus.Broken;
                result.ErrorMessage = response.ReasonPhrase ?? "HTTP error";
            }
            else
            {
                result.Status = LinkStatus.Broken;
                result.ErrorMessage = $"Unexpected status code: {response.StatusCode}";
            }
        }
        catch (TaskCanceledException)
        {
            result.Status = LinkStatus.Warning;
            result.ErrorType = "timeout";
            result.ErrorMessage = "Request timeout";
        }
        catch (HttpRequestException ex)
        {
            result.Status = LinkStatus.Broken;
            result.ErrorType = "http_error";
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private LinkResult ValidateAnchor(Link link, IReadOnlyDictionary<string, IReadOnlyList<string>>? anchorsMap)
    {
        var result = new LinkResult { Link = link };

        // Get anchors for the current file
        var fileKey = link.SourceFile.RelativePath;
        if (anchorsMap != null && anchorsMap.TryGetValue(fileKey, out var anchors))
        {
            var anchorId = link.TargetUrl.TrimStart('#');
            
            if (anchors.Contains(anchorId))
            {
                result.Status = LinkStatus.Healthy;
            }
            else
            {
                result.Status = LinkStatus.Broken;
                result.ErrorType = "anchor_not_found";
                result.ErrorMessage = "anchor not found";
                
                // Try to suggest a similar anchor
                result.AnchorSuggestion = _anchorSuggestionService.Suggest(anchorId, anchors);
            }
        }
        else
        {
            // No anchors available for this file
            result.Status = LinkStatus.Broken;
            result.ErrorType = "anchor_not_found";
            result.ErrorMessage = "anchor not found";
        }

        return result;
    }

    private LinkResult ValidateEmail(Link link)
    {
        var result = new LinkResult { Link = link };

        // Extract email from mailto: URL
        var email = link.TargetUrl.Replace("mailto:", "", StringComparison.OrdinalIgnoreCase);

        // Simple email validation
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        
        if (emailRegex.IsMatch(email))
        {
            result.Status = LinkStatus.Healthy;
        }
        else
        {
            result.Status = LinkStatus.Broken;
            result.ErrorType = "invalid_email";
            result.ErrorMessage = "Invalid email format";
        }

        return result;
    }

    private LinkResult ValidateRelativePath(Link link, IReadOnlySet<string>? repoFiles, 
        IReadOnlyDictionary<string, IReadOnlyList<string>>? anchorsMap = null)
    {
        var result = new LinkResult { Link = link };

        // If repoFiles is null, we're in Markdown source mode - skip relative paths
        if (repoFiles == null)
        {
            result.Status = LinkStatus.Skipped;
            result.ErrorMessage = "Relative path skipped in Markdown mode";
            return result;
        }

        // Check if the target contains an anchor (e.g., "../docs/guide.md#section")
        var targetUrl = link.TargetUrl;
        string? anchorPart = null;
        string filePart = targetUrl;

        if (targetUrl.Contains('#'))
        {
            var parts = targetUrl.Split('#', 2);
            filePart = parts[0];
            anchorPart = parts[1];
        }

        // Normalize the path
        var normalizedPath = NormalizeRelativePath(link.SourceFile.RelativePath, filePart);

        // Check if file exists in repo
        if (!repoFiles.Contains(normalizedPath))
        {
            result.Status = LinkStatus.Broken;
            result.ErrorType = "file_not_found";
            result.ErrorMessage = "File not found in repository";
            return result;
        }

        // If there's an anchor part, validate it
        if (!string.IsNullOrEmpty(anchorPart) && anchorsMap != null)
        {
            if (anchorsMap.TryGetValue(normalizedPath, out var anchors))
            {
                if (anchors.Contains(anchorPart))
                {
                    result.Status = LinkStatus.Healthy;
                }
                else
                {
                    result.Status = LinkStatus.Broken;
                    result.ErrorType = "anchor_not_found";
                    result.ErrorMessage = "anchor not found in target file";
                    
                    // Suggest similar anchor
                    result.AnchorSuggestion = _anchorSuggestionService.Suggest(anchorPart, anchors);
                }
            }
            else
            {
                // File exists but no anchors available
                result.Status = LinkStatus.Warning;
                result.ErrorMessage = "Unable to validate anchor (file has no anchors)";
            }
        }
        else
        {
            // No anchor to validate, file exists
            result.Status = LinkStatus.Healthy;
        }

        return result;
    }

    /// <summary>
    /// Normalizes a relative path from source file to target file
    /// </summary>
    private string NormalizeRelativePath(string sourceFilePath, string targetPath)
    {
        // Remove leading ./ or /
        targetPath = targetPath.TrimStart('.', '/');

        // If target path doesn't contain ../, it's already relative to repo root
        if (!targetPath.Contains("../"))
        {
            return targetPath;
        }

        // Handle ../ relative paths
        var sourceDir = System.IO.Path.GetDirectoryName(sourceFilePath)?.Replace('\\', '/') ?? "";
        var targetParts = targetPath.Split('/');
        var sourceParts = sourceDir.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var sourceList = sourceParts.ToList();

        foreach (var part in targetParts)
        {
            if (part == "..")
            {
                if (sourceList.Count > 0)
                {
                    sourceList.RemoveAt(sourceList.Count - 1);
                }
            }
            else if (part != ".")
            {
                sourceList.Add(part);
            }
        }

        return string.Join("/", sourceList);
    }
}
