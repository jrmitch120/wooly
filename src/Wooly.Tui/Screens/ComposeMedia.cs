using System.Globalization;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a compose screen tells its Media rows of itself when they are drawn or asked about (#387): where the typing
///     is, which lights the header or puts the bar against a picked row, and the sensitive toggle as the draft settles
///     it — the author's own setting, or held on by a warning (#379).
/// </summary>
/// <param name="Typing">Where on the compose screen the typing is.</param>
/// <param name="Sensitive">Whether what is attached goes behind a click.</param>
/// <param name="ByAWarning">Whether a warning is what holds the toggle on.</param>
internal readonly record struct MediaLook(ComposeField Typing, bool Sensitive, bool ByAWarning);

/// <summary>
///     What compose's Media holds (#387): the pending attachments, in the order the post will carry them, the row picked
///     among them and the one last taken off — and, on an edit, the attachments the post already carries, listed
///     read-only (#381). It draws the rows under the Media header and the header's value, and says what a click on a
///     row lands on, for the compose screen that lays them out and the attachments screen that lists them (story 58).
/// </summary>
/// <remarks>
///     Where the typing is stays the compose screen's, which walks it through the rows and hands it over in a
///     <see cref="MediaLook" />; so does the sensitive setting, which a warning is a draft's to hold on.
/// </remarks>
public sealed class ComposeMedia
{
    /// <summary>The label of the header the pending attachments sit under (#375).</summary>
    private const string Label = "Media";

    /// <summary>What the Media header says it holds while nothing is attached.</summary>
    private const string NothingAttached = "none";

    /// <summary>The key that adds to the Media header, which it names after what it holds while there is room.</summary>
    private const string AttachKey = "ctrl-o to add";

    /// <summary>The sensitive toggle's word, after its box (#379).</summary>
    private const string SensitiveWord = "sensitive";

    /// <summary>What the sensitive toggle adds while a warning holds it on (#379).</summary>
    private const string SensitiveByWarning = " (warning)";

    /// <summary>What an edit's Media header says of the attachments the post carries, which it cannot change.</summary>
    private const string KeptAsTheyAre = "kept as they are";

    /// <summary>The quiet mark on a pending attachment with no description (#375, #377).</summary>
    private const string NoDescription = "no alt text";

    /// <summary>What a pending attachment's row says while the instance processes it.</summary>
    private const string BeingProcessed = "processing";

    /// <summary>The grip a pending attachment's row is dragged by, muted, ahead of everything else on it.</summary>
    private const string Grip = "⠶";

    /// <summary>The bar against a picked row's grip, as a picked post's is against its edge (#378).</summary>
    private const string SelectionBar = "▌";

    /// <summary>What takes a pending attachment off the post, in plain ASCII (#375, #378).</summary>
    private const string RemoveMark = "x";

    /// <summary>What a refusal a retry could mend offers, the key named in it (#378).</summary>
    private const string RetryOffer = "retry (r)";

    /// <summary>The offer cut to its key, where the status column has not room for the reason and it both.</summary>
    private const string ShortRetryOffer = "(r)";

    /// <summary>The columns left blank either side, as everywhere on the compose screen (#317).</summary>
    private const int Pad = 2;

    /// <summary>The column a pending attachment's grip sits at, a column in from its selection bar (#375).</summary>
    private const int GripAt = Pad + 3;

    /// <summary>The column a pending attachment's picture starts at, one column of air after its grip (#382).</summary>
    private const int PictureAt = GripAt + 2;

    /// <summary>
    ///     The column a pending attachment's name starts at, one column of air after its picture — two past the header's
    ///     value column, the picture being three wide where the rows were first laid with one (#375, #382).
    /// </summary>
    private const int NameAt = PictureAt + AttachmentPicture.Columns + 1;

    /// <summary>How wide a pending attachment's name is cut to on a terminal with room for it.</summary>
    private const int MostName = 32;

    /// <summary>How narrow a name is cut to before anything else gives way.</summary>
    private const int LeastName = 12;

    /// <summary>The kind column's width, the kind and the gap after it, which goes first on a narrow terminal.</summary>
    private const int KindColumn = 9;

    /// <summary>The size column's width, right-aligned.</summary>
    private const int SizeColumn = 8;

    /// <summary>The fewest columns the status column is left with before the kind and then the name give way.</summary>
    private const int LeastStatus = 16;

    /// <summary>The most the status column is drawn across, so <c>x</c> stays near the row it takes off.</summary>
    private const int MostStatus = 40;

    /// <summary>How many cells an upload's gauge takes.</summary>
    private const int GaugeCells = 8;

    /// <summary>What is attached, in the order the post will carry it (#375).</summary>
    private readonly List<ComposeAttachment> _attachments = [];

    /// <summary>How many attachments the instance lets a post carry, as the compose screen has heard (#319).</summary>
    private readonly Func<PostLimits> _limits;

    /// <summary>The attachment last taken off and where it was, for <c>ctrl-z</c> (#378); one deep.</summary>
    private (ComposeAttachment Attachment, int At)? _removed;

    /// <param name="takes">Whether attachments can be added here: on a fresh post or a reply, never an edit.</param>
    /// <param name="kept">What the post being edited already carries, or nothing.</param>
    /// <param name="limits">How long the instance lets a post be, and how many attachments it carries.</param>
    internal ComposeMedia(bool takes, IReadOnlyList<PostMedia> kept, Func<PostLimits> limits)
    {
        Takes = takes;
        Kept = kept;
        _limits = limits;
    }

    /// <summary>
    ///     Whether this takes attachments: a fresh post and a reply do, under their Media header (#375); an edit carries
    ///     the post's own through unchanged (ADR-0008), and its header only lists them (#381).
    /// </summary>
    public bool Takes { get; }

    /// <summary>What is attached, in the order the post will carry it, each with where it has got to (#375).</summary>
    public IReadOnlyList<ComposeAttachment> Attachments => _attachments;

    /// <summary>
    ///     What the post being edited already carries, which an edit lists under its Media header read-only so that the
    ///     author can see it stays (#381) — and which the edit leaves as it was, never naming it (ADR-0008). Nothing on
    ///     a fresh post or a reply.
    /// </summary>
    private IReadOnlyList<PostMedia> Kept { get; }

    /// <summary>How many rows the Media header has under it: one for each attachment, pending or kept.</summary>
    public int RowCount => _attachments.Count + Kept.Count;

    /// <summary>How many more attachments the post has room for, by the instance's limit.</summary>
    public int Room => Takes ? Math.Max(0, _limits().Attachments - _attachments.Count) : 0;

    /// <summary>Whether anything attached is still going up or being processed, which a send waits on (ADR-0026).</summary>
    public bool Unfinished => _attachments.Any(attachment => attachment.Unfinished);

    /// <summary>How many attachments the instance refused, which a send will not go out with (ADR-0026).</summary>
    public int Refused => _attachments.Count(attachment => attachment.State is AttachmentState.Refused);

    /// <summary>
    ///     The pending attachment whose row was last picked (#378) — by the walk, a press or a click — which is the one
    ///     the row's keys act on while the typing is on a row (<see cref="ComposeField.Attachment" />).
    /// </summary>
    public ComposeAttachment? Picked { get; internal set; }

    /// <summary>
    ///     Attaches <paramref name="files" />, in order, after whatever is attached already — as many as the post has
    ///     room for (#375). Each starts out going up, which is the shell's to start and to feed back
    ///     (<see cref="Progressed" />).
    /// </summary>
    /// <returns>The ones attached, which are the ones the shell sends up.</returns>
    public IReadOnlyList<ComposeAttachment> Attach(IEnumerable<ComposeAttachment> files)
    {
        var attached = files.Take(Room).ToList();

        _attachments.AddRange(attached);

        return attached;
    }

    /// <summary>
    ///     Takes <paramref name="attachment" /> off the post (#378), remembering it and where it was, one deep, for
    ///     <see cref="BringBack" />. Where it was the one picked, the pick goes to the row that took its place, else
    ///     the one above, else nowhere.
    /// </summary>
    /// <returns>Whether it was attached.</returns>
    internal bool Remove(ComposeAttachment attachment)
    {
        var at = _attachments.IndexOf(attachment);

        if (at < 0)
        {
            return false;
        }

        _attachments.RemoveAt(at);
        _removed = (attachment, at);

        if (ReferenceEquals(Picked, attachment))
        {
            Picked = _attachments.Count > 0 ? _attachments[Math.Min(at, _attachments.Count - 1)] : null;
        }

        return true;
    }

    /// <summary>
    ///     The attachment last taken off, back where it was with its description and wherever it had got to, and picked
    ///     (#378). Nothing while the post has no room for it.
    /// </summary>
    /// <returns>Whether there was one to bring back and somewhere to put it.</returns>
    internal bool BringBack()
    {
        if (_removed is not var (attachment, at) || Room == 0)
        {
            return false;
        }

        _attachments.Insert(Math.Min(at, _attachments.Count), attachment);
        _removed = null;
        Picked = attachment;

        return true;
    }

    /// <summary>
    ///     <paramref name="attachment" /> dragged to the <paramref name="place" />th row, the rows between making way
    ///     (#378) — a place past either end is that end.
    /// </summary>
    /// <returns>An edit, or nothing where it is already there or is not attached.</returns>
    public ComposeChange ReorderTo(ComposeAttachment attachment, int place)
    {
        var from = _attachments.IndexOf(attachment);
        var to = Math.Clamp(place, 0, _attachments.Count - 1);

        if (from < 0 || from == to)
        {
            return ComposeChange.None;
        }

        _attachments.RemoveAt(from);
        _attachments.Insert(to, attachment);

        return ComposeChange.Edited;
    }

    /// <summary><paramref name="attachment" /> <paramref name="by" /> places earlier or later on the post (#378).</summary>
    /// <returns>An edit, or nothing off either end or where it is not attached.</returns>
    internal ComposeChange Move(ComposeAttachment attachment, int by) =>
        ReorderTo(attachment, _attachments.IndexOf(attachment) + by);

    /// <summary>
    ///     A click on <paramref name="attachment" />'s <c>retry (r)</c>, or <c>r</c> on its row: it starts going up again
    ///     from nothing, where a retry could mend its refusal and nowhere else (ADR-0006: the author's to ask for, never
    ///     done by itself).
    /// </summary>
    /// <returns>The attachment, for the shell to send up; or nothing.</returns>
    public ComposeAttachment? Retry(ComposeAttachment attachment)
    {
        if (!_attachments.Contains(attachment) || attachment.State is not AttachmentState.Refused { Retryable: true })
        {
            return null;
        }

        attachment.State = new AttachmentState.Sending(0);

        return attachment;
    }

    /// <summary>
    ///     Where <paramref name="attachment" /> has got to, as the shell heard it: its row says so from now on. Nothing
    ///     where it is no longer attached — kept, but unsaid, on the one <c>ctrl-z</c> could still bring back.
    /// </summary>
    /// <returns>Whether that changed anything on the screen.</returns>
    public bool Progressed(ComposeAttachment attachment, AttachmentState state)
    {
        // One taken off but still to be brought back hears on too, so that it comes back as far as it has got (#378).
        var attached = _attachments.Contains(attachment);

        if ((!attached && !ReferenceEquals(_removed?.Attachment, attachment)) || attachment.State == state)
        {
            return false;
        }

        attachment.State = state;

        return attached;
    }

    /// <summary>
    ///     What <paramref name="attachment" />'s author says it shows, as the description editor has it now (#377). Only
    ///     where it is still attached.
    /// </summary>
    /// <returns>An edit, or nothing where that is what it said already.</returns>
    public ComposeChange Describe(ComposeAttachment attachment, string description)
    {
        if (!_attachments.Contains(attachment) || attachment.Description == description)
        {
            return ComposeChange.None;
        }

        attachment.Description = description;

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     The instance took <paramref name="description" /> for <paramref name="attachment" />, as the shell heard
    ///     (#377): what it holds from now on, which a send no longer waits to tell it.
    /// </summary>
    /// <returns>Whether that changed anything.</returns>
    public bool Told(ComposeAttachment attachment, string description)
    {
        if (!_attachments.Contains(attachment) || attachment.Told == description)
        {
            return false;
        }

        attachment.Told = description;

        return true;
    }

    /// <summary>
    ///     What a row under the Media header offers while the walk is on it (#378): a retry where one could mend its
    ///     refusal, moving it and taking it off — and, on the header or a row, bringing back what was last taken off
    ///     while there is something to and room for it.
    /// </summary>
    internal KeyHint[] Keys(MediaLook look) =>
    [
        .. look.Typing == ComposeField.Attachment && Picked is { State: AttachmentState.Refused { Retryable: true } }
            ? [new KeyHint("r", "retry")]
            : Array.Empty<KeyHint>(),
        .. look.Typing == ComposeField.Attachment
            ? [new KeyHint("shift-↑↓", "move"), new KeyHint("del", "remove")]
            : Array.Empty<KeyHint>(),
        .. look.Typing is (ComposeField.Media or ComposeField.Attachment) && _removed is not null && Room > 0
            ? [new KeyHint("ctrl-z", "bring back")]
            : Array.Empty<KeyHint>(),
    ];

    /// <summary>
    ///     The Media header's line <paramref name="width" /> wide: its value, counting its rows too where they are
    ///     <paramref name="folded" /> into it (<see cref="Value" />).
    /// </summary>
    internal Line Header(int width, bool folded, MediaLook look) =>
        ComposeScreen.Header(Label, Role.Muted, Value(ComposeScreen.ValueWidth(width), folded, look));

    /// <summary>
    ///     The Media header's line <paramref name="width" /> wide, unfolded, as the attachments screen heads its list of
    ///     the rows with it (story 58): the count, the key that adds and the sensitive toggle, the toggle saying it is
    ///     one to click (<see cref="AttachmentPart.Sensitive" />).
    /// </summary>
    internal Line ListedHeader(int width, MediaLook look)
    {
        var toggle = Toggle(look);
        var value = Value(ComposeScreen.ValueWidth(width), folded: false, look);

        return ComposeScreen.Header(
            Label,
            Role.Muted,
            [.. value.Select(span => span == toggle ? span with { Item = (int)AttachmentPart.Sensitive } : span)]);
    }

    /// <summary>
    ///     A row under the Media header for each attachment, <paramref name="width" /> wide: the pending ones first, in
    ///     the order the post will carry them, then on an edit those the post carries already (#375, #381).
    /// </summary>
    internal IEnumerable<Line> Rows(int width, Drawing? drawing, MediaLook look) =>
    [
        .. _attachments.Select(attached => AttachmentRow(attached, width, drawing, look)),
        .. Kept.Select(kept => KeptRow(kept, width, drawing)),
    ];

    /// <summary>
    ///     <paramref name="attached" />'s row as it is drawn under the header, <paramref name="width" /> wide, for the
    ///     attachments screen to list (story 58): the same columns, picture and status, each run saying what a click on it
    ///     means.
    /// </summary>
    internal Line ListedRow(ComposeAttachment attached, int width, Drawing drawing, MediaLook look) =>
        AttachmentRow(attached, width, drawing, look, parted: true);

    /// <summary>
    ///     The pending attachment whose row is the <paramref name="row" />th under the header, drawn
    ///     <paramref name="width" /> wide, and what a click <paramref name="column" /> columns into it means (#378) —
    ///     nothing off the rows.
    /// </summary>
    internal (ComposeAttachment Attachment, AttachmentPart Part)? At(int row, int column, int width, MediaLook look)
    {
        if (row < 0 || row >= _attachments.Count)
        {
            return null;
        }

        var attached = _attachments[row];

        foreach (var (span, part) in AttachmentRuns(attached, width, look))
        {
            if (column < span.Width)
            {
                return (attached, part);
            }

            column -= span.Width;
        }

        return (attached, AttachmentPart.Row);
    }

    /// <summary>
    ///     Whether a click <paramref name="column" /> columns into <paramref name="header" />, the Media header's line as
    ///     it was drawn, lands on the sensitive toggle at the end of it (#379) — read off the very row that is drawn,
    ///     folded or not. Never while nothing is attached, there being no toggle.
    /// </summary>
    internal bool OnTheToggle(Line header, int column, MediaLook look)
    {
        if (_attachments.Count == 0)
        {
            return false;
        }

        var toggle = Toggle(look).Text;
        var at = 0;

        foreach (var span in header.Spans)
        {
            if (span.Text == toggle)
            {
                return column >= at && column < at + span.Width;
            }

            at += span.Width;
        }

        return false;
    }

    /// <summary>
    ///     The Media header's value in <paramref name="room" /> columns (#375), in the warning header's format and all
    ///     muted: what it holds — <c>none</c>, or how many of the instance's limit — then the key that adds to it, which
    ///     goes once the post is full. <paramref name="folded" />, it counts its rows too, on a terminal too short for
    ///     them: how many have no description and how many were refused, the refusals in the error's colour.
    ///     <para>
    ///         An edit's has no key, there being no way to add to it (#381): it says <c>none</c>, or how many the post
    ///         carries and that they are kept as they are — no limit, since nothing more can be put against it.
    ///     </para>
    /// </summary>
    private Span[] Value(int room, bool folded, MediaLook look)
    {
        var count = RowCount;
        var held = count == 0 ? NothingAttached
            : Takes ? $"{count} of {_limits().Attachments}"
            : count.ToString(CultureInfo.InvariantCulture);

        // The header takes no selection bar with the typing on it: its own words light up, as To's choice does — but
        // not while the walk is on one of the rows under it, which carries the bar instead (#377).
        var lit = look.Typing == ComposeField.Media;
        var spans = new List<Span> { new(held, lit ? Role.SelectedText : Role.Muted) };
        var undescribed = _attachments.Count(attached => !attached.Described)
                          + Kept.Count(kept => string.IsNullOrWhiteSpace(kept.Description));

        if (folded && undescribed > 0)
        {
            spans.Add(new Span($" · {undescribed} {NoDescription}", Role.Muted));
        }

        if (folded && Refused > 0)
        {
            spans.Add(new Span(" · ", Role.Muted));
            spans.Add(new Span($"{Refused} failed", Role.Error));
        }

        if (Room > 0)
        {
            spans.Add(new Span($" · {AttachKey}", Role.Muted));
        }

        if (Kept.Count > 0)
        {
            spans.Add(new Span($" · {KeptAsTheyAre}", Role.Muted));
        }

        if (_attachments.Count > 0)
        {
            spans.Add(new Span(" · ", Role.Muted));
            spans.Add(Toggle(look));
        }

        return Fitted(spans, room);
    }

    /// <summary>
    ///     The sensitive toggle at the end of the Media header's line (#379): <c>□ sensitive</c>, muted, while it is off;
    ///     <c>■ sensitive</c> in the warning's colour while it is on, since it hides what is attached the way a warning
    ///     hides the post; and <c>■ sensitive (warning)</c>, the same, while a warning holds it on.
    /// </summary>
    private static Span Toggle(MediaLook look) =>
        look.ByAWarning ? new Span($"■ {SensitiveWord}{SensitiveByWarning}", Role.ContentWarning)
        : look.Sensitive ? new Span($"■ {SensitiveWord}", Role.ContentWarning)
        : new Span($"□ {SensitiveWord}", Role.Muted);

    /// <summary>
    ///     A row for an attachment the post being edited already carries, <paramref name="width" /> columns wide (#381),
    ///     in a pending attachment's columns wherever it has something to put in them (review of #372): its small
    ///     picture, from the instance's preview of it through the feed's picture path, where
    ///     <paramref name="drawing" /> can draw one; its kind; and, in the status column, its description in quotes or
    ///     the quiet mark. Nothing a pending attachment's row has for changing it — no grip, no <c>x</c> — and no name
    ///     or size, which the instance does not hand back: their columns are blank, but for the kind, which takes the
    ///     name's where a narrow terminal has given its own column up.
    /// </summary>
    private static Line KeptRow(PostMedia kept, int width, Drawing? drawing)
    {
        var (name, kind, room) = RowColumns(width);
        var picture = AttachmentPicture.OfKept(kept, PictureAt, drawing?.Pictures, drawing?.Raster);
        var said = ComposeAttachment.KindWordOf(kept.Kind);
        var description = kept.Description?.ReplaceLineEndings(" ").Trim() ?? string.Empty;
        Span[] status = description.Length > 0
            ? [new Span($"“{TextWrap.Clip(description, Math.Max(0, room - 2))}”", Role.Body)]
            : [new Span(NoDescription, Role.Muted)];

        return Line.Of(
        [
            Gap(PictureAt),
            picture.Held,
            Gap(1),
            new Span(Glyphs.Padded(kind ? string.Empty : TextWrap.Clip(said, name), name), Role.Muted),
            Gap(2),
            .. kind ? [new Span(Glyphs.Padded(said, KindColumn), Role.Muted)] : Array.Empty<Span>(),
            Gap(SizeColumn + 1 + 3 + 1),
            .. Fitted(status, room),
        ]) with
        {
            Insets = picture.Box is { } box ? [box] : [],
            Wants = picture.Wanted,
        };
    }

    /// <summary>
    ///     A pending attachment's row, <paramref name="width" /> columns wide (#375): the selection bar where the row is
    ///     picked (#378), its grip, the column its picture goes in, its name, its kind, its size, the <c>x</c> that
    ///     takes it off, and one status column saying where it has got to. Every column but the status sits where the
    ///     terminal's width puts it, never where a name does, so that nothing on a row moves as it goes up, as more are
    ///     attached, or as the walk passes over it.
    /// </summary>
    /// <remarks>
    ///     The picture's column is three wide whatever the terminal, and holds the picture where
    ///     <paramref name="drawing" /> can draw one (#382) — so that it moves nothing when it comes, and nothing is moved
    ///     on a terminal that draws none.
    /// </remarks>
    /// <param name="attached">The attachment the row is for.</param>
    /// <param name="width">How wide the row is.</param>
    /// <param name="drawing">What pictures are here, and how this terminal paints them.</param>
    /// <param name="look">Where the typing is, which says whether the row is picked.</param>
    /// <param name="parted">
    ///     Whether each run carries what a click on it means (<see cref="Span.Item" />), for a screen whose clicks the
    ///     window reads off its rows rather than a field of compose's (<see cref="AttachmentsScreen" />).
    /// </param>
    private Line AttachmentRow(
        ComposeAttachment attached,
        int width,
        Drawing? drawing,
        MediaLook look,
        bool parted = false)
    {
        var picture = AttachmentPicture.Of(attached.Path, PictureAt, drawing?.Pictures, drawing?.Raster);

        return Line.Of(
        [
            .. AttachmentRuns(attached, width, look, picture.Held)
                .Select(run => parted ? run.Span with { Item = (int)run.Part } : run.Span),
        ]) with
        {
            Insets = picture.Box is { } box ? [box] : [],
            Wants = picture.Wanted,
        };
    }

    /// <summary>
    ///     A pending attachment's row as <see cref="AttachmentRow" /> draws it, each run with what a click on it means
    ///     (#378): the <c>x</c> takes it off, <c>retry (r)</c> sends it up again, and anywhere else is the row.
    /// </summary>
    /// <param name="attached">The attachment the row is for.</param>
    /// <param name="width">How wide the row is.</param>
    /// <param name="look">Where the typing is, which says whether the row is picked.</param>
    /// <param name="picture">
    ///     What the picture's column holds (<see cref="AttachmentPicture" />), or blanks where nobody said.
    /// </param>
    private List<(Span Span, AttachmentPart Part)> AttachmentRuns(
        ComposeAttachment attached,
        int width,
        MediaLook look,
        Span? picture = null)
    {
        var (name, kind, status) = RowColumns(width);
        var picked = look.Typing == ComposeField.Attachment && ReferenceEquals(Picked, attached);

        return
        [
            // The bar fills only the left half of its cell, so set right against the grip it reads as one space.
            .. picked
                ? [(Gap(GripAt - 1), AttachmentPart.Row), (new Span(SelectionBar, Role.Selection), AttachmentPart.Row)]
                : new[] { (Gap(GripAt), AttachmentPart.Row) },
            (new Span(Grip, Role.Muted), AttachmentPart.Row),
            (Gap(1), AttachmentPart.Row),
            (picture ?? Gap(AttachmentPicture.Columns), AttachmentPart.Row),
            (Gap(1), AttachmentPart.Row),
            // In its usual role picked or not: the bar against the grip is the whole of what says a row is picked, and a
            // name lit as well read as a second thing picked (review of #372).
            (new Span(Glyphs.Padded(TextWrap.Clip(attached.Name, name), name), Role.Body), AttachmentPart.Row),
            (Gap(2), AttachmentPart.Row),
            .. kind
                ? [(new Span(Glyphs.Padded(attached.KindWord, KindColumn), Role.Muted), AttachmentPart.Row)]
                : Array.Empty<(Span, AttachmentPart)>(),
            (new Span(attached.Size.PadLeft(SizeColumn), Role.Muted), AttachmentPart.Row),
            (Gap(1), AttachmentPart.Row),

            // The x's cell and one either side of it, so that a click a column off still takes it off.
            (Gap(1), AttachmentPart.Remove),
            (new Span(RemoveMark, Role.Destructive), AttachmentPart.Remove),
            (Gap(1), AttachmentPart.Remove),
            (Gap(1), AttachmentPart.Row),
            .. Status(attached, status),
        ];
    }

    /// <summary>
    ///     How a row's columns share <paramref name="width" />: the name 32 wide, the kind, and whatever is left for the
    ///     status up to its most. Where that leaves the status fewer than its least, the kind goes first and then the
    ///     name narrows, as far as its own least (#375).
    /// </summary>
    private static (int Name, bool Kind, int Status) RowColumns(int width)
    {
        // The grip, the picture and the gaps either side of them come to where names start.
        var room = width - Pad - NameAt;
        var name = MostName;
        var kind = true;

        int Status() => room - name - 2 - (kind ? KindColumn : 0) - SizeColumn - 2 - 1 - 2;

        if (Status() < LeastStatus)
        {
            kind = false;
        }

        if (Status() < LeastStatus)
        {
            name = Math.Max(LeastName, name - (LeastStatus - Status()));
        }

        return (name, kind, Math.Clamp(Status(), 0, MostStatus));
    }

    /// <summary>
    ///     What a row's status column says in <paramref name="room" /> columns, one thing at a time (#375): the upload's
    ///     gauge and how far it has got, that the instance is processing it, why it was refused, or — ready — its
    ///     description in quotes, or the quiet mark where it has none. No mark for ready: a row saying nothing else is
    ///     one that is done.
    /// </summary>
    /// <remarks>
    ///     A description too long for the room it has is cut inside its closing quote, so the row still reads as a
    ///     quotation that goes on; and a click on it, or on the quiet mark, opens the description editor (#377).
    /// </remarks>
    private static IEnumerable<(Span Span, AttachmentPart Part)> Status(ComposeAttachment attached, int room) =>
        attached.State switch
        {
            AttachmentState.Refused refused => Failure(refused, room),
            AttachmentState.Sending(var done) => Fitted(Gauge(done), room).Select(span => (span, AttachmentPart.Row)),
            AttachmentState.Processing =>
                Fitted([new Span(BeingProcessed, Role.Muted)], room).Select(span => (span, AttachmentPart.Row)),
            _ => Fitted(
                    [
                        attached.Described
                            ? new Span(
                                $"“{TextWrap.Clip(attached.Saying.ReplaceLineEndings(" "), Math.Max(0, room - 2))}”",
                                Role.Body)
                            : new Span(NoDescription, Role.Muted),
                    ],
                    room)
                .Select(span => (span, AttachmentPart.Description)),
        };

    /// <summary>
    ///     A refusal in <paramref name="room" /> columns (#378): why, in the error's colour and with no mark beside the
    ///     <c>x</c> to double it, then <c>retry (r)</c> where trying again could mend it — a dropped connection, never a
    ///     file the instance will not take. Where both do not fit, the offer shortens to <c>(r)</c> and the reason is
    ///     cut, so that the key is still there to be read.
    /// </summary>
    private static IEnumerable<(Span Span, AttachmentPart Part)> Failure(AttachmentState.Refused refused, int room)
    {
        if (!refused.Retryable)
        {
            return Fitted([new Span(refused.Why, Role.Error)], room).Select(span => (span, AttachmentPart.Row));
        }

        var offer = Glyphs.Columns(refused.Why) + 2 + Glyphs.Columns(RetryOffer) <= room ? RetryOffer : ShortRetryOffer;
        var why = Math.Max(0, room - 2 - Glyphs.Columns(offer));

        return
        [
            (new Span(TextWrap.Clip(refused.Why, why), Role.Error), AttachmentPart.Row),
            (Gap(2), AttachmentPart.Row),
            (new Span(offer, Role.Key), AttachmentPart.Retry),
        ];
    }

    /// <summary>An upload's gauge, <c>████░░░░  54%</c>: eight cells filled as far as it has got, then the share of it.</summary>
    private static Span[] Gauge(double done)
    {
        var share = Math.Clamp(done, 0, 1);
        var filled = (int)Math.Round(share * GaugeCells);

        return
        [
            new Span(new string('█', filled), Role.Gauge),
            new Span(new string('░', GaugeCells - filled), Role.GaugeEmpty),
            new Span(string.Create(CultureInfo.InvariantCulture, $" {(int)Math.Round(share * 100),3}%"), Role.Muted),
        ];
    }

    /// <summary>
    ///     <paramref name="spans" /> cut to <paramref name="room" /> columns, the last that does not fit ending in an
    ///     ellipsis and anything after it left off.
    /// </summary>
    private static Span[] Fitted(IEnumerable<Span> spans, int room)
    {
        var fitted = new List<Span>();

        foreach (var span in spans)
        {
            if (span.Width <= room)
            {
                fitted.Add(span);
                room -= span.Width;

                continue;
            }

            if (room > 0)
            {
                fitted.Add(span with { Text = TextWrap.Clip(span.Text, room) });
            }

            break;
        }

        return [.. fitted];
    }

    private static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);
}
