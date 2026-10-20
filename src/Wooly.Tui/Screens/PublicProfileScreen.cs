using System.Drawing;
using Wooly.Core.Accounts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The form that edits the own account's public profile (#406, ADR-0028): a screen on the stack, pushed over
///     <b>You</b> by <c>E</c> or its "edit public profile" row, opened on the public profile as the account wrote it.
/// </summary>
/// <remarks>
///     As inert as compose (ADR-0015): it holds what is being written and says what a save would send, and reaches no
///     port. The shell reads the public profile before it opens and sends what it says on <c>ctrl-s</c>.
///     <para>
///         Laid out as compose's headers are — a word right-aligned in a column of its own, then the value — so the two
///         screens that take writing read alike. The fields themselves are widgets laid over the rows kept for them
///         (the view's); the screen paints what each holds under it, so the form reads the same to a test with no
///         terminal behind it, and says where each goes from the one layout that paints the rows.
///     </para>
/// </remarks>
public sealed class PublicProfileScreen : Screen, IDraft
{
    /// <summary>The columns left blank either side of everything on the screen, as on compose (#317).</summary>
    private const int Pad = 2;

    /// <summary>How wide the right-aligned label column is: wide enough for <c>Fields</c>, the widest word on it.</summary>
    private const int LabelWidth = 6;

    /// <summary>The columns between a label and its value, and between a custom field's label and its value.</summary>
    private const int Gap = 2;

    /// <summary>The fewest rows the bio's editor is ever left with: a line being written, and one either side of it.</summary>
    private const int LeastBioRows = 3;

    /// <summary>The most rows the bio's editor is given, however tall the terminal: what is under it matters too.</summary>
    private const int MostBioRows = 8;

    /// <summary>How many rows the bio's editor takes where nobody says how tall the room is.</summary>
    private const int BioRowsUnmeasured = 4;

    /// <summary>
    ///     How many rows the avatar and the header each take on a terminal that draws pictures (#407): enough to see what
    ///     it is, and few enough that the bio keeps its rows on a short terminal. One where nothing is drawn.
    /// </summary>
    internal const int PictureRows = 2;

    /// <summary>How wide the avatar's box is: about square, beside two rows of cells twice as tall as they are wide.</summary>
    private const int AvatarColumns = 4;

    /// <summary>
    ///     How wide the header's box is: three times the avatar's, the shape Mastodon crops a header to. The words beside
    ///     both pictures start after it, so that they line up and a picture arriving moves neither.
    /// </summary>
    internal const int HeaderColumns = 12;

    /// <summary>What a picture row says where nothing has been chosen and the instance has a picture.</summary>
    public const string CurrentPicture = "current";

    /// <summary>What a picture row says where nothing has been chosen and the instance has no picture.</summary>
    public const string NoPicture = "none";

    /// <summary>What a picture row says after the name of the file chosen for it: that nothing has gone up yet.</summary>
    public const string SentOnSave = " · sent on save";

    /// <summary>What a field says, dimly, while it holds nothing — the label half and the value half.</summary>
    public const string LabelHint = "label";

    /// <inheritdoc cref="LabelHint" />
    public const string ValueHint = "value";

    /// <summary>What an empty display name says, dimly: the instance shows the username in its place.</summary>
    public const string NameHint = "none · your username is shown";

    /// <summary>What an empty bio says, dimly.</summary>
    public const string BioHint = "none · say something about yourself";

    private readonly PublicProfile _read;

    private readonly string[] _labels;

    private readonly string[] _values;

    /// <param name="read">The public profile as written, read fresh as the form was opened.</param>
    public PublicProfileScreen(PublicProfile read)
    {
        _read = read;

        // Four rows at the least, which is what Mastodon keeps, and as many as a fork's account already has where it
        // keeps more — so that a save never drops a field nobody could see to keep (#404's review).
        var rows = Math.Max(PublicProfileField.MastodonKeeps, read.Fields.Count);

        _labels = [.. Enumerable.Range(0, rows).Select(row => row < read.Fields.Count ? read.Fields[row].Label : "")];
        _values = [.. Enumerable.Range(0, rows).Select(row => row < read.Fields.Count ? read.Fields[row].Value : "")];

        DisplayName = read.DisplayName;
        Bio = read.Bio;
        IsLocked = read.IsLocked;
        IsBot = read.IsBot;
        IsDiscoverable = read.IsDiscoverable;
    }

    /// <summary>The public profile as it was read, which is what everything here is compared against.</summary>
    public PublicProfile Read => _read;

    /// <summary>The name to be shown as, as it stands in the form.</summary>
    public string DisplayName { get; private set; }

    /// <summary>The bio as written, as it stands in the form.</summary>
    public string Bio { get; private set; }

    /// <summary>
    ///     The file chosen or pasted to be the avatar, or <see langword="null" /> to leave the avatar as it is. Sent only
    ///     when the form is saved, there being nowhere to send a profile picture ahead of the save (ADR-0028).
    /// </summary>
    public string? Avatar { get; private set; }

    /// <summary>The file chosen or pasted to be the header, or <see langword="null" /> to leave it. Sent as <see cref="Avatar" /> is.</summary>
    public string? Header { get; private set; }

    /// <summary>How many custom-field rows the form has: four, or more where the account already has more.</summary>
    public int FieldRows => _labels.Length;

    /// <summary>Whether followers are to be approved by hand.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Whether the account is to say it posts automatically.</summary>
    public bool IsBot { get; private set; }

    /// <summary>Whether the account is to be suggested and listed in the directory.</summary>
    public bool IsDiscoverable { get; private set; }

    /// <summary>Where the typing is, or which toggle the walk is on: the display name, as the form opens.</summary>
    public PublicProfileStop Typing { get; private set; } = new(PublicProfilePart.DisplayName);

    /// <inheritdoc />
    /// <remarks>
    ///     ADR-0028's words, in the sentence case every crumb is in (#216): it names what is being done, as compose's
    ///     <c>Edit</c> does.
    /// </remarks>
    public override string Crumb => "Edit public profile";

    /// <inheritdoc />
    /// <remarks>
    ///     <c>space</c> first on a toggle, and <c>⏎</c> and the paste on a picture, being what the row is for; then the
    ///     walk, offered only the ways it goes, as compose's is (#337): it stops at either end rather than coming round.
    ///     The paste names both keys, since which of them reaches Wooly is the terminal's to say (#380).
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        .. Typing.IsAToggle ? [new KeyHint("space", "flip")] : Array.Empty<KeyHint>(),
        .. Typing.IsAPicture
            ? [new KeyHint("⏎", "choose file"), new KeyHint("ctrl-v/alt-v", "paste")]
            : Array.Empty<KeyHint>(),
        Stops.IndexOf(Typing) switch
        {
            0 => new KeyHint("↓", "field"),
            var at when at == Stops.Count - 1 => new KeyHint("↑", "field"),
            _ => new KeyHint("↑↓", "field"),
        },
        new KeyHint("ctrl-s", "save"),
        new KeyHint("esc", "leave"),
    ];

    /// <summary>
    ///     Every place the arrows walk to, top to bottom, which is the order they are drawn in: the display name, the
    ///     bio, the avatar and the header, each custom field's label and then its value, and the three toggles.
    /// </summary>
    private List<PublicProfileStop> Stops =>
    [
        new(PublicProfilePart.DisplayName),
        new(PublicProfilePart.Bio),
        new(PublicProfilePart.Avatar),
        new(PublicProfilePart.Header),
        .. Enumerable.Range(0, FieldRows).SelectMany(row => new PublicProfileStop[]
        {
            new(PublicProfilePart.Label, row), new(PublicProfilePart.Value, row),
        }),
        new(PublicProfilePart.Locked),
        new(PublicProfilePart.Bot),
        new(PublicProfilePart.Discoverable),
    ];

    /// <summary>
    ///     <c>↓</c> or <c>↑</c>: the typing on to the next place down or up, stopping at either end rather than coming
    ///     round (#337's rule for compose).
    /// </summary>
    /// <returns>A move, or nothing at either end.</returns>
    public ComposeChange Walk(int by)
    {
        var stops = Stops;
        var to = Math.Clamp(stops.IndexOf(Typing) + by, 0, stops.Count - 1);

        return TypeInto(stops[to]);
    }

    /// <summary>The typing into <paramref name="stop" />, by a click or the walk.</summary>
    /// <returns>A move, or nothing where the typing was there already.</returns>
    public ComposeChange TypeInto(PublicProfileStop stop)
    {
        if (Typing == stop || !Stops.Contains(stop))
        {
            return ComposeChange.None;
        }

        Typing = stop;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     What <c>ctrl-s</c> sends: whatever differs from what was read, and nothing else (ADR-0028) — so nothing left
    ///     alone here can overwrite something changed on the web while the form was open. The custom fields go as the
    ///     rows that hold anything, in order, an emptied row being left out of them, which is what removes its field. A
    ///     picture goes only where a file was chosen for it, and goes in this save (ADR-0028).
    /// </summary>
    /// <remarks>
    ///     The bio is compared line ending for line ending alike, as compose compares a post (#373), so that an editor
    ///     writing its lines back its own way is not somebody having written something.
    /// </remarks>
    public PublicProfileChange Outgoing => new PublicProfileChange
    {
        DisplayName = DisplayName,
        Bio = string.Equals(Bio.ReplaceLineEndings(), _read.Bio.ReplaceLineEndings(), StringComparison.Ordinal)
            ? _read.Bio
            : Bio,
        Fields =
        [
            .. Enumerable.Range(0, FieldRows)
                         .Where(row => !string.IsNullOrWhiteSpace(_labels[row]) || !string.IsNullOrWhiteSpace(_values[row]))
                         .Select(row => new PublicProfileField(_labels[row], _values[row])),
        ],
        IsLocked = IsLocked,
        IsBot = IsBot,
        IsDiscoverable = IsDiscoverable,
        Avatar = Avatar,
        Header = Header,
    }.Against(_read);

    /// <summary>
    ///     Whether anything here differs from what the form opened on, which is what makes leaving it worth a question
    ///     (#373's rule for compose). What it holds rather than whether a key was pressed: a letter typed and rubbed out
    ///     again, or a toggle flipped and flipped back, leaves it untouched.
    /// </summary>
    public bool Touched => !Outgoing.IsEmpty;

    /// <inheritdoc />
    public string Discarding => "Discard these changes?";

    /// <summary>The display name's field changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the field says back what the form holds, as it does when filled on opening.</returns>
    public ComposeChange RewriteDisplayName(string written) => Rewrite(DisplayName, written, () => DisplayName = written);

    /// <summary>The bio's editor changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the editor says back what the form holds.</returns>
    public ComposeChange RewriteBio(string written) => Rewrite(Bio, written, () => Bio = written);

    /// <summary>The <paramref name="row" />th custom field's label changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the field says back what the form holds.</returns>
    public ComposeChange RewriteLabel(int row, string written) =>
        Rewrite(_labels[row], written, () => _labels[row] = written);

    /// <summary>The <paramref name="row" />th custom field's value changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the field says back what the form holds.</returns>
    public ComposeChange RewriteValue(int row, string written) =>
        Rewrite(_values[row], written, () => _values[row] = written);

    /// <summary>
    ///     <c>space</c>, <c>⏎</c> or a click on the toggle the walk is on: it the other way.
    /// </summary>
    /// <returns>An edit, or nothing where the walk is on a field rather than a toggle.</returns>
    public ComposeChange Flip() => Flip(Typing.Part);

    /// <summary>The toggle <paramref name="toggle" /> names the other way, from a click on it.</summary>
    /// <returns>An edit, or nothing where <paramref name="toggle" /> is no toggle.</returns>
    public ComposeChange Flip(PublicProfilePart toggle)
    {
        switch (toggle)
        {
            case PublicProfilePart.Locked:
                IsLocked = !IsLocked;

                break;

            case PublicProfilePart.Bot:
                IsBot = !IsBot;

                break;

            case PublicProfilePart.Discoverable:
                IsDiscoverable = !IsDiscoverable;

                break;

            default:
                return ComposeChange.None;
        }

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     The file at <paramref name="path" />, chosen in the file browser or pasted, as the picture the walk is on — the
    ///     avatar or the header — to be drawn in place of the current one and sent when the form is saved (#407).
    /// </summary>
    /// <returns>An edit, or nothing where the walk is on no picture or the file is the one already chosen.</returns>
    public ComposeChange Pick(string path)
    {
        switch (Typing.Part)
        {
            case PublicProfilePart.Avatar when Avatar != path:
                Avatar = path;

                break;

            case PublicProfilePart.Header when Header != path:
                Header = path;

                break;

            default:
                return ComposeChange.None;
        }

        return ComposeChange.Edited;
    }

    private static ComposeChange Rewrite(string holding, string written, Action rewrite)
    {
        if (string.Equals(holding, written, StringComparison.Ordinal))
        {
            return ComposeChange.None;
        }

        rewrite();

        return ComposeChange.Edited;
    }

    /// <summary>The <paramref name="row" />th custom field's label, as it stands in the form.</summary>
    public string LabelOf(int row) => _labels[row];

    /// <summary>The <paramref name="row" />th custom field's value, as it stands in the form.</summary>
    public string ValueOf(int row) => _values[row];

    /// <summary>
    ///     Where the field, picture or toggle at <paramref name="stop" /> goes inside a content viewport of
    ///     <paramref name="viewport" /> on a terminal that paints as <paramref name="raster" /> says — nowhere, for a row
    ///     a short viewport has no room for.
    /// </summary>
    /// <remarks>Under the raster the rows were laid out by, which says how tall a picture's row is.</remarks>
    public Rectangle At(PublicProfileStop stop, Size viewport, Raster? raster = null) =>
        Laid(viewport.Width, viewport.Height, raster, pictures: null).Places.GetValueOrDefault(stop);

    /// <inheritdoc />
    /// <remarks>
    ///     A blank under the panel's edge, the display name, the bio's rows, a blank, the avatar's and the header's rows,
    ///     a blank, the custom-field rows, a blank and the three toggles — each field's rows holding what it holds, or its
    ///     hint, for the widget laid over them.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing) =>
        Laid(drawing.Width, drawing.Height, drawing.Raster, drawing.Pictures).Rows;

    /// <summary>
    ///     The form laid out <paramref name="width" /> wide in a room <paramref name="height" /> tall, on a terminal
    ///     that paints as <paramref name="raster" /> says with <paramref name="pictures" />' pixels: its rows, and where
    ///     each field, picture and toggle goes on them.
    /// </summary>
    /// <remarks>
    ///     Every column is worked out from the width and none from what is written, so nothing moves as it is typed:
    ///     a custom field's label takes a third of the value column and its value the rest. The bio takes what height
    ///     the room spares, between three rows and eight, or four where nobody says how tall the room is. How tall a
    ///     picture's row is depends on the terminal and on nothing chosen, so choosing one moves nothing either.
    /// </remarks>
    private (IReadOnlyList<Line> Rows, Dictionary<PublicProfileStop, Rectangle> Places) Laid(
        int width,
        int? height,
        Raster? raster,
        IPictures? pictures)
    {
        var at = Math.Min(Pad + LabelWidth + Gap, width);
        var room = Math.Max(0, width - at - Pad);
        var labelRoom = room / 3;
        var valueAt = Math.Min(at + labelRoom + Gap, width);
        var valueRoom = Math.Max(0, room - labelRoom - Gap);

        var pictureRows = raster?.Cell is not null ? PictureRows : 1;

        var toggles = new[] { PublicProfilePart.Locked, PublicProfilePart.Bot, PublicProfilePart.Discoverable };
        var others = 1 + 1 + 1 + (pictureRows * 2) + 1 + FieldRows + 1 + toggles.Length;
        var bioRows = height is { } tall ? Math.Clamp(tall - others, LeastBioRows, MostBioRows) : BioRowsUnmeasured;

        var rows = new List<Line>();
        var places = new Dictionary<PublicProfileStop, Rectangle>();

        rows.Add(Line.Blank);

        Place(new PublicProfileStop(PublicProfilePart.DisplayName), new Rectangle(at, rows.Count, room, 1));
        rows.Add(Stamp(PublicProfilePart.DisplayName, Header("Name", Written(DisplayName, NameHint, room))));

        Place(new PublicProfileStop(PublicProfilePart.Bio), new Rectangle(at, rows.Count, room, bioRows));

        Span[] bio = Bio.Length == 0
            ? [Written(Bio, BioHint, room)]
            : TextWrap.Wrap(Bio.ReplaceLineEndings("\n"), Math.Max(1, room)).Select(line => new Span(line, Role.Body))
                      .ToArray();

        for (var line = 0; line < bioRows; line++)
        {
            var said = line < bio.Length ? bio[line] : new Span("", Role.Body);

            rows.Add(Stamp(PublicProfilePart.Bio, line == 0 ? Header("Bio", said) : Under(said)));
        }

        rows.Add(Line.Blank);

        foreach (var picture in new[] { PublicProfilePart.Avatar, PublicProfilePart.Header })
        {
            Place(new PublicProfileStop(picture), new Rectangle(at, rows.Count, room, pictureRows));
            rows.AddRange(Pictured(picture, at, width, pictureRows, raster, pictures).Select(line => Stamp(picture, line)));
        }

        rows.Add(Line.Blank);

        for (var row = 0; row < FieldRows; row++)
        {
            Place(new PublicProfileStop(PublicProfilePart.Label, row), new Rectangle(at, rows.Count, labelRoom, 1));
            Place(new PublicProfileStop(PublicProfilePart.Value, row), new Rectangle(valueAt, rows.Count, valueRoom, 1));

            var label = Written(_labels[row], LabelHint, labelRoom);
            var value = Written(_values[row], ValueHint, valueRoom);
            var pair = new[] { label, Blank(labelRoom - label.Width + Gap), value };
            var onIt = Typing is { Part: PublicProfilePart.Label or PublicProfilePart.Value } typing && typing.Row == row;

            rows.Add(Stamp(onIt, row == 0 ? Header("Fields", pair) : Under(pair)));
        }

        rows.Add(Line.Blank);

        foreach (var toggle in toggles)
        {
            var (name, says, on) = toggle switch
            {
                PublicProfilePart.Locked => ("Locked", "approve followers by hand", IsLocked),
                PublicProfilePart.Bot => ("Bot", "posts automatically", IsBot),
                _ => ("Discoverable", "in the directory and suggestions", IsDiscoverable),
            };

            Place(new PublicProfileStop(toggle), new Rectangle(at, rows.Count, room, 1));

            var box = on ? "■ " : "□ ";
            var named = TextWrap.Clip(box + name, room);
            var explained = TextWrap.Clip($" · {says}", room - Glyphs.Columns(named));

            rows.Add(Stamp(toggle, Under(new Span(named, Role.Body), new Span(explained, Role.Muted))));
        }

        return (rows, places);

        void Place(PublicProfileStop stop, Rectangle place) => places[stop] = place;

        Line Header(string label, params Span[] value) =>
            Line.Of([Blank(Pad + LabelWidth - Glyphs.Columns(label)), new Span(label, Role.Muted), Blank(Gap), .. value]);

        Line Under(params Span[] value) => Line.Of([Blank(at), .. value]);
    }

    /// <summary>
    ///     The <paramref name="pictureRows" /> rows of the avatar's or the header's row, its label right-aligned as every
    ///     other's: on a terminal that draws, the picture in a box of its own at <paramref name="at" /> — the one chosen,
    ///     or else the current one, the Stand-in's shade holding the box until the pixels are here — and then the file
    ///     chosen for it, or that it is the current one or there is none. Where nothing is drawn, the words alone.
    /// </summary>
    /// <remarks>
    ///     Through the picture path everything else drawn takes (ADR-0016, ADR-0022): the current avatar is the one
    ///     <b>You</b>'s header block has already sent for, and a file chosen is the one the file browser previewed.
    /// </remarks>
    private IEnumerable<Line> Pictured(
        PublicProfilePart picture,
        int at,
        int width,
        int pictureRows,
        Raster? raster,
        IPictures? pictures)
    {
        var (label, columns, chosen, current) = picture == PublicProfilePart.Avatar
            ? ("Avatar", AvatarColumns, Avatar, _read.AvatarUrl)
            : ("Header", HeaderColumns, Header, _read.HeaderUrl);

        var cell = raster?.Cell;
        var named = cell is null ? at : Math.Min(at + HeaderColumns + Gap, width);
        var room = Math.Max(0, width - named - Pad);

        Span[] said;

        if (chosen is null)
        {
            said = [new Span(TextWrap.Clip(current is null ? NoPicture : CurrentPicture, room), Role.Muted)];
        }
        else
        {
            var name = new Span(TextWrap.Clip(Path.GetFileName(chosen), room), Role.Body);

            said = [name, new Span(TextWrap.Clip(SentOnSave, room - name.Width), Role.Muted)];
        }

        if (cell is not { } drawnAt)
        {
            yield return Labelled(label, at, said);

            yield break;
        }

        var wanted = Wanted(picture);
        var pixels = wanted is not null && pictures is not null ? pictures.Of(wanted) : null;
        var held = wanted is null ? Blank(columns)
            : pixels is null ? StandIn.Row(columns)
            : new Span(new string(' ', columns), Role.Media);
        var fitted = wanted is not null && pixels is not null
            ? new Inset(wanted, at, columns, pictureRows).Fit(pixels, drawnAt)
            : default;

        for (var row = 0; row < pictureRows; row++)
        {
            var line = row == 0
                ? Labelled(label, at, [held, Blank(named - at - columns), .. said])
                : Line.Of(Blank(at), held);

            yield return line with
            {
                Wants = row == 0 ? wanted : null,
                Insets = fitted.Inset is { } box && row == fitted.Row ? [box] : [],
            };
        }
    }

    /// <summary>
    ///     The picture <paramref name="picture" />'s row draws: the file chosen for it where it is one this client
    ///     decodes, or the instance's current one — none where a file was chosen that cannot be drawn, or there is no
    ///     current one.
    /// </summary>
    private Drawn? Wanted(PublicProfilePart picture)
    {
        var (chosen, current) = picture == PublicProfilePart.Avatar
            ? (Avatar, _read.AvatarUrl)
            : (Header, _read.HeaderUrl);

        if (chosen is not null)
        {
            return AttachmentPicture.Decodes(chosen) ? Drawn.OnDisk(chosen) : null;
        }

        if (current is null)
        {
            return null;
        }

        return picture == PublicProfilePart.Avatar
            ? Drawn.Avatar(_read.Account.Address, current)
            : Drawn.ProfileHeader(_read.Account.Address, current);
    }

    /// <summary>A row of the form: <paramref name="label" /> right-aligned before <paramref name="value" /> at <paramref name="at" />.</summary>
    private static Line Labelled(string label, int at, Span[] value) =>
        Line.Of([Blank(at - Gap - Glyphs.Columns(label)), new Span(label, Role.Muted), Blank(Gap), .. value]);

    /// <summary>
    ///     <paramref name="line" /> with the selection bar in its first column where the walk is on
    ///     <paramref name="part" /> — the mark that says where the walk is with no colour, which a toggle has no caret
    ///     to say.
    /// </summary>
    private Line Stamp(PublicProfilePart part, Line line) => Stamp(Typing.Part == part, line);

    private static Line Stamp(bool onIt, Line line) =>
        onIt && line.Spans.Count > 0 && line.Spans[0] is { Text.Length: > 0 } first
            ? line.Respanned([new Span("▌", Role.Selection), first with { Text = first.Text[1..] }, .. line.Spans.Skip(1)])
            : line;

    /// <summary>What a field holds, clipped to its room — or its hint, dimly, where it holds nothing.</summary>
    private static Span Written(string text, string hint, int room) =>
        text.Length == 0
            ? new Span(TextWrap.Clip(hint, room), Role.Muted)
            : new Span(TextWrap.Clip(text, room), Role.Body);

    private static Span Blank(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);
}
