using System.Drawing;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Rendering;
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

/// <summary>Who a compose screen's post goes out as, for its From header (#317).</summary>
/// <param name="Handle">The profile's account, as a byline's handle: <c>@jeff</c>.</param>
/// <param name="Instance">The instance it is on, which tells two profiles with the same username apart.</param>
public sealed record ComposeFrom(string Handle, string Instance)
{
    /// <summary>
    ///     Who <paramref name="profile" /> posts as: its account's username, or the profile's own name where the
    ///     account has not been verified, on the instance the profile signs in to.
    /// </summary>
    public static ComposeFrom Of(ActiveProfile profile) =>
        new(profile.Account is { } account ? $"@{account.Split('@')[0]}" : profile.Name, profile.Instance);
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
    public ComposeScreen(
        ComposeFor purpose,
        Post? about = null,
        string? addressing = null,
        bool aboutIsMine = false,
        ComposeFrom? from = null)
    {
        Purpose = purpose;
        About = about;
        _aboutIsMine = aboutIsMine;
        _from = from;

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

    /// <summary>What has been written so far.</summary>
    public string Text { get; set; }

    /// <summary>
    ///     What the field holds, letter for letter — pre-filled on a reply from the post being answered and on an edit
    ///     from the post being changed, and from there the author's to keep, edit or clear. It is their post.
    /// </summary>
    /// <remarks>
    ///     The row on screen and the thing a keystroke changes, and nothing else's to set: what it <em>means</em> on
    ///     the way out is <see cref="Outgoing" />'s, which is the same field read two ways and the reason nobody
    ///     outside gets to choose between them (#146).
    /// </remarks>
    public string Warning { get; private set; }

    /// <summary>
    ///     Whether what is typed is going into the warning rather than into the post. Both are on screen at once and
    ///     <c>ctrl-w</c> moves between them, since a terminal editor takes the keys of whichever field has them.
    /// </summary>
    public bool WritingTheWarning { get; private set; }

    /// <inheritdoc />
    /// <remarks>Only while the warning is taking letters, a post's own text being the editor widget's to take.</remarks>
    public override bool IsTyping => WritingTheWarning;

    /// <inheritdoc />
    public override bool HoldsADraft => true;

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
    ///     While the warning is taking letters the keymap key goes unsaid, because <c>?</c> is a question somebody is
    ///     entitled to warn about and every printable key is going into the field — the rule the search prompt already
    ///     keeps (<c>docs/tui-shell.md</c>).
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("ctrl-s", Purpose == ComposeFor.Edit ? "save" : "send"),
        new("ctrl-w", WritingTheWarning ? "back to the post" : "content warning"),
        new("esc", "throw it away"),
        .. WritingTheWarning ? [] : new KeyHint[] { PostKeys.Asking },
    ];

    /// <inheritdoc />
    /// <remarks>
    ///     The whole of the content region, laid out to its height (<see cref="Drawing.Height" />) rather than
    ///     scrolled: the headers, the hairline, blank rows where the editor is laid over them, and the foot.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing) => Laid(drawing.Width, drawing.Height).Rows;

    /// <inheritdoc />
    /// <remarks>Into the warning, which is the only thing on this screen the shell carries letters into.</remarks>
    public override void Type(char letter) => Warning += letter;

    /// <inheritdoc />
    public override void Backspace() => Warning = Backspaced(Warning);

    /// <summary><c>ctrl-w</c>: hands the typing to the warning, or hands it back.</summary>
    public void WriteTheWarning() => WritingTheWarning = !WritingTheWarning;

    /// <summary>
    ///     Where the editor goes inside the content panel's viewport of <paramref name="viewport" />: under the
    ///     headers and the hairline below them, inside the padding either side, and down to the hairline at the foot.
    ///     Said here rather than worked out by the window from the rows above, because this is what paints those rows
    ///     (#315) — and both come from the one layout, so they cannot come to disagree.
    /// </summary>
    public Rectangle EditorAt(Size viewport) => Laid(viewport.Width, viewport.Height).Editor;

    /// <summary>
    ///     The screen laid out at <paramref name="width" /> by <paramref name="height" />: every row, top to bottom,
    ///     and where the editor sits among them (#317, variant A of the prototype).
    /// </summary>
    /// <remarks>
    ///     A blank; the headers — From, the reply header and its quote on a reply, the warning; a hairline; a blank;
    ///     the editor; a hairline; the row the count sits on. Two columns of padding either side of all of it.
    ///     <para>
    ///         Never so far down that there is no editor left. ADR-0015 priced the editor's share of a 24-row
    ///         terminal at more than what sits above it, but a terminal can be any size, and an editor that starts
    ///         below the foot is one nobody can type in. Where there is not room for everything, rows give way in the
    ///         order <see cref="Row.Keep" /> ranks them: the tail of what is being answered first, the warning never —
    ///         it is a row the reader types into, and one they cannot see is worse than a quote that stops early.
    ///         Where nobody says how tall the room is, it is as tall as the rows and the editor's least want.
    ///     </para>
    /// </remarks>
    private (IReadOnlyList<Line> Rows, Rectangle Editor) Laid(int width, int? height)
    {
        var inner = Math.Max(0, width - (Pad * 2));
        var valueWidth = Math.Max(0, inner - LabelWidth - LabelGap);
        var hairline = Line.Of(Gap(Pad), new Span(new string('─', inner), Role.PanelBorder));

        var above = new List<Row> { new(Line.Blank, Keep.TopBlank), new(Header("From", Role.Muted, From(valueWidth)), Keep.From) };

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

        above.Add(new Row(WarningHeader(valueWidth), Keep.Always));
        above.Add(new Row(hairline, Keep.HeaderHairline));
        above.Add(new Row(Line.Blank, Keep.BlankUnderHairline));

        var foot = new List<Row> { new(hairline, Keep.FootHairline), new(Line.Blank, Keep.CountRow) };
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

        return (rows, new Rectangle(Math.Min(Pad, width), top, inner, editor));
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
    ///         Silence rather than a visibility of the screen's choosing. A reply is answered as narrowly as the post
    ///         it answers, and a post says nothing so that the account's own default on the instance decides — this
    ///         shell has no visibility picker to have been told anything by.
    ///     </para>
    /// </remarks>
    private PostDraft Drafted() => new()
    {
        Text = Text,
        ContentWarning = ContentWarnings.Written(Warning),
        InReplyTo = Purpose == ComposeFor.Reply ? About?.Id : null,
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
    ///     find, and while it is being written it hints at what goes there instead. The caret is a mark rather than a
    ///     colour, the way the search prompt's is, so a terminal with none still says where the typing is going.
    /// </remarks>
    private Line WarningHeader(int room)
    {
        var mark = PostLines.WarningMark.Trim();

        if (WritingTheWarning)
        {
            // A column left for the caret, which is the one thing on this row that has to stay visible: a reader who
            // has typed past the width would otherwise be looking at a row with no sign of where their next letter
            // goes.
            var written = TextWrap.Clip(Warning, Math.Max(0, room - 1));
            var hint = Warning.Length == 0 ? TextWrap.Clip(WarningBeingWritten, Math.Max(0, room - 1)) : string.Empty;

            return Header(
                mark,
                Role.ContentWarning,
                new Span(written, Role.ContentWarning),
                new Span("▌", Role.Selection),
                new Span(hint, Role.Muted));
        }

        return Warning.Length > 0
            ? Header(mark, Role.ContentWarning, new Span(TextWrap.Clip(Warning, room), Role.ContentWarning))
            : Header(mark, Role.Muted, new Span(TextWrap.Clip(NoWarningWritten, room), Role.Muted));
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
