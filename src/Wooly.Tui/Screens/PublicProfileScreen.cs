using System.Drawing;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
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
///         Laid out as compose is, so the two screens that take writing read alike: the short fields as its headers
///         are — a word right-aligned in a column of its own, then the value — and the bio as its post is, at the foot
///         between two hairlines with its count under them. The fields themselves are widgets laid over the rows kept for them
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

    /// <summary>How many rows the bio's editor takes where nobody says how tall the room is.</summary>
    private const int BioRowsUnmeasured = 4;

    /// <summary>
    ///     What a picture row says where nothing has been chosen and the instance has a picture: what <c>⏎</c> on it
    ///     does, the picture beside it saying what is there.
    /// </summary>
    public const string ChangePicture = "change…";

    /// <summary>What a picture row says, before <see cref="ChoosePicture" />, where the instance has no picture.</summary>
    public const string NoPicture = "none · ";

    /// <summary>What <c>⏎</c> on a picture row with no picture does.</summary>
    public const string ChoosePicture = "choose…";

    /// <summary>What a picture row says after the name of the file chosen for it: that nothing has gone up yet.</summary>
    public const string SentOnSave = " · sent on save";

    /// <summary>What a field says, dimly, while it holds nothing — the label half and the value half.</summary>
    public const string LabelHint = "label";

    /// <inheritdoc cref="LabelHint" />
    public const string ValueHint = "value";

    /// <summary>What an empty display name says, dimly: the instance shows the username in its place.</summary>
    public const string NameHint = "none · your username is shown";

    /// <summary>What an empty bio says, dimly, where it would start — as compose's empty post does.</summary>
    public const string BioHint = "Say something about yourself";

    private readonly PublicProfile _read;

    private readonly string[] _labels;

    private readonly string[] _values;

    /// <summary>Which way each toggle is, as it stands in the form.</summary>
    private readonly Dictionary<PublicProfilePart, bool> _toggled;

    /// <summary>The file chosen or pasted for each picture, where one has been.</summary>
    private readonly Dictionary<PublicProfilePart, string> _picked = [];

    /// <summary>
    ///     How many of the form's rows are above a room too short for all of them: moved only as far as keeps the field
    ///     the walk is on in the room, so that walking within what is shown moves nothing.
    /// </summary>
    private int _scrolled;

    /// <summary>The instance the public profile is on, or none where nobody said.</summary>
    private readonly string? _on;

    /// <param name="read">The public profile as written, read fresh as the form was opened.</param>
    /// <param name="on">
    ///     The instance the public profile is on, and so whose limit on a bio it is measured against (#422) — or none,
    ///     where nobody said.
    /// </param>
    public PublicProfileScreen(PublicProfile read, string? on = null)
    {
        _read = read;
        _on = on;

        // Four rows at the least, which is what Mastodon keeps, and as many as a fork's account already has where it
        // keeps more — so that a save never drops a field nobody could see to keep (#404's review).
        var rows = Math.Max(PublicProfileField.MastodonKeeps, read.Fields.Count);

        _labels = [.. Enumerable.Range(0, rows).Select(row => row < read.Fields.Count ? read.Fields[row].Label : "")];
        _values = [.. Enumerable.Range(0, rows).Select(row => row < read.Fields.Count ? read.Fields[row].Value : "")];

        DisplayName = read.DisplayName;
        Bio = read.Bio;
        _toggled = new Dictionary<PublicProfilePart, bool>
        {
            [PublicProfilePart.Locked] = read.IsLocked,
            [PublicProfilePart.Bot] = read.IsBot,
            [PublicProfilePart.Discoverable] = read.IsDiscoverable,
        };
    }

    /// <summary>
    ///     Whether this public profile is on <paramref name="instance" />, which is whose limit on a bio it is measured
    ///     against (#422), as a compose's post is (<see cref="ComposeScreen.GoesOutOn" />). Never, where it was opened
    ///     without saying which instance it is on.
    /// </summary>
    public bool GoesOutOn(string instance) =>
        _on is { } on && string.Equals(on, instance, StringComparison.OrdinalIgnoreCase);

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
    public string? Avatar => _picked.GetValueOrDefault(PublicProfilePart.Avatar);

    /// <summary>The file chosen or pasted to be the header, or <see langword="null" /> to leave it. Sent as <see cref="Avatar" /> is.</summary>
    public string? Header => _picked.GetValueOrDefault(PublicProfilePart.Header);

    /// <summary>How many custom-field rows the form has: four, or more where the account already has more.</summary>
    public int FieldRows => _labels.Length;

    /// <summary>Whether followers are to be approved by hand.</summary>
    public bool IsLocked => _toggled[PublicProfilePart.Locked];

    /// <summary>Whether the account is to say it posts automatically.</summary>
    public bool IsBot => _toggled[PublicProfilePart.Bot];

    /// <summary>Whether the account is to be suggested and listed in the directory.</summary>
    public bool IsDiscoverable => _toggled[PublicProfilePart.Discoverable];

    /// <summary>
    ///     What the instance lets a bio be, for the count under it, as compose's post is measured (#319): Mastodon's own
    ///     until the instance has said. Only counted against — the instance has the last word on a save (ADR-0028).
    /// </summary>
    public PostLimits Limits { get; set; } = PostLimits.Default;

    /// <summary>Where the typing is, or which toggle the walk is on: the display name, as the form opens.</summary>
    public PublicProfileStop Typing { get; private set; } = new(PublicProfilePart.DisplayName);

    /// <inheritdoc />
    /// <remarks>
    ///     ADR-0028's words, in the sentence case every crumb is in (#216): it names what is being done, as compose's
    ///     <c>Edit</c> does.
    /// </remarks>
    public override string Crumb => "Edit public profile";

    /// <inheritdoc />
    /// <remarks>Its fields take it before the window sees it (#445).</remarks>
    public override bool TypesBackslash => true;

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
    ///     Every place the typing goes, in the order the form is read, which is the order <c>⏎</c> walks: the display
    ///     name, the avatar and the header, each custom field's label and then its value, the three toggles, and the bio
    ///     at the foot. The arrows walk it a column at a time (<see cref="Walk" />, <see cref="Across" />).
    /// </summary>
    private List<PublicProfileStop> Stops =>
    [
        new(PublicProfilePart.DisplayName),
        .. PublicProfilePicture.Both.Select(picture => new PublicProfileStop(picture.Part)),
        .. Enumerable.Range(0, FieldRows).SelectMany(row => new PublicProfileStop[]
        {
            new(PublicProfilePart.Label, row), new(PublicProfilePart.Value, row),
        }),
        .. PublicProfileToggle.All.Select(toggle => new PublicProfileStop(toggle.Part)),
        new(PublicProfilePart.Bio),
    ];

    /// <summary>
    ///     <c>↓</c> or <c>↑</c>: the typing on to the next place down or up, stopping at either end rather than coming
    ///     round (#337's rule for compose). A custom field's label and value sit side by side, so the walk keeps to the
    ///     column it is in down the rows — and leaves them for the pictures above or the toggles below, coming back in
    ///     on the labels.
    /// </summary>
    /// <returns>A move, or nothing at either end.</returns>
    public DraftChange Walk(int by)
    {
        if (Typing.Part is PublicProfilePart.Label or PublicProfilePart.Value
            && Typing.Row + by is var row and >= 0
            && row < FieldRows)
        {
            return TypeInto(Typing with { Row = row });
        }

        var rows = Stops.Where(stop => stop.Part != PublicProfilePart.Value).ToList();
        var from = rows.IndexOf(Typing with { Part = Typing.Part == PublicProfilePart.Value ? PublicProfilePart.Label : Typing.Part });
        var to = Math.Clamp(from + by, 0, rows.Count - 1);

        return TypeInto(rows[to]);
    }

    /// <summary>
    ///     <c>⏎</c> in a one-line field: the typing on to the next place in the order the form is read — from a custom
    ///     field's label to its value, and from the value to the next row's label.
    /// </summary>
    /// <returns>A move, or nothing at the end.</returns>
    public DraftChange WalkOn()
    {
        var stops = Stops;

        return TypeInto(stops[Math.Min(stops.IndexOf(Typing) + 1, stops.Count - 1)]);
    }

    /// <summary>
    ///     <c>→</c> off the end of a custom field's label, or <c>←</c> off the start of its value: the typing across
    ///     to the other half of the row. Nothing anywhere else, there being nothing beside it.
    /// </summary>
    /// <returns>A move, or nothing where there is nothing that way.</returns>
    public DraftChange Across(int by) => (Typing.Part, by) switch
    {
        (PublicProfilePart.Label, > 0) => TypeInto(Typing with { Part = PublicProfilePart.Value }),
        (PublicProfilePart.Value, < 0) => TypeInto(Typing with { Part = PublicProfilePart.Label }),
        _ => DraftChange.None,
    };

    /// <summary>The typing into <paramref name="stop" />, by a click or the walk.</summary>
    /// <returns>A move, or nothing where the typing was there already.</returns>
    public DraftChange TypeInto(PublicProfileStop stop)
    {
        if (Typing == stop || !Stops.Contains(stop))
        {
            return DraftChange.None;
        }

        Typing = stop;

        return DraftChange.Moved;
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
    public DraftChange RewriteDisplayName(string written) => Rewrite(DisplayName, written, () => DisplayName = written);

    /// <summary>The bio's editor changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the editor says back what the form holds.</returns>
    public DraftChange RewriteBio(string written) => Rewrite(Bio, written, () => Bio = written);

    /// <summary>The <paramref name="row" />th custom field's label changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the field says back what the form holds.</returns>
    public DraftChange RewriteLabel(int row, string written) =>
        Rewrite(_labels[row], written, () => _labels[row] = written);

    /// <summary>The <paramref name="row" />th custom field's value changed: what it holds now.</summary>
    /// <returns>An edit, or nothing where the field says back what the form holds.</returns>
    public DraftChange RewriteValue(int row, string written) =>
        Rewrite(_values[row], written, () => _values[row] = written);

    /// <summary>
    ///     <c>space</c>, <c>⏎</c> or a click on the toggle the walk is on: it the other way.
    /// </summary>
    /// <returns>An edit, or nothing where the walk is on a field rather than a toggle.</returns>
    public DraftChange Flip() => Flip(Typing.Part);

    /// <summary>The toggle <paramref name="toggle" /> names the other way, from a click on it.</summary>
    /// <returns>An edit, or nothing where <paramref name="toggle" /> is no toggle.</returns>
    public DraftChange Flip(PublicProfilePart toggle)
    {
        if (!_toggled.TryGetValue(toggle, out var on))
        {
            return DraftChange.None;
        }

        _toggled[toggle] = !on;

        return DraftChange.Edited;
    }

    /// <summary>
    ///     The file at <paramref name="path" />, chosen in the file browser or pasted, as the picture the walk is on — the
    ///     avatar or the header — to be drawn in place of the current one and sent when the form is saved (#407).
    /// </summary>
    /// <returns>An edit, or nothing where the walk is on no picture or the file is the one already chosen.</returns>
    public DraftChange Pick(string path)
    {
        if (PublicProfilePicture.Of(Typing.Part) is not { } picture || PickedFor(picture) == path)
        {
            return DraftChange.None;
        }

        _picked[picture.Part] = path;

        return DraftChange.Edited;
    }

    private static DraftChange Rewrite(string holding, string written, Action rewrite)
    {
        if (string.Equals(holding, written, StringComparison.Ordinal))
        {
            return DraftChange.None;
        }

        rewrite();

        return DraftChange.Edited;
    }

    /// <summary>The <paramref name="row" />th custom field's label, as it stands in the form.</summary>
    public string LabelOf(int row) => _labels[row];

    /// <summary>The <paramref name="row" />th custom field's value, as it stands in the form.</summary>
    public string ValueOf(int row) => _values[row];

    /// <summary>
    ///     Where the field, picture or toggle at <paramref name="stop" /> goes inside a content viewport of
    ///     <paramref name="viewport" /> on a terminal that paints as <paramref name="raster" /> says with
    ///     <paramref name="pictures" />' pixels — nowhere, for a row a short viewport has no room for.
    /// </summary>
    /// <remarks>
    ///     Under the raster and the pictures the rows were laid out by, which between them say whether a picture is
    ///     drawn and so how tall a picture's row is (<see cref="Drawing.Draws" />).
    /// </remarks>
    public Rectangle At(PublicProfileStop stop, Size viewport, Raster? raster = null, IPictures? pictures = null) =>
        Laid(viewport.Width, viewport.Height, raster, pictures).Places.GetValueOrDefault(stop);

    /// <inheritdoc />
    /// <remarks>
    ///     A blank under the panel's edge, the display name, a blank, the avatar's and the header's rows, a blank, the
    ///     custom-field rows, a blank and the three toggles; then a hairline, a blank, the bio's rows, a hairline and the
    ///     bio's count, as compose's post is laid — each field's rows holding what it holds, or its hint, for the widget
    ///     laid over them.
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
    ///     the room spares and three rows at the least, as compose's post does, or four where nobody says how tall the
    ///     room is; it is as wide as compose's post, from pad to pad. How tall a
    ///     picture's row is depends on the terminal and on nothing chosen, so choosing one moves nothing either. A room
    ///     too short for every row is given those around the field the walk is on (<see cref="InTheRoom" />).
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

        // By the one test every picture is drawn by, so that a terminal with a cell but nothing supplying pictures does
        // not hold a shade open for good waiting on pixels nothing will send for (#438).
        var draws = Drawing.Draws(pictures, raster, out _);
        var pictureRows = draws ? PublicProfilePictures.Rows : 1;
        var inner = Math.Max(0, width - (Pad * 2));

        var pictureGap = draws ? 1 : 0;
        var others = 1 + 1 + 1 + (pictureRows * PublicProfilePicture.Both.Count) + pictureGap + 1 + FieldRows + 1
            + PublicProfileToggle.All.Count + 1 + 1 + 1 + 1;
        var bioRows = height is { } tall ? Math.Max(tall - others, LeastBioRows) : BioRowsUnmeasured;

        var rows = new List<Line>();
        var places = new Dictionary<PublicProfileStop, Rectangle>();

        rows.Add(Line.Blank);

        Place(new PublicProfileStop(PublicProfilePart.DisplayName), new Rectangle(at, rows.Count, room, 1));
        rows.Add(Labelled("Name", at, [Written(DisplayName, NameHint, room)]));

        rows.Add(Line.Blank);

        foreach (var picture in PublicProfilePicture.Both)
        {
            // Drawn pictures are a row apart, so the avatar's box does not run into the header's.
            if (picture != PublicProfilePicture.Both[0] && pictureGap > 0)
            {
                rows.Add(Line.Blank);
            }

            Place(new PublicProfileStop(picture.Part), new Rectangle(at, rows.Count, room, pictureRows));
            rows.AddRange(Pictured(picture, at, width, pictureRows, draws, raster, pictures));
        }

        rows.Add(Line.Blank);

        for (var row = 0; row < FieldRows; row++)
        {
            Place(new PublicProfileStop(PublicProfilePart.Label, row), new Rectangle(at, rows.Count, labelRoom, 1));
            Place(new PublicProfileStop(PublicProfilePart.Value, row), new Rectangle(valueAt, rows.Count, valueRoom, 1));

            var label = Written(_labels[row], LabelHint, labelRoom);
            var value = Written(_values[row], ValueHint, valueRoom);
            Span[] pair = [label, Blank(labelRoom - label.Width + Gap), value];

            rows.Add(row == 0 ? Labelled("Fields", at, pair) : Under(pair));
        }

        rows.Add(Line.Blank);

        foreach (var toggle in PublicProfileToggle.All)
        {
            Place(new PublicProfileStop(toggle.Part), new Rectangle(at, rows.Count, room, 1));

            var box = _toggled[toggle.Part] ? "■ " : "□ ";
            var named = TextWrap.Clip(box + toggle.Name, room);
            var explained = TextWrap.Clip($" · {toggle.Says}", room - Glyphs.Columns(named));

            rows.Add(Under(new Span(named, Lit(toggle.Part, Role.Body)), new Span(explained, Role.Muted)));
        }

        rows.Add(ComposeHeaders.Hairline(width));
        rows.Add(Line.Blank);

        var bioAt = Math.Min(Pad, width);

        Place(new PublicProfileStop(PublicProfilePart.Bio), new Rectangle(bioAt, rows.Count, inner, bioRows));

        Span[] bio = Bio.Length == 0
            ? [Written(Bio, BioHint, inner)]
            : TextWrap.Wrap(Bio.ReplaceLineEndings("\n"), Math.Max(1, inner)).Select(line => new Span(line, Role.Body))
                      .ToArray();

        for (var line = 0; line < bioRows; line++)
        {
            rows.Add(Line.Of(Blank(bioAt), line < bio.Length ? bio[line] : new Span("", Role.Body)));
        }

        rows.Add(ComposeHeaders.Hairline(width));
        rows.Add(ComposeHeaders.Count(PostLength.Of(Bio, warning: null, Limits with { Characters = Limits.Bio }), Limits.Bio, width));

        return InTheRoom(rows, places, height);

        void Place(PublicProfileStop stop, Rectangle place) => places[stop] = place;

        Line Under(params Span[] value) => Line.Of([Blank(at), .. value]);
    }

    /// <summary>
    ///     The rows of a form taller than the room <paramref name="height" /> leaves, cut to those that fit around the
    ///     field the walk is on, and where each field goes among them — nowhere, for one moved out of the room. The form
    ///     moves only when the walk leaves what is shown, and only as far as brings it back, so that the field walked to
    ///     is always on screen and nothing moves while it already was (review of #403). Every row, where it fits.
    /// </summary>
    private (IReadOnlyList<Line> Rows, Dictionary<PublicProfileStop, Rectangle> Places) InTheRoom(
        List<Line> rows,
        Dictionary<PublicProfileStop, Rectangle> places,
        int? height)
    {
        if (height is not { } room || rows.Count <= room)
        {
            _scrolled = 0;

            return (rows, places);
        }

        var walked = places.GetValueOrDefault(Typing);
        var top = Math.Clamp(_scrolled, 0, rows.Count - room);

        if (walked.Top < top)
        {
            top = walked.Top;
        }
        else if (walked.Bottom > top + room)
        {
            top = Math.Min(walked.Top, walked.Bottom - room);
        }

        _scrolled = top;

        var shown = new Dictionary<PublicProfileStop, Rectangle>();

        foreach (var (stop, place) in places)
        {
            var kept = Rectangle.Intersect(place, new Rectangle(place.X, top, place.Width, room));

            if (kept.Height > 0)
            {
                kept.Offset(0, -top);
                shown[stop] = kept;
            }
        }

        return ([.. rows.Skip(top).Take(room)], shown);
    }

    /// <summary>
    ///     The <paramref name="pictureRows" /> rows of the avatar's or the header's row, its label right-aligned as every
    ///     other's: on a terminal that draws, the picture in a box of its own at <paramref name="at" /> — the one chosen,
    ///     or else the current one, the Stand-in's shade holding the box until the pixels are here — and then the file
    ///     chosen for it, or else what <c>⏎</c> on it does: change the picture, or choose one where there is none. Where nothing is drawn, the words alone.
    /// </summary>
    /// <remarks>
    ///     Through the picture path everything else drawn takes (ADR-0016, ADR-0022): the current avatar is the one
    ///     <b>You</b>'s header block has already sent for, and a file chosen is the one the file browser previewed.
    /// </remarks>
    private IEnumerable<Line> Pictured(
        PublicProfilePicture picture,
        int at,
        int width,
        int pictureRows,
        bool draws,
        Raster? raster,
        IPictures? pictures)
    {
        var (label, columns, chosen) = (picture.Label, picture.Columns, PickedFor(picture));

        var named = !draws ? at : Math.Min(at + PublicProfilePictures.HeaderColumns + Gap, width);
        var room = Math.Max(0, width - named - Pad);

        Span[] said;

        if (chosen is null)
        {
            // Only the action is lit where the walk is on the row: "none · " is a fact about the row, not the key.
            Span Action(string does, int left) => new(TextWrap.Clip(does, left), Lit(picture.Part, Role.Muted));

            if (Current(picture) is null)
            {
                var none = new Span(TextWrap.Clip(NoPicture, room), Role.Muted);

                said = [none, Action(ChoosePicture, room - none.Width)];
            }
            else
            {
                said = [Action(ChangePicture, room)];
            }
        }
        else
        {
            var name = new Span(TextWrap.Clip(Path.GetFileName(chosen), room), Lit(picture.Part, Role.Body));

            said = [name, new Span(TextWrap.Clip(SentOnSave, room - name.Width), Role.Muted)];
        }

        if (!draws)
        {
            yield return Labelled(label, at, said);

            yield break;
        }

        // Filled as the feed's avatar is, whatever the picture's shape, so the avatar and the header start in one
        // column and end at their boxes' edges (#438).
        var box = PictureBox.Fixed(Wanted(picture), columns, pictureRows, pictures, raster);

        for (var row = 0; row < pictureRows; row++)
        {
            var line = row == 0
                ? Labelled(label, at, [box.Cells(row), Blank(named - at - columns), .. said])
                : Line.Of(Blank(at), box.Cells(row));

            yield return line with
            {
                Wants = row == 0 ? box.Wanted : null,
                Insets = box.Insets(row, at),
            };
        }
    }

    /// <summary>
    ///     The picture <paramref name="picture" />'s row draws: the file chosen for it where it is one this client
    ///     decodes, or the instance's current one — none where a file was chosen that cannot be drawn, or there is no
    ///     current one.
    /// </summary>
    private Drawn? Wanted(PublicProfilePicture picture) =>
        PickedFor(picture) is { } chosen
            ? AttachmentPicture.Decodes(chosen) ? Drawn.OnDisk(chosen) : null
            : Current(picture);

    /// <summary>The file chosen or pasted for <paramref name="picture" />, or <see langword="null" /> where none has been.</summary>
    private string? PickedFor(PublicProfilePicture picture) => _picked.GetValueOrDefault(picture.Part);

    /// <summary>
    ///     The picture the instance has for <paramref name="picture" /> now, through the picture path everything else
    ///     drawn takes, or <see langword="null" /> where it has none.
    /// </summary>
    private Drawn? Current(PublicProfilePicture picture) => picture.Part switch
    {
        PublicProfilePart.Avatar when _read.AvatarUrl is { } avatar => Drawn.Avatar(_read.Account.Address, avatar),
        PublicProfilePart.Header when _read.HeaderUrl is { } header => Drawn.HeaderPicture(_read.Account.Address, header),
        _ => null,
    };

    /// <summary>A row of the form: <paramref name="label" /> right-aligned before <paramref name="value" /> at <paramref name="at" />.</summary>
    private static Line Labelled(string label, int at, Span[] value) =>
        Line.Of([Blank(at - Gap - Glyphs.Columns(label)), new Span(label, Role.Muted), Blank(Gap), .. value]);

    /// <summary>
    ///     <paramref name="role" />, or lit where the walk is on <paramref name="part" />: how a picture or a toggle —
    ///     which have no caret to say the walk is on them — says so, as compose's Media and To light their value.
    /// </summary>
    private Role Lit(PublicProfilePart part, Role role) => Typing.Part == part ? Role.SelectedText : role;

    /// <summary>What a field holds, clipped to its room — or its hint, dimly, where it holds nothing.</summary>
    private static Span Written(string text, string hint, int room) =>
        text.Length == 0
            ? new Span(TextWrap.Clip(hint, room), Role.Muted)
            : new Span(TextWrap.Clip(text, room), Role.Body);

    private static Span Blank(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);
}
