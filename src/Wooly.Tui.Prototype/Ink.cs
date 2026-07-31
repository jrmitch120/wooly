using Terminal.Gui.Drawing;
using Wooly.Core.Posts;

namespace Wooly.Tui.Prototype;

/// <summary>
///     Text and colour scraps every variant needs. Formatting only — no layout, so a variant is still free to lay
///     itself out any way it likes.
/// </summary>
internal static class Ink
{
    public static readonly Attribute Dim = new(StandardColor.BrightBlack, StandardColor.Black);
    public static readonly Attribute Body = new(StandardColor.Silver, StandardColor.Black);
    public static readonly Attribute Author = new(StandardColor.White, StandardColor.Black);
    public static readonly Attribute Handle = new(StandardColor.BrightBlue, StandardColor.Black);
    public static readonly Attribute Warning = new(StandardColor.BrightYellow, StandardColor.Black);
    public static readonly Attribute Boosted = new(StandardColor.BrightGreen, StandardColor.Black);
    public static readonly Attribute Favorited = new(StandardColor.BrightMagenta, StandardColor.Black);
    public static readonly Attribute Selected = new(StandardColor.White, StandardColor.Blue);
    public static readonly Attribute SelectedDim = new(StandardColor.BrightCyan, StandardColor.Blue);
    public static readonly Attribute Chrome = new(StandardColor.Black, StandardColor.Cyan);
    public static readonly Attribute Rail = new(StandardColor.Silver, StandardColor.Black);
    public static readonly Attribute RailOn = new(StandardColor.Black, StandardColor.BrightCyan);
    public static readonly Attribute Badge = new(StandardColor.BrightRed, StandardColor.Black);
    public static readonly Attribute Prototype = new(StandardColor.Black, StandardColor.BrightMagenta);

    /// <summary>How long ago, the way a timeline says it: 2m, 4h, 3d.</summary>
    public static string Ago(DateTimeOffset when)
    {
        var span = Sample.Now - when;

        return span switch
        {
            { TotalMinutes: < 1 } => "now",
            { TotalHours: < 1 } => $"{(int)span.TotalMinutes}m",
            { TotalDays: < 1 } => $"{(int)span.TotalHours}h",
            _ => $"{(int)span.TotalDays}d",
        };
    }

    /// <summary>The glyph a visibility gets, so a reader can see the audience without reading a word.</summary>
    public static string Audience(PostVisibility visibility) => visibility switch
    {
        PostVisibility.Public => "○",
        PostVisibility.Unlisted => "◌",
        PostVisibility.Private => "●",
        PostVisibility.Direct => "✉",
        _ => "?",
    };

    public static string Clip(string text, int width)
    {
        var flat = text.ReplaceLineEndings(" ").TrimEnd();

        if (width <= 1)
        {
            return string.Empty;
        }

        return flat.Length <= width ? flat : string.Concat(flat.AsSpan(0, width - 1), "…");
    }

    /// <summary>Wraps at whole words, which is the only wrapping a post's text ever needs.</summary>
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        var lines = new List<string>();

        if (width <= 0)
        {
            return lines;
        }

        foreach (var paragraph in text.Split('\n'))
        {
            var line = string.Empty;

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length == 0)
                {
                    line = word;
                }
                else if (line.Length + 1 + word.Length <= width)
                {
                    line = $"{line} {word}";
                }
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }

            lines.Add(line);
        }

        return lines;
    }
}
