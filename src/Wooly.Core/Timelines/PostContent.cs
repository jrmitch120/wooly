using System.Net;
using System.Text;

namespace Wooly.Core.Timelines;

/// <summary>
///     Flattens the HTML an instance serves a post's text as into the plain text a terminal can print. Mastodon emits a
///     small, predictable subset — paragraphs, line breaks, and links — so this reads that subset rather than pulling
///     in an HTML parser for a job that never sees arbitrary markup.
/// </summary>
internal static class PostContent
{
    /// <summary>Turns <paramref name="html" /> into plain text, preserving where the lines were.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = new StringBuilder(html.Length);
        var insideTag = false;

        for (var index = 0; index < html.Length; index++)
        {
            var character = html[index];

            if (character == '<')
            {
                // A paragraph or a break is where a line ended, and dropping it silently would run two sentences of
                // the author's into one. Every other tag is formatting a terminal has no use for.
                text.Append(LineBreakFor(html, index));

                insideTag = true;

                continue;
            }

            if (character == '>')
            {
                insideTag = false;

                continue;
            }

            if (!insideTag)
            {
                text.Append(character);
            }
        }

        // Decoded last, so an entity for '<' in the user's own text cannot be read as a tag by the loop above.
        var plain = WebUtility.HtmlDecode(text.ToString());

        // A post is one block of text however many blank lines its HTML implied; three of them in a terminal is the
        // markup showing through.
        return CollapseBlankLines(plain).Trim();
    }

    /// <summary>
    ///     What the tag beginning at <paramref name="index" /> ends: a paragraph, which the author left a blank line
    ///     after, a line, which they did not, or nothing.
    /// </summary>
    private static string LineBreakFor(string html, int index)
    {
        var rest = html.AsSpan(index);

        if (rest.StartsWith("</p", StringComparison.OrdinalIgnoreCase))
        {
            return "\n\n";
        }

        return rest.StartsWith("<br", StringComparison.OrdinalIgnoreCase) ? "\n" : string.Empty;
    }

    private static string CollapseBlankLines(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd());
        var collapsed = new StringBuilder(text.Length);
        var blankRun = 0;

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                blankRun++;

                if (blankRun > 1)
                {
                    continue;
                }
            }
            else
            {
                blankRun = 0;
            }

            collapsed.Append(line).Append('\n');
        }

        return collapsed.ToString();
    }
}
