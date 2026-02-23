namespace MarkDownLinkCheck.Models;

/// <summary>
/// Represents the type of link found in Markdown content.
/// </summary>
public enum LinkType
{
    /// <summary>
    /// External HTTP/HTTPS URL
    /// </summary>
    ExternalUrl = 0,
    
    /// <summary>
    /// Relative path within repository (e.g., ./docs/setup.md)
    /// </summary>
    RelativePath = 1,
    
    /// <summary>
    /// Anchor link (e.g., #installation)
    /// </summary>
    Anchor = 2,
    
    /// <summary>
    /// Email link (mailto:)
    /// </summary>
    Email = 3,
    
    /// <summary>
    /// Image link (![alt](url))
    /// </summary>
    Image = 4
}
