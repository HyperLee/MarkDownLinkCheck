using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkDownLinkCheck.Models;
using System.Text.RegularExpressions;

namespace MarkDownLinkCheck.Services;

/// <summary>
/// Service for parsing Markdown content and extracting links and anchors using Markdig.
/// </summary>
public class MarkdownParserService : IMarkdownParserService
{
    private const int MaxLinksPerFile = 1000;

    /// <summary>
    /// Parses Markdown content and extracts all links.
    /// </summary>
    public IReadOnlyList<Link> ParseLinks(string content, string fileName)
    {
        // Normalize line endings first so Markdig's Span positions match our line calculations
        var normalizedContent = content.Replace("\r\n", "\n").Replace("\r", "\n");
        
        var pipeline = new MarkdownPipelineBuilder()
            .UsePreciseSourceLocation()  // Enable precise line/column tracking for inlines
            .Build();
        var document = Markdown.Parse(normalizedContent, pipeline);
        
        var markdownFile = new MarkdownFile
        {
            FileName = fileName,
            RelativePath = fileName,
            Content = content
        };

        var links = new List<Link>();
        var lines = normalizedContent.Split('\n');
        ExtractLinksFromDocument(document, markdownFile, links, lines);

        // Limit to 1000 links per file
        if (links.Count > MaxLinksPerFile)
        {
            links = links.Take(MaxLinksPerFile).ToList();
        }

        return links;
    }

    /// <summary>
    /// Extracts all anchor IDs from Markdown headings (GitHub-style).
    /// </summary>
    public IReadOnlyList<string> ExtractAnchors(string content)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(content, pipeline);
        
        var anchors = new List<string>();
        ExtractAnchorsFromBlock(document, anchors);

        return anchors;
    }

    private void ExtractLinksFromDocument(MarkdownObject obj, MarkdownFile markdownFile, List<Link> links, string[] lines)
    {
        if (obj is ContainerBlock containerBlock)
        {
            foreach (var child in containerBlock)
            {
                // Skip code blocks
                if (child is FencedCodeBlock || child is CodeBlock)
                    continue;

                // Skip HTML blocks that might contain comments
                if (child is HtmlBlock htmlBlock)
                {
                    if (htmlBlock.Lines.ToString().Contains("<!--"))
                        continue;
                }

                ExtractLinksFromDocument(child, markdownFile, links, lines);
            }
        }
        else if (obj is LeafBlock leafBlock)
        {
            if (leafBlock.Inline != null)
            {
                // Use leafBlock.Line which is 0-based, add 1 for 1-based line numbers
                // Markdig stores line info, leafBlock.Line gives us the starting line of this block
                var baseLineNumber = leafBlock.Line + 1;
                ExtractLinksFromInline(leafBlock.Inline, markdownFile, links, baseLineNumber, lines);
            }
        }
    }

    private void ExtractLinksFromInline(Inline inline, MarkdownFile markdownFile, List<Link> links, int lineNumber, string[] lines)
    {
        var current = inline;
        while (current != null)
        {
            // Skip inline code
            if (current is CodeInline)
            {
                current = current.NextSibling;
                continue;
            }

            // Skip HTML inline that might contain comments
            if (current is HtmlInline htmlInline && htmlInline.Tag.Contains("<!--"))
            {
                current = current.NextSibling;
                continue;
            }

            if (current is LinkInline linkInline)
            {
                if (!string.IsNullOrEmpty(linkInline.Url))
                {
                    // Use the inline's Line property (0-based), add 1 for 1-based line numbers
                    var linkLineNumber = linkInline.Line + 1;
                    
                    var link = new Link
                    {
                        TargetUrl = linkInline.Url,
                        RawText = GetLinkRawText(linkInline),
                        LineNumber = linkLineNumber,
                        SourceFile = markdownFile,
                        IsImage = linkInline.IsImage
                    };

                    link.Type = Link.DetermineLinkType(link.TargetUrl, link.IsImage);
                    links.Add(link);
                }
            }
            else if (current is AutolinkInline autolinkInline)
            {
                if (!string.IsNullOrEmpty(autolinkInline.Url))
                {
                    // Use the inline's Line property (0-based), add 1 for 1-based line numbers
                    var linkLineNumber = autolinkInline.Line + 1;
                    
                    var link = new Link
                    {
                        TargetUrl = autolinkInline.Url,
                        RawText = $"<{autolinkInline.Url}>",
                        LineNumber = linkLineNumber,
                        SourceFile = markdownFile,
                        IsImage = false
                    };

                    link.Type = Link.DetermineLinkType(link.TargetUrl, link.IsImage);
                    links.Add(link);
                }
            }

            // Recursively process child inlines
            if (current is ContainerInline containerInline && containerInline.FirstChild != null)
            {
                ExtractLinksFromInline(containerInline.FirstChild, markdownFile, links, lineNumber, lines);
            }

            current = current.NextSibling;
        }
    }

    private int GetLineNumberFromPosition(int position, string[] lines)
    {
        int currentPos = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int lineEnd = currentPos + lines[i].Length;
            if (position >= currentPos && position < lineEnd)
            {
                return i + 1; // 1-based line number
            }
            currentPos = lineEnd + 1; // +1 for newline character
        }
        return lines.Length; // Fallback to last line
    }

    private string GetLinkRawText(LinkInline linkInline)
    {
        // Try to get the label/text content
        var label = GetInlineText(linkInline);
        var prefix = linkInline.IsImage ? "!" : "";
        return $"{prefix}[{label}]({linkInline.Url})";
    }

    private string GetInlineText(ContainerInline inline)
    {
        var text = new System.Text.StringBuilder();
        var current = inline.FirstChild;
        
        while (current != null)
        {
            if (current is LiteralInline literalInline)
            {
                text.Append(literalInline.Content);
            }
            else if (current is ContainerInline containerInline && containerInline.FirstChild != null)
            {
                text.Append(GetInlineText(containerInline));
            }

            current = current.NextSibling;
        }

        return text.ToString();
    }

    private void ExtractAnchorsFromBlock(MarkdownObject obj, List<string> anchors)
    {
        if (obj is ContainerBlock containerBlock)
        {
            foreach (var child in containerBlock)
            {
                ExtractAnchorsFromBlock(child, anchors);
            }
        }
        else if (obj is HeadingBlock headingBlock)
        {
            var headingText = GetHeadingText(headingBlock);
            if (!string.IsNullOrEmpty(headingText))
            {
                var anchor = GenerateGitHubStyleAnchor(headingText);
                anchors.Add(anchor);
            }
        }
    }

    private string GetHeadingText(HeadingBlock headingBlock)
    {
        if (headingBlock.Inline == null)
            return string.Empty;

        var text = new System.Text.StringBuilder();
        foreach (var inline in headingBlock.Inline)
        {
            if (inline is LiteralInline literalInline)
            {
                text.Append(literalInline.Content);
            }
            else if (inline is LinkInline linkInline)
            {
                // Get link text, not URL
                foreach (var child in linkInline)
                {
                    if (child is LiteralInline childLiteral)
                    {
                        text.Append(childLiteral.Content);
                    }
                }
            }
            else if (inline is EmphasisInline emphasisInline)
            {
                foreach (var child in emphasisInline)
                {
                    if (child is LiteralInline childLiteral)
                    {
                        text.Append(childLiteral.Content);
                    }
                }
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Generates a GitHub-style anchor from heading text.
    /// Rules: lowercase, replace spaces with hyphens, remove special chars except hyphens.
    /// </summary>
    private string GenerateGitHubStyleAnchor(string headingText)
    {
        // Convert to lowercase
        var anchor = headingText.ToLowerInvariant();

        // Replace spaces with hyphens
        anchor = anchor.Replace(' ', '-');

        // Remove special characters except hyphens and alphanumeric
        anchor = Regex.Replace(anchor, @"[^a-z0-9\-]", "");

        // Remove consecutive hyphens
        anchor = Regex.Replace(anchor, @"-+", "-");

        // Trim hyphens from start and end
        anchor = anchor.Trim('-');

        return anchor;
    }
}
