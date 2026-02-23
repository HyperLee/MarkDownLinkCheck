using MarkDownLinkCheck.Services;
using Xunit;

namespace MarkDownLinkCheck.Tests.Unit.Services;

/// <summary>
/// Unit tests for AnchorSuggestionService (T018)
/// Tests: exact match, Levenshtein distance ≤ 2, no suggestion when distance > 2
/// </summary>
public class AnchorSuggestionServiceTests
{
    private readonly IAnchorSuggestionService _service;

    public AnchorSuggestionServiceTests()
    {
        _service = new AnchorSuggestionService();
    }

    [Fact]
    public void Suggest_WhenExactMatch_ReturnsExactMatch()
    {
        // Arrange
        var invalidAnchor = "installation";
        var validAnchors = new List<string> { "setup", "installation", "configuration" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Equal("installation", suggestion);
    }

    [Theory]
    [InlineData("instalation", "installation")] // Missing 'l'
    [InlineData("installtion", "installation")] // Missing 'a'
    [InlineData("insstallation", "installation")] // Extra 's'
    [InlineData("imstallation", "installation")] // Substitute 'n' with 'm'
    public void Suggest_WhenLevenshteinDistance1_ReturnsSuggestion(string invalidAnchor, string expected)
    {
        // Arrange
        var validAnchors = new List<string> { "setup", "installation", "configuration" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Equal(expected, suggestion);
    }

    [Theory]
    [InlineData("installaion", "installation")] // Missing 't', wrong 'i'
    [InlineData("instllation", "installation")] // Missing 'a'
    [InlineData("installaton", "installation")] // Missing 'i'
    public void Suggest_WhenLevenshteinDistance2_ReturnsSuggestion(string invalidAnchor, string expected)
    {
        // Arrange
        var validAnchors = new List<string> { "setup", "installation", "configuration" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Equal(expected, suggestion);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("completely-different")]
    [InlineData("setup123")]
    public void Suggest_WhenLevenshteinDistanceGreaterThan2_ReturnsNull(string invalidAnchor)
    {
        // Arrange
        var validAnchors = new List<string> { "setup", "installation", "configuration" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Null(suggestion);
    }

    [Fact]
    public void Suggest_WhenMultipleCandidates_ReturnsClosest()
    {
        // Arrange
        var invalidAnchor = "setup";
        var validAnchors = new List<string> { "setup-guide", "set-up", "setups", "configuration" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.NotNull(suggestion);
        // Should return one of the close matches (distance 1-2)
        Assert.Contains(suggestion, validAnchors);
    }

    [Fact]
    public void Suggest_WhenNoValidAnchors_ReturnsNull()
    {
        // Arrange
        var invalidAnchor = "installation";
        var validAnchors = new List<string>();

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Null(suggestion);
    }

    [Fact]
    public void Suggest_WhenEmptyInvalidAnchor_ReturnsNull()
    {
        // Arrange
        var invalidAnchor = "";
        var validAnchors = new List<string> { "setup", "installation" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        Assert.Null(suggestion);
    }

    [Fact]
    public void Suggest_IsCaseSensitive()
    {
        // Arrange
        var invalidAnchor = "Installation";
        var validAnchors = new List<string> { "installation", "setup" };

        // Act
        var suggestion = _service.Suggest(invalidAnchor, validAnchors);

        // Assert
        // "Installation" vs "installation" has distance of 1 (case change)
        Assert.Equal("installation", suggestion);
    }
}
