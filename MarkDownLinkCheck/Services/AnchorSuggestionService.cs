namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for suggesting anchor alternatives using Levenshtein distance.
/// </summary>
public class AnchorSuggestionService : IAnchorSuggestionService
{
    /// <summary>
    /// Suggests a valid anchor that is close to the invalid anchor.
    /// Returns null if no close match found (distance > 2).
    /// </summary>
    public string? Suggest(string invalidAnchor, IReadOnlyList<string> validAnchors)
    {
        if (string.IsNullOrEmpty(invalidAnchor) || validAnchors.Count == 0)
            return null;

        // Check for exact match first
        if (validAnchors.Contains(invalidAnchor))
            return invalidAnchor;

        // Find closest match with Levenshtein distance ≤ 2
        string? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (var validAnchor in validAnchors)
        {
            int distance = CalculateLevenshteinDistance(invalidAnchor, validAnchor);
            
            if (distance <= 2 && distance < bestDistance)
            {
                bestMatch = validAnchor;
                bestDistance = distance;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var distance = new int[source.Length + 1, target.Length + 1];

        // Initialize first column and row
        for (int i = 0; i <= source.Length; i++)
            distance[i, 0] = i;
        for (int j = 0; j <= target.Length; j++)
            distance[0, j] = j;

        // Calculate distances
        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // deletion
                        distance[i, j - 1] + 1),     // insertion
                    distance[i - 1, j - 1] + cost);  // substitution
            }
        }

        return distance[source.Length, target.Length];
    }
}
