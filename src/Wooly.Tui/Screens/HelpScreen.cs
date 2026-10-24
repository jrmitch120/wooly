using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
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
        new("` / ~", "move it to the next or previous rail group's first destination"),
        new("ctrl-p", "profiles: who this session is acting as"),
        // Listed here and on no status row: set and forgotten, they would take room on every screen (#414).
        new("- / + or =", "narrow or widen the shell by 10%"),
        // On no status row either, for the same reason, but where the rail is hidden (#441, #445).
        new("\\", "hide or show the rail"),
        // The pointer: it adds no keys of its own, only another way to ask for these (#286). An action a row, its input
        // in the key column as a key's is (#446).
        new("wheel", "scroll the content"),
        new("click", "arrive on the rail; pick a row"),
        new("double click", "read (⏎)"),
        new("right click", "up one level, as esc"),
        new("click a crumb", "walk back to it"),
        new("click «/»", "hide or show the rail"),
    ];

    /// <summary>The screen whose keys these are.</summary>
    public Screen About => about;

    /// <inheritdoc />
    public override string Crumb => "Keys";

    /// <inheritdoc />
    /// <remarks>
    ///     The arrows, because a keymap is often taller than the terminal and this is the screen a reader arrives at
    ///     precisely because they do not know how to move around yet.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => [new("esc", "back"), PostKeys.Scrolling];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line> { Line.Of($"On {about.Crumb}", Role.BylineName), Line.Blank };

        lines.AddRange(about.Keys.SelectMany(key => Rows(key, drawing.Width)));
        lines.Add(Line.Blank);
        lines.Add(Line.Of("Everywhere", Role.BylineName));
        lines.Add(Line.Blank);
        lines.AddRange(Frame.SelectMany(key => Rows(key, drawing.Width)));

        return lines;
    }

    /// <summary>The key in one column and what it does in the next, so a reader can scan down either.</summary>
    /// <remarks>
    ///     The key takes <see cref="Role.Key" /> and the padding after it does not (#221): a themer who gives
    ///     <c>key</c> a background would otherwise get a band across every row rather than a highlighted key. A meaning
    ///     too long for the width carries on to the rows under it, each starting in the meaning column, rather than
    ///     being cut (#446): help is the one screen a reader comes to for every word.
    /// </remarks>
    private static IEnumerable<Line> Rows(KeyHint key, int width)
    {
        const int keyColumn = 16;
        var columns = Glyphs.Columns(key.Key);
        var spacer = new string(' ', Math.Max(0, keyColumn - columns));
        var used = columns + spacer.Length;
        var meaning = TextWrap.Wrap(key.Does, Math.Max(0, width - used));

        yield return Line.Of([
            new Span(key.Key, Role.Key),
            new Span(spacer, Role.Body),
            new Span(meaning.Count > 0 ? meaning[0] : string.Empty, Role.Body),
        ]);

        // A row carried on from the one above has no key, and no empty run of the key's role standing in for one.
        foreach (var row in meaning.Skip(1))
        {
            yield return Line.Of([new Span(new string(' ', used), Role.Body), new Span(row, Role.Body)]);
        }
    }
}
