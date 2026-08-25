using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The keys the screen underneath answers to, plus the frame's own. What <c>?</c> opens onto — the spec has no
///     in-app help story, and this is the shell adding one so that every screen #29, #30 and #31 bring gets it for
///     free (<c>docs/tui-shell.md</c>).
/// </summary>
public sealed class HelpScreen(Screen about) : Screen
{
    /// <summary>
    ///     The keys that mean the same thing everywhere. What may vary from screen to screen is everything else; these
    ///     are the frame, and a reader has to be able to rely on them.
    /// </summary>
    private static readonly IReadOnlyList<KeyHint> Frame =
    [
        new("esc", "up one level — never quits"),
        new("ctrl-q", "quit"),
        new("?", "these keys"),
        new("tab / shift-tab", "move the rail's cursor; it settles onto a destination"),
    ];

    /// <inheritdoc />
    public override string Crumb => "keys";

    /// <inheritdoc />
    /// <remarks>
    ///     The arrows, because a keymap is often taller than the terminal and this is the screen a reader arrives at
    ///     precisely because they do not know how to move around yet.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => [PostKeys.Scrolling, new("esc", "back")];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line> { Line.Of($"On {about.Crumb}", Role.BylineName), Line.Blank };

        lines.AddRange(about.Keys.Select(key => Row(key, drawing.Width)));
        lines.Add(Line.Blank);
        lines.Add(Line.Of("Everywhere", Role.BylineName));
        lines.Add(Line.Blank);
        lines.AddRange(Frame.Select(key => Row(key, drawing.Width)));

        return lines;
    }

    /// <summary>The key in one column and what it does in the next, so a reader can scan down either.</summary>
    private static Line Row(KeyHint key, int width)
    {
        const int keyColumn = 16;
        var padded = key.Key.PadRight(keyColumn);

        return Line.Of([
            new Span(padded, Role.BylineHandle),
            new Span(TextWrap.Clip(key.Does, Math.Max(0, width - padded.Length)), Role.Body),
        ]);
    }
}
