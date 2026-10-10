using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The attachments screen (story 58): what is attached to a compose, a row each, on a screen pushed over it where a
///     terminal too short for the rows folded them into the Media header's line — so that on a short terminal nothing
///     attached is out of reach of describing, removing, bringing back, reordering or retrying (review of #372).
/// </summary>
/// <remarks>
///     A list over the compose screen's own draft rather than a copy of it: the rows are the compose's, drawn as its
///     header draws them, and every key and click here changes the compose, through the shell, as the same key on a row
///     under its header does — so what is sent is what was changed here, and coming back finds the header saying so.
///     The walk is the compose's pick of a row; leaving puts it back on the header (<see cref="Left" />).
/// </remarks>
/// <param name="compose">The compose screen whose attachments these are.</param>
public sealed class AttachmentsScreen(ComposeScreen compose) : Screen
{
    /// <summary>The columns left blank either side, as on the compose screen under it.</summary>
    private const int Pad = 2;

    /// <summary>The compose screen whose attachments these are.</summary>
    public ComposeScreen Compose => compose;

    /// <summary>What the compose holds under its Media header, which this lists (#387).</summary>
    public ComposeMedia Media => compose.Media;

    /// <inheritdoc />
    /// <remarks>The header's own word, under the compose it lists the rows of.</remarks>
    public override string Crumb => "Media";

    /// <inheritdoc />
    protected override IPicked Walking { get; } = new Rows(compose);

    /// <inheritdoc />
    /// <remarks>
    ///     What a row under the header offers, and what the header does: describe, add, toggle sensitive — those that act
    ///     on a row off the status row once every row is taken off.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("⏎", "describe", NeedsAPick: true),
        .. Media.Keys(compose.Look),
        new("s", "sensitive", NeedsAPick: true),
        .. Media.Room > 0
            ? [new KeyHint("ctrl-o", "add media"), ComposeScreen.PasteKeys]
            : Array.Empty<KeyHint>(),
        new("esc", "back"),
    ];

    /// <inheritdoc />
    /// <remarks>
    ///     The Media header's line, unfolded; a blank; then a row each, the picked one carrying the selection bar against
    ///     its grip and nothing else, as under the header — not on the band, which lit its name as a second thing picked
    ///     (review of #372). The bar is also what the page follows (<see cref="Rendering.Scroll" />).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line> { Line.Blank, Media.ListedHeader(drawing.Width, compose.Look), Line.Blank };

        for (var at = 0; at < Media.Attachments.Count; at++)
        {
            lines.Add(Media.ListedRow(Media.Attachments[at], drawing.Width, drawing, compose.Look).PartOf(at));
        }

        if (Media.Attachments.Count == 0)
        {
            lines.Add(Line.Of(
                new Span(new string(' ', Pad + 2), Role.Body),
                new Span("nothing attached", Role.Muted)));
        }

        return lines;
    }

    /// <summary>
    ///     A click on a row's <c>x</c> takes it off, on its <c>retry (r)</c> sends it up again, on its description or
    ///     quiet mark opens the description editor, and on the header's toggle puts what is attached behind a click — as
    ///     the same clicks do under the header. Anywhere else on a row picks it.
    /// </summary>
    public override Verb Clicked(int? item, int? part, bool chorded) => (AttachmentPart?)part switch
    {
        AttachmentPart.Remove when item is not null => Verb.RemoveAttachment,
        AttachmentPart.Retry when item is not null => Verb.RetryAttachment,
        AttachmentPart.Description when item is not null => Verb.Describe,
        AttachmentPart.Sensitive => Verb.ToggleSensitive,
        _ => Verb.None,
    };

    /// <inheritdoc />
    /// <remarks>The compose's walk goes back onto the header, whose line is all that draws of the rows.</remarks>
    public override void Left() => _ = compose.OffTheRows();

    /// <summary>The rows, walked by the compose's own pick of one, so that its keys act on the row picked here.</summary>
    private sealed class Rows(ComposeScreen compose) : IPicked
    {
        private ComposeMedia Media => compose.Media;

        public int At => Media.Picked is { } picked ? IndexOf(picked) : 0;

        public int Count => Media.Attachments.Count;

        public void Move(int by) => Pick(At + by);

        public void Pick(int at)
        {
            if (Count > 0)
            {
                compose.Pick(Media.Attachments[Math.Clamp(at, 0, Count - 1)]);
            }
        }

        private int IndexOf(ComposeAttachment attachment)
        {
            for (var at = 0; at < Count; at++)
            {
                if (ReferenceEquals(Media.Attachments[at], attachment))
                {
                    return at;
                }
            }

            return 0;
        }
    }
}
