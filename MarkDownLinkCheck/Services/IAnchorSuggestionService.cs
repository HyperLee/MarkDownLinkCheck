namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for suggesting anchor alternatives using Levenshtein distance.
/// </summary>
public interface IAnchorSuggestionService
{
    /// <summary>
    /// Suggests a valid anchor that is close to the invalid anchor.
    /// Returns null if no close match found (distance > 2).
    /// </summary>
    /// <param name="invalidAnchor">The invalid anchor</param>
    /// <param name="validAnchors">List of valid anchors</param>
    /// <returns>Suggested anchor or null</returns>
    string? Suggest(string invalidAnchor, IReadOnlyList<string> validAnchors);
}
