using System.Drawing;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The description editor (#377): what a pending attachment shows, written for people who cannot see it. A screen
///     pushed over the compose screen it was opened from (ADR-0015), laid out as the prototype settled (#374) — the
///     picture top left, <c>Description (alt text)</c> over the field, and a counter against the instance's limit.
/// </summary>
/// <remarks>
///     What is written goes onto the attachment as it is typed, so there is nothing to keep or throw away when the
///     screen goes: <c>esc</c> and <c>ctrl-s</c> are both "done", and there is no cancel. Sending it to the instance is
///     the shell's, once the attachment is there to put it on (ADR-0026). The field itself is a widget, the view's; this
///     says where it goes, as the compose screen does for its editor.
/// </remarks>
/// <param name="compose">The compose screen the attachment is on, which is what holds its description.</param>
/// <param name="attachment">The pending attachment being described.</param>
public sealed class DescriptionScreen(ComposeScreen compose, ComposeAttachment attachment) : Screen
{
    /// <summary>What the field is labelled, the domain's word and the one people know it by (#372).</summary>
    public const string Label = "Description (alt text)";

    /// <summary>What an empty field says, dimly, where the first letter will go.</summary>
    public const string EmptyHint = "Say what it shows, for people who can't see it";

    /// <summary>The columns left blank either side of everything on the screen, as on compose (#317).</summary>
    private const int Pad = 2;

    /// <summary>
    ///     The narrowest content panel that has room for the picture beside the field rather than above it: half of it
    ///     still a field a sentence fits across.
    /// </summary>
    private const int WideFrom = 80;

    /// <summary>The fewest rows the field is ever left with: a line being written, and one either side of it.</summary>
    private const int LeastFieldRows = 3;

    /// <summary>
    ///     The most rows the picture takes above the label on a narrow panel: enough to see what is there, on a screen
    ///     whose business is the field under it (#382).
    /// </summary>
    private const int MostPictureRows = 10;

    /// <summary>The compose screen the attachment is on.</summary>
    public ComposeScreen Compose => compose;

    /// <summary>The pending attachment being described.</summary>
    public ComposeAttachment Attachment => attachment;

    /// <summary>What the description says so far.</summary>
    public string Description => attachment.Description;

    /// <inheritdoc />
    public override string Crumb => $"Describe {attachment.Name}";

    /// <inheritdoc />
    /// <remarks>Both "done": what was typed is kept either way.</remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => [new("esc", "done"), new("ctrl-s", "done")];

    /// <summary>The field changed: what the description says now, which goes straight onto the attachment.</summary>
    /// <returns>An edit, or nothing where the field says back what the attachment already holds.</returns>
    public ComposeChange Rewrite(string description) => compose.Media.Describe(attachment, description);

    /// <summary>
    ///     Where the field goes inside the content panel's viewport of <paramref name="viewport" />: under its label,
    ///     beside the picture on a wide panel and across it on a narrow one, down to the row above the counter — the
    ///     label and the field lower on a narrow panel where <paramref name="raster" /> draws the picture above them.
    /// </summary>
    public Rectangle FieldAt(Size viewport, Raster? raster = null) =>
        Laid(viewport.Width, viewport.Height, raster).Field;

    /// <inheritdoc />
    /// <remarks>
    ///     A blank under the panel's edge, the label, a blank, the field's rows — blank, the field being laid over them —
    ///     and the counter along the foot; on a narrow panel that draws, the picture's place and a blank above the
    ///     label. The picture itself is top left in its place, once its pixels are here (#382).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var laid = Laid(drawing.Width, drawing.Height, drawing.Raster);
        var place = laid.Picture;

        if (place.Width < 1 || place.Height < 1)
        {
            return laid.Rows;
        }

        var picture = FilePicture.Of(attachment.Path, place.X, place.Width, place.Height, drawing);
        var rows = laid.Rows.ToArray();

        rows[place.Y] = rows[place.Y] with
        {
            Insets = picture.Box is { } box ? [box] : [],
            Wants = picture.Wanted,
        };

        return rows;
    }

    /// <summary>
    ///     Whether a picture of what is being described goes on this screen at all, under <paramref name="raster" />: a
    ///     terminal that draws, and a picture this client decodes. Where not, a narrow panel holds no place for one.
    /// </summary>
    private bool Shows(Raster? raster) => raster?.Cell is not null && AttachmentPicture.Decodes(attachment.Path);

    /// <summary>
    ///     The screen laid out at <paramref name="width" /> by <paramref name="height" />: its rows, where the field
    ///     goes, and the place the picture goes in — top left, beside the field and level with its label on a wide panel;
    ///     above the label on a narrow one that shows it (#374, #382), and none on a narrow one that does not.
    /// </summary>
    /// <remarks>
    ///     The place is held from the first frame whether the pixels are here or not, so the label and the field never
    ///     move when they arrive. On a narrow panel it takes what the field can spare of its least, up to
    ///     <see cref="MostPictureRows" />, and is not held at all where that would be less than two rows.
    /// </remarks>
    private (IReadOnlyList<Line> Rows, Rectangle Field, Rectangle Picture) Laid(int width, int? height, Raster? raster)
    {
        var wide = width >= WideFrom;
        var at = wide ? (width / 2) + 1 : Pad;
        var across = Math.Max(0, width - at - Pad);
        var room = Math.Max(height ?? 0, 3 + LeastFieldRows + 1);

        // Above the label on a narrow panel: the place, then the blank that separates it from the label.
        var spare = room - 3 - LeastFieldRows - 1 - 1;
        var above = !wide && Shows(raster) && spare >= 2 ? Math.Min(MostPictureRows, spare) : 0;
        var top = above > 0 ? above + 1 : 0;
        var field = room - top - 3 - 1;

        IReadOnlyList<Line> rows =
        [
            .. Enumerable.Repeat(Line.Blank, top + 1),
            Line.Of(Gap(at), new Span(TextWrap.Clip(Label, across), Role.Muted)),
            Line.Blank,
            .. Enumerable.Repeat(Line.Blank, field),
            Counter(width),
        ];

        var picture = wide ? new Rectangle(Pad, 1, Math.Max(0, at - 1 - (Pad * 2)), room - 2)
            : above > 0 ? new Rectangle(Pad, 1, Math.Max(0, width - (Pad * 2)), above)
            : Rectangle.Empty;

        return (rows, new Rectangle(Math.Min(at, width), top + 3, across, field), picture);
    }

    /// <summary>
    ///     The counter, right-aligned inside the padding: how many characters the description has of the instance's
    ///     limit — muted while there is room, in the quota's low colour within the last tenth, and an error's past it,
    ///     so a description the instance would refuse is seen before it is sent. As the post's count is (#319).
    /// </summary>
    /// <remarks>Counted in characters as an instance counts them, which is one a code point.</remarks>
    private Line Counter(int width)
    {
        var limit = compose.Limits.Descriptions;
        var used = attachment.Description.EnumerateRunes().Count();
        var count = $"{used} / {limit}";

        var role = used > limit ? Role.Error
            : used > limit * 9 / 10 ? Role.QuotaLow
            : Role.Muted;

        return Line.Of(Gap(width - Pad - Glyphs.Columns(count)), new Span(count, role));
    }

    private static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);
}
