using MarkDownLinkCheck.Models;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for validating individual links.
/// </summary>
public interface ILinkValidatorService
{
    /// <summary>
    /// Validates a single link and returns the result.
    /// </summary>
    /// <param name="link">Link to validate</param>
    /// <param name="repoFiles">Set of file paths in the repository (for relative path validation)</param>
    /// <param name="anchorsMap">Map of file paths to their anchors (for cross-file anchor validation)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Link validation result</returns>
    Task<LinkResult> ValidateAsync(
        Link link, 
        IReadOnlySet<string>? repoFiles, 
        IReadOnlyDictionary<string, IReadOnlyList<string>>? anchorsMap, 
        CancellationToken cancellationToken);
}
