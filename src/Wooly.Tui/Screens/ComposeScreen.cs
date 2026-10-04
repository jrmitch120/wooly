using System.Drawing;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>What a compose screen was opened to do, which is the only thing that differs between the three.</summary>
public enum ComposeFor
{
    /// <summary>A post answering nothing.</summary>
    Post,

    /// <summary>A reply to the post the screen was opened from.</summary>
    Reply,

    /// <summary>A change to one of the profile's own posts.</summary>
    Edit,
}

/// <summary>
///     Where on a compose screen the typing is going: one of the headers that takes typing, or the post itself
///     (ADR-0024, #337).
/// </summary>
public enum ComposeField
{
    /// <summary>Who the post goes to: its visibility, chosen on a row of radio buttons (#338).</summary>
    To,

    /// <summary>The content warning over the post (#123, #320).</summary>
    Warning,

    /// <summary>The post being written.</summary>
    Post,
}

/// <summary>Who a compose screen's post goes out as, for its From header (#317).</summary>
/// <param name="Handle">The profile's account, as a byline's handle: <c>@jeff</c>.</param>
/// <param name="Instance">The instance it is on, which tells two profiles with the same username apart.</param>
public sealed record ComposeFrom(string Handle, string Instance)
{
    /// <summary>
    ///     Who <paramref name="profile" /> posts as: its account's username, or the profile's own name where the
    ///     account has not been verified, on the instance the profile signs in to.
    /// </summary>
    public static ComposeFrom Of(ActiveProfile profile) => new(profile.Handle ?? profile.Name, profile.Instance);
}

/// <summary>
///     Writing a post: a new one, a reply, or a change to one already published. A screen pushed onto the stack like
///     any other, which is what <c>docs/tui-shell.md</c> left open and ADR-0015 settles.
/// </summary>
/// <remarks>
///     The text itself lives here rather than in the editor widget, so that what is being written is a fact about the
///     shell — something a test can set and read — rather than something only a terminal knows.
/// </remarks>
public sealed class ComposeScreen : Screen
{
    /// <summary>What To says while it is the account's own default on the instance, which this screen cannot see.</summary>
    private const string AccountDefault = "account default";

    /// <summary>The arrows either side of To's one value where the whole row does not fit (#338).</summary>
    private const string StepBack = "◂";

    private const string StepOn = "▸";

    /// <summary>What a click on either arrow stands for, as the spans carry it: never a visibility.</summary>
    private const int StepBackItem = -1;

    private const int StepOnItem = -2;

    /// <summary>What the warning row says while it is empty and nobody is writing in it.</summary>
    private const string NoWarningWritten = "none · ctrl-w to add";

    /// <summary>What the warning row says while it is empty and is being written in.</summary>
    private const string WarningBeingWritten = "say what it's about";

    /// <summary>The columns left blank either side of everything on the screen (#317).</summary>
    private const int Pad = 2;

    /// <summary>How wide the headers' right-aligned label column is: wide enough for <c>From</c>.</summary>
    private const int LabelWidth = 4;

    /// <summary>The columns between a header's label and its value.</summary>
    private const int LabelGap = 2;

    /// <summary>What an empty editor says, dimly, where the first letter will go (#316).</summary>
    public const string EmptyPostHint = "What's on your mind?";

    /// <summary>
    ///     The fewest rows the editor is ever left with, however much of what is being answered wants to sit above it
    ///     (<see cref="EditorAt" />). Three: a line being written, and one either side of it to see.
    /// </summary>
    private const int LeastEditorRows = 3;

    private readonly bool _aboutIsMine;

    private readonly ComposeFrom? _from;

    /// <summary>What To opened on, which a draft sends as not chosen for as long as To still shows it (#338).</summary>
    private readonly PostVisibility? _startingVisibility;

    /// <param name="purpose">What this screen was opened to do.</param>
    /// <param name="about">The post being replied to or edited.</param>
    /// <param name="addressing">
    ///     Who this is being written to, as the mentions that reach them, or <see langword="null" /> for a post that
    ///     addresses nobody — with a space after it, because their own words go after the recipient rather than into
    ///     their name. Two things ask for one: a reply, which a direct post is delivered by (ADR-0013) and any other is
    ///     notified by (#130) — either way the account being answered is reached by being named, so the mention is put
    ///     where the reader can see and edit it rather than added silently on the way out; and a fresh post opened on a
    ///     picked mention, which is a reader saying who they mean to write to before they have written anything (#85).
    /// </param>
    /// <param name="aboutIsMine">
    ///     Whether <paramref name="about" /> is the profile's own post — settled by <c>Shell.IsMine</c> where this
    ///     screen is pushed, so the reply label below costs no lookup of its own.
    /// </param>
    /// <param name="from">
    ///     Who the post goes out as, for the From header — or <see langword="null" /> for a screen built with no
    ///     profile behind it, whose header is the label alone.
    /// </param>
    /// <param name="visibility">
    ///     What To starts on, which is what would go out if nobody touched it — worked out by the shell, where the
    ///     preferences and the post being answered are both in reach — or <see langword="null" /> where that is not
    ///     known, and To reads "account default" and sends nothing (ADR-0024, #338). An edit shows the post's own
    ///     whatever this says, since Mastodon cannot change it.
    /// </param>
    public ComposeScreen(
        ComposeFor purpose,
        Post? about = null,
        string? addressing = null,
        bool aboutIsMine = false,
        ComposeFrom? from = null,
        PostVisibility? visibility = null)
    {
        Purpose = purpose;
        About = about;
        _aboutIsMine = aboutIsMine;
        _from = from;

        Visibility = purpose == ComposeFor.Edit && about is not null ? about.Visibility : visibility;
        _startingVisibility = Visibility;

        Opening = purpose switch
        {
            ComposeFor.Edit => about?.Content ?? string.Empty,
            _ => string.IsNullOrEmpty(addressing) ? string.Empty : $"{addressing} ",
        };

        Text = Opening;

        // Pre-filled from whatever post this one is about: what a reply answers, since a reply to a warned post is
        // usually about the warned thing and an author who has to remember to re-type the warning is one who sometimes
        // will not (#123); and what an edit is changing, which is the same warning coming back to the author who wrote
        // it (#140). A post answering nothing has no post to have been filled from and opens on an empty field rather
        // than no field at all (#139). Only the words the author wrote carry across: the instance's sensitive flag is
        // a mark over the attachments and says nothing a field could hold.
        Warning = about?.ContentWarning ?? string.Empty;
    }

    /// <summary>What this screen was opened to do.</summary>
    public ComposeFor Purpose { get; }

    /// <summary>The post being replied to or edited, or <see langword="null" /> for one answering nothing.</summary>
    public Post? About { get; }

    /// <summary>
    ///     What was in the editor before anybody typed: the post itself for an edit, the mention for a reply this
    ///     client had to address, and nothing for anything else.
    /// </summary>
    public string Opening { get; }

    /// <summary>
    ///     How long the instance lets the post be, which the count is out of: Mastodon's own until the instance has
    ///     said otherwise, which the shell asks it once a session and hands on as soon as it hears (#319).
    /// </summary>
    public PostLimits Limits { get; set; } = PostLimits.Default;

    /// <summary>What has been written so far, kept in step with the editor on every edit.</summary>
    public string Text { get; set; }

    /// <summary>
    ///     What the field holds, letter for letter — pre-filled on a reply from the post being answered and on an edit
    ///     from the post being changed, and from there the author's to keep, edit or clear. It is their post.
    /// </summary>
    /// <remarks>
    ///     The field's text, learned through <see cref="RewriteWarning" /> as the field changes, and nothing else's to
    ///     set: what it <em>means</em> on the way out is <see cref="Outgoing" />'s, which is the same field read two ways
    ///     and the reason nobody outside gets to choose between them (#146).
    /// </remarks>
    public string Warning { get; private set; }

    /// <summary>
    ///     Who the post goes to, as To shows it (ADR-0024, #338): what goes out, or <see langword="null" /> while it is
    ///     the account's own default on the instance, which this screen cannot see and so sends nothing for.
    /// </summary>
    public PostVisibility? Visibility { get; private set; }

    /// <summary>
    ///     The fields the typing walks, in the order the arrows walk them (ADR-0024, #337): the headers that take typing,
    ///     top to bottom as drawn, then the post under them. A header that comes to take typing is put here and joins
    ///     the walk, with no key rule of its own.
    /// </summary>
    private static readonly ComposeField[] Fields = [ComposeField.To, ComposeField.Warning, ComposeField.Post];

    /// <summary>
    ///     Which field what is typed is going into. Every field is on screen at once and the typing is in one of them,
    ///     since a terminal takes the keys of whichever field has them: the arrows walk it (<see cref="Walk" />),
    ///     <c>ctrl-w</c> jumps it into the warning and back, and a click into a field moves it there too, which keeps
    ///     this in step (#320).
    /// </summary>
    public ComposeField Typing { get; private set; } = ComposeField.Post;

    /// <summary>Whether what is typed is going into the warning rather than into the post.</summary>
    public bool WritingTheWarning => Typing == ComposeField.Warning;

    /// <summary>Whether the typing is in one of the headers rather than in the post.</summary>
    public bool OnAHeader => Typing != ComposeField.Post;

    /// <summary>
    ///     What the warning field says, dimly, while it is empty: how to reach it while the typing is in the post, and
    ///     what goes there while it is in the field.
    /// </summary>
    public string WarningHint => WritingTheWarning ? WarningBeingWritten : NoWarningWritten;

    /// <inheritdoc />
    public override bool HoldsADraft => true;

    /// <inheritdoc />
    /// <remarks>
    ///     Whoever wrote the post this one is about and everyone it names, so that the person being answered is the
    ///     most recently seen — and one keystroke away — once the screen is pushed.
    /// </remarks>
    public override IEnumerable<Mentionable> Seen => About is { } about ? Mentionable.In(about) : [];

    /// <summary>
    ///     What goes out when <c>ctrl-s</c> is pressed, whole: the post this screen publishes, or the change it saves
    ///     to one already published. Said here because this is where the fields are — what is left to the shell is the
    ///     port, the stack and the notice, which are the three things a screen has none of (#146).
    /// </summary>
    /// <remarks>
    ///     Asked for at the moment of sending rather than kept in step with the fields, since the key that asks is the
    ///     key that ends the screen.
    /// </remarks>
    public Outgoing Outgoing => Purpose switch
    {
        // About is the post e was pressed on, which the shell refuses to open an edit without.
        ComposeFor.Edit => new Outgoing.Saving(About!.Id, Changed()),
        _ => new Outgoing.Publishing(Drafted()),
    };

    /// <summary>
    ///     Whether there is anything here worth sending — which for a reply this client addressed means anything
    ///     beyond the mention it opened with, since a message that is nothing but its recipient's name says nothing.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Text)
        || (Purpose == ComposeFor.Reply && string.Equals(Text.Trim(), Opening.Trim(), StringComparison.Ordinal));

    /// <inheritdoc />
    public override string Crumb => Purpose switch
    {
        ComposeFor.Post => "Compose",
        ComposeFor.Reply => $"Reply to @{About?.Account}",
        ComposeFor.Edit => "Edit",
        _ => "Compose",
    };

    /// <inheritdoc />
    /// <remarks>
    ///     No keymap key, in either field: <c>?</c> is a letter in the post and in the warning alike, each field taking
    ///     its own keys before the shell sees them (#320) — so naming it would be offering a press that types a
    ///     question mark.
    ///     <para>
    ///         On a header the walk between the fields comes first (<c>docs/tui-shell.md</c>, #337): there <c>↑</c> and
    ///         <c>↓</c> are always the walk's, where in the post they are the caret's but on its first line. On To the
    ///         choosing comes ahead of that, being what the row is for (#338).
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        .. Typing == ComposeField.To ? [new KeyHint("←→", "choose")] : Array.Empty<KeyHint>(),
        .. OnAHeader ? [new KeyHint("↑↓", "field")] : Array.Empty<KeyHint>(),
        new("ctrl-s", Purpose == ComposeFor.Edit ? "save" : "send"),
        new("ctrl-w", WritingTheWarning ? "back to the post" : "content warning"),
        new("esc", "throw it away"),
    ];

    /// <inheritdoc />
    /// <remarks>
    ///     The whole of the content region, laid out to its height (<see cref="Drawing.Height" />) rather than
    ///     scrolled: the headers, the hairline, blank rows where the editor is laid over them, and the foot.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing) => Laid(drawing.Width, drawing.Height).Rows;

    /// <summary>
    ///     The warning field changed: what it holds now, learned the way the post's text is learned from the editor
    ///     (#320). The field is the author's to type into; what goes out is still this screen's to say.
    /// </summary>
    public void RewriteWarning(string written) => Warning = written;

    /// <summary><c>ctrl-w</c>: hands the typing to the warning, or hands it back to the post.</summary>
    public void WriteTheWarning() => Typing = WritingTheWarning ? ComposeField.Post : ComposeField.Warning;

    /// <summary>
    ///     <c>↑</c> or <c>↓</c> (<paramref name="by" /> −1 or 1): moves the typing to the field above or below in
    ///     <see cref="Fields" /> — out of the post into the last header, between the headers, and off the last header
    ///     back into the post (ADR-0024, #337).
    /// </summary>
    /// <returns>
    ///     Whether it moved, which it does not off either end: there is nothing above the top header, and below the post
    ///     is the post's own business.
    /// </returns>
    /// <remarks>
    ///     A field that takes nothing is not walked into: To on an edit, which Mastodon cannot change.
    /// </remarks>
    public bool Walk(int by)
    {
        var walked = Fields.Where(Takes).ToArray();
        var to = Array.IndexOf(walked, Typing) + by;

        if (to < 0 || to >= walked.Length)
        {
            return false;
        }

        Typing = walked[to];

        return true;
    }

    /// <summary>Whether <paramref name="field" /> can have the typing at all, which To cannot where it allows nothing.</summary>
    public bool Takes(ComposeField field) =>
        field != ComposeField.To || Enum.GetValues<PostVisibility>().Any(Allows);

    /// <summary>
    ///     The typing went into <paramref name="field" /> by a click rather than by this screen's keys, and the screen
    ///     follows it there (#320, #338).
    /// </summary>
    /// <returns>Whether that moved it, which it does not where it was already there or the field takes nothing.</returns>
    public bool TypeInto(ComposeField field)
    {
        if (Typing == field || !Takes(field))
        {
            return false;
        }

        Typing = field;

        return true;
    }

    /// <summary>
    ///     <c>←</c> or <c>→</c> (<paramref name="by" /> −1 or 1) on To: the next visibility that way which To allows,
    ///     skipping any it does not (ADR-0024, #338). From "account default", the first that way from the row's end.
    /// </summary>
    /// <returns>Whether the choice moved: never off To, nor off either end of what it allows.</returns>
    public bool Choose(int by)
    {
        if (Typing != ComposeField.To || Stepped(by) is not { } next)
        {
            return false;
        }

        Visibility = next;

        return true;
    }

    /// <summary>
    ///     A click <paramref name="column" /> columns into To's value as it is drawn <paramref name="room" /> wide: on a
    ///     value To allows it chooses it, on an arrow it steps, and anywhere on the row it gives To the typing — but a
    ///     value To does not allow ignores it, and so does all of To on an edit (ADR-0024, #338).
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public bool ClickTo(int column, int room)
    {
        if (!Takes(ComposeField.To))
        {
            return false;
        }

        var was = (Visibility, Typing);
        var item = ItemAt(ToValue(room), column);

        switch (item)
        {
            case >= 0 when !Allows((PostVisibility)item):
                return false;
            case >= 0:
                Visibility = (PostVisibility)item;

                break;
            case StepBackItem or StepOnItem:
                Visibility = Stepped(item == StepOnItem ? 1 : -1) ?? Visibility;

                break;
        }

        Typing = ComposeField.To;

        return (Visibility, Typing) != was;
    }

    /// <summary>What the span <paramref name="column" /> columns into <paramref name="spans" /> stands for, if anything.</summary>
    private static int? ItemAt(IEnumerable<Span> spans, int column)
    {
        foreach (var span in spans)
        {
            if (column < span.Width)
            {
                return span.Item;
            }

            column -= span.Width;
        }

        return null;
    }

    /// <summary>The next visibility To allows <paramref name="by" /> along the row from the one chosen, or none.</summary>
    private PostVisibility? Stepped(int by)
    {
        var all = Enum.GetValues<PostVisibility>();
        var from = Visibility is { } chosen ? Array.IndexOf(all, chosen) : by > 0 ? -1 : all.Length;

        for (var at = from + by; at >= 0 && at < all.Length; at += by)
        {
            if (Allows(all[at]))
            {
                return all[at];
            }
        }

        return null;
    }

    /// <summary>
    ///     Where the editor goes inside the content panel's viewport of <paramref name="viewport" />: under the
    ///     headers and the hairline below them, inside the padding either side, and down to the hairline at the foot.
    ///     Said here rather than worked out by the window from the rows above, because this is what paints those rows
    ///     (#315) — and both come from the one layout, so they cannot come to disagree.
    /// </summary>
    public Rectangle EditorAt(Size viewport) => Laid(viewport.Width, viewport.Height).Editor;

    /// <summary>
    ///     Where the warning field goes inside the same viewport: the warning header's value column, on whichever row
    ///     the headers above it leave it (#320). From the one layout that paints the header, as for the editor.
    /// </summary>
    public Rectangle WarningAt(Size viewport) => Laid(viewport.Width, viewport.Height).Warning;

    /// <summary>Where To's value goes inside the same viewport, for the row of buttons laid over it (#338).</summary>
    public Rectangle ToAt(Size viewport) => Laid(viewport.Width, viewport.Height).To;

    /// <summary>
    ///     To's value as it is drawn <paramref name="room" /> columns wide — what the row laid over it paints, so that
    ///     row and the one a test reads off <see cref="Lines" /> are the same spans.
    /// </summary>
    public IReadOnlyList<Span> ToSpans(int room) => ToValue(room);

    /// <summary>
    ///     The screen laid out at <paramref name="width" /> by <paramref name="height" />: every row, top to bottom,
    ///     and where the editor sits among them (#317, variant A of the prototype).
    /// </summary>
    /// <remarks>
    ///     A blank; the headers — From, To, the reply header and its quote on a reply, the warning; a hairline; a blank;
    ///     the editor; a hairline; the row the count sits on. Two columns of padding either side of all of it.
    ///     <para>
    ///         Never so far down that there is no editor left. ADR-0015 priced the editor's share of a 24-row
    ///         terminal at more than what sits above it, but a terminal can be any size, and an editor that starts
    ///         below the foot is one nobody can type in. Where there is not room for everything, rows give way in the
    ///         order <see cref="Row.Keep" /> ranks them: the tail of what is being answered first, To and the warning
    ///         never — the warning is a row the reader types into, and one they cannot see is worse than a quote that
    ///         stops early, and To says who the post reaches (#338).
    ///         Where nobody says how tall the room is, it is as tall as the rows and the editor's least want.
    ///     </para>
    /// </remarks>
    private (IReadOnlyList<Line> Rows, Rectangle Editor, Rectangle Warning, Rectangle To) Laid(int width, int? height)
    {
        var inner = Math.Max(0, width - (Pad * 2));
        var valueWidth = Math.Max(0, inner - LabelWidth - LabelGap);
        var hairline = Line.Of(Gap(Pad), new Span(new string('─', inner), Role.PanelBorder));

        var to = new Row(Header("To", Role.Muted, ToValue(valueWidth)), Keep.Always);

        var above = new List<Row>
        {
            new(Line.Blank, Keep.TopBlank),
            new(Header("From", Role.Muted, From(valueWidth)), Keep.From),
            to,
        };

        if (Purpose == ComposeFor.Reply && About is { } answered)
        {
            var said = PostReplyName.Answered(answered.Account, _aboutIsMine);

            above.Add(new Row(
                Header(PostReplyName.Mark, Role.Muted, new Span(TextWrap.Clip(said, valueWidth), Role.Muted)),
                Keep.ReplyHeader));

            // Three rows of what was said, and blank ones are not among them (#141): a post's paragraphs arrive as
            // blank lines, so a quote that took its three rows in order spent one of them on a gap.
            const string gutter = "│ ";
            var quoted = TextWrap.Wrap(answered.Content, Math.Max(1, valueWidth - Glyphs.Columns(gutter)))
                                 .Where(row => row.Length > 0)
                                 .Take(3);

            above.AddRange(quoted.Select(row => new Row(
                Line.Of(
                    Gap(Pad + LabelWidth + LabelGap),
                    new Span(gutter, Role.PanelBorder),
                    new Span(row, Role.Muted)),
                Keep.Quote)));
        }

        var warning = new Row(WarningHeader(valueWidth), Keep.Always);

        above.Add(warning);
        above.Add(new Row(hairline, Keep.HeaderHairline));
        above.Add(new Row(Line.Blank, Keep.BlankUnderHairline));

        var foot = new List<Row> { new(hairline, Keep.FootHairline), new(Count(width), Keep.CountRow) };
        var room = height ?? (above.Count + LeastEditorRows + foot.Count);

        // Whatever ranks lowest goes first and, among equals, whichever is lowest on the screen: the quote gives up
        // its tail rather than its start.
        while (above.Count + foot.Count + LeastEditorRows > room
               && above.Concat(foot).Where(row => row.Keep != Keep.Always).MinBy(row => row.Keep) is { } least)
        {
            var holding = foot.Contains(least) ? foot : above;

            holding.RemoveAt(holding.FindLastIndex(row => row.Keep == least.Keep));
        }

        var top = above.Count;
        var editor = Math.Max(0, room - top - foot.Count);

        IReadOnlyList<Line> rows =
        [
            .. above.Select(row => row.Line),
            .. Enumerable.Repeat(Line.Blank, editor),
            .. foot.Select(row => row.Line),
        ];

        return (
            rows,
            new Rectangle(Math.Min(Pad, width), top, inner, editor),
            new Rectangle(
                Math.Min(Pad + LabelWidth + LabelGap, width),
                above.IndexOf(warning),
                valueWidth,
                1),
            new Rectangle(
                Math.Min(Pad + LabelWidth + LabelGap, width),
                above.IndexOf(to),
                valueWidth,
                1));
    }

    /// <summary>
    ///     A header: <paramref name="label" /> right-aligned in the label column, in <paramref name="role" />, then
    ///     <paramref name="value" />.
    /// </summary>
    private static Line Header(string label, Role role, params Span[] value) =>
        Line.Of([Gap(Pad + LabelWidth - Glyphs.Columns(label)), new Span(label, role), Gap(LabelGap), .. value]);

    /// <summary>
    ///     The From header's value in <paramref name="room" /> columns: the handle as a byline's, then the instance,
    ///     muted — the instance cut first, since the handle is the half that says whose post this is.
    /// </summary>
    private Span[] From(int room)
    {
        if (_from is not { } from)
        {
            return [];
        }

        var handle = TextWrap.Clip(from.Handle, room);
        var instance = TextWrap.Clip($" · {from.Instance}", room - Glyphs.Columns(handle));

        return [new Span(handle, Role.BylineHandle), new Span(instance, Role.Muted)];
    }

    /// <summary>
    ///     To's value in <paramref name="room" /> columns (ADR-0024, #338): every visibility as a radio button, the
    ///     chosen one filled — or, where that row does not fit, the one chosen with an arrow either side. Values To does
    ///     not allow are muted, and so is all of it on an edit.
    /// </summary>
    /// <remarks>
    ///     Each value's span carries the visibility it stands for as its <see cref="Span.Item" />, and each arrow the
    ///     way it steps, so that a click is answered from the very spans that were drawn.
    /// </remarks>
    private Span[] ToValue(int room)
    {
        var row = Enum.GetValues<PostVisibility>()
                      .SelectMany((visibility, at) => (Span[])
                      [
                          .. at > 0 ? [Gap(2)] : Array.Empty<Span>(),
                          new Span($"{Bubble(visibility)} {PostVisibilityName.Of(visibility)}", ValueRole(visibility))
                          {
                              Item = (int)visibility,
                          },
                      ])
                      .ToArray();

        if (Visibility is not null && row.Sum(span => span.Width) <= room)
        {
            return row;
        }

        var shown = Visibility is { } chosen
            ? new Span($"{Bubble(chosen)} {PostVisibilityName.Of(chosen)}", ValueRole(chosen)) { Item = (int)chosen }
            : new Span(AccountDefault, Purpose == ComposeFor.Edit ? Role.Muted : Role.Body);

        Span[] cycle =
        [
            new(StepBack, Role.Muted) { Item = StepBackItem },
            Gap(1),
            shown,
            Gap(1),
            new(StepOn, Role.Muted) { Item = StepOnItem },
        ];

        return cycle.Sum(span => span.Width) <= room ? cycle : [shown with { Text = TextWrap.Clip(shown.Text, room) }];
    }

    private string Bubble(PostVisibility visibility) => visibility == Visibility ? "●" : "○";

    /// <summary>
    ///     The role a value of To is drawn in: muted where it cannot be chosen, the selection's where it is chosen and
    ///     To has the typing — which is how a reader sees where the typing went, a row of buttons having no caret — and
    ///     the body's otherwise.
    /// </summary>
    private Role ValueRole(PostVisibility visibility) =>
        !Allows(visibility) ? Role.Muted
        : visibility == Visibility && Typing == ComposeField.To ? Role.SelectedText
        : Role.Body;

    /// <summary>
    ///     Whether To may be set to <paramref name="visibility" />: any on a fresh post, none on an edit — Mastodon cannot
    ///     change a published post's visibility — and on a reply only as narrow as the post being answered or narrower,
    ///     so the screen never offers what ADR-0013 would refuse. The keys, the clicks and the dimming all read this.
    /// </summary>
    public bool Allows(PostVisibility visibility) => Purpose switch
    {
        ComposeFor.Edit => false,
        ComposeFor.Reply when About is { } answered => !PostAudience.IsWiderThan(visibility, answered.Visibility),
        _ => true,
    };

    /// <summary>
    ///     The count at the foot, right-aligned inside the padding: how much of the post's limit has been used, as the
    ///     instance will count it (#319) — muted while there is room, in the quota's low colour within the last tenth,
    ///     and an error's past the limit, so that a reader notices before the instance refuses the post.
    /// </summary>
    private Line Count(int width)
    {
        var used = PostLength.Of(Text, Warning, Limits);
        var count = $"{used} / {Limits.Characters}";

        // The most the count can reach and stay muted: past it is the last tenth of the limit.
        var nearlyFull = Limits.Characters * 9 / 10;

        var role = used > Limits.Characters ? Role.Error
            : used > nearlyFull ? Role.QuotaLow
            : Role.Muted;

        return Line.Of(Gap(width - Pad - Glyphs.Columns(count)), new Span(count, role));
    }

    private static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);

    /// <summary>
    ///     The post this screen publishes: what was written, whatever warning is over it, and the post it answers
    ///     where it answers one.
    /// </summary>
    /// <remarks>
    ///     A cleared field sends no warning at all rather than putting the post behind a blank, an instance reading an
    ///     empty warning as none (<see cref="ContentWarnings.Written" />, <see cref="PostDraft.ContentWarning" />).
    ///     That is the one thing this reads differently from <see cref="Changed" />, and the whole reason both are
    ///     assembled here: they are one field meaning two things, and the screen holding the field is the only place
    ///     that can be expected to know which is which.
    ///     <para>
    ///         The visibility To shows, chosen only where the author moved it off what it opened on (#338): a
    ///         preference To started on is still a preference, and <c>PostAuthor</c> narrows one too wide for the post
    ///         being answered exactly as it does on the CLI, where a choice that wide is refused. Unknown sends nothing,
    ///         which leaves it to the account's own default — or, on a reply, to the post being answered.
    ///     </para>
    /// </remarks>
    private PostDraft Drafted() => new()
    {
        Text = Text,
        ContentWarning = ContentWarnings.Written(Warning),
        InReplyTo = Purpose == ComposeFor.Reply ? About?.Id : null,
        Visibility = Visibility,
        VisibilityChosen = Visibility != _startingVisibility,
    };

    /// <summary>The change this screen saves to the post it was opened on: its text, and the warning over it.</summary>
    /// <remarks>
    ///     Always said, never left silent: the field opened on the post's own warning, so whatever it holds now is what
    ///     the author wants (#140). The field as it stands rather than as <see cref="Drafted" /> reads it, since an
    ///     empty one here means "take the warning away" rather than "say nothing about it" — the third state
    ///     <see cref="PostEdit.ContentWarning" /> keeps for the CLI, where <c>--cw</c> can be absent from the command
    ///     line. Whitespace amounts to no warning here too, which
    ///     <see cref="PostEdit.ContentWarningWanted" /> reads off the same rule rather than a second one.
    ///     <para>
    ///         What this re-sends is the warning as the timeline last read it, which may be stale — the same exposure
    ///         the body already carries, the editor being pre-filled from the same post.
    ///     </para>
    /// </remarks>
    private PostEdit Changed() => new() { Text = Text, ContentWarning = Warning };

    /// <summary>
    ///     The warning header: what this post is going behind, under the mark a warned post wears in the feed
    ///     (<see cref="PostLines.WarningMark" />) and in the role its warning is drawn in, so that a warning being
    ///     written looks like the warning it will become. The mark is lit while there is a warning or one is being
    ///     written, and dim while there is none — so a reader sees at a glance whether the post is going out behind one.
    /// </summary>
    /// <remarks>
    ///     Empty, it says so rather than going blank: a row a reader can type into is a row they have to be able to
    ///     find, and while it is being written it hints at what goes there instead. The value is painted as the field
    ///     laid over it shows it (#320), so the row reads the same with no terminal behind it; the caret is the
    ///     field's, and the terminal's own.
    /// </remarks>
    private Line WarningHeader(int room)
    {
        var mark = PostLines.WarningMark.Trim();
        var role = WritingTheWarning || Warning.Length > 0 ? Role.ContentWarning : Role.Muted;

        return Warning.Length > 0
            ? Header(mark, role, new Span(TextWrap.Clip(Warning, room), Role.ContentWarning))
            : Header(mark, role, new Span(TextWrap.Clip(WarningHint, room), Role.Muted));
    }

    /// <summary>One row of the layout, and how long it holds out on a terminal too short for every row.</summary>
    private sealed record Row(Line Line, Keep Keep);

    /// <summary>
    ///     How long a row holds out on a terminal too short for everything, lowest first to go: what is being
    ///     answered gives way first, then the blanks — which say nothing — before the reply header, which says where
    ///     the reply will land.
    /// </summary>
    private enum Keep
    {
        Quote,
        TopBlank,
        BlankUnderHairline,
        ReplyHeader,
        FootHairline,
        CountRow,
        From,
        HeaderHairline,
        Always,
    }
}
