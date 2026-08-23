using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    /// <summary>What the warning row says while nobody has written a warning into it.</summary>
    private const string NoWarningWritten = "no content warning";

    private readonly bool _aboutIsMine;

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
    public ComposeScreen(ComposeFor purpose, Post? about = null, string? addressing = null, bool aboutIsMine = false)
    {
        Purpose = purpose;
        About = about;
        _aboutIsMine = aboutIsMine;

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
        ComposeFor.Post => "compose",
        ComposeFor.Reply => $"reply to @{About?.Account}",
        ComposeFor.Edit => "edit",
        _ => "compose",
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
        .. WritingTheWarning ? [] : new KeyHint[] { new("?", "keys") },
    ];

    /// <summary>
    ///     How many rows the warning band takes up, which is two on every compose screen: the field, and the blank
    ///     standing above it. Room the shell has to leave above the live editor, the same way
    ///     <see cref="AnsweringHeight" /> is, and kept whether or not anything has been typed into it, since a field
    ///     is there to be typed into.
    /// </summary>
    /// <remarks>
    ///     The blank is the band's rather than the reply block's (#143). It reads as space above the warning either
    ///     way, but only one of the two puts it on every screen: hung off the block, it appeared on a reply and
    ///     nowhere else, and the one row the three screens have in common was the row they spaced differently.
    ///     <para>
    ///         Held at two rows on an edit while an edit had no warning to write, both of them blank, against the habit
    ///         that a part with nothing in it is skipped rather than spaced (#142) — so that the editor did not start
    ///         higher on <c>e</c> than on <c>c</c>. #140 gave the edit a field of its own and nothing shifted, which is
    ///         what that row was being held for.
    ///     </para>
    /// </remarks>
    public int WarningHeight => 2;

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var lines = new List<Line>(Answering(width));

        lines.AddRange(WarningRows(width));
        lines.AddRange(TextWrap.Wrap(Text.Length == 0 ? " " : Text, width).Select(row => Line.Of(row, Role.Body)));

        return lines;
    }

    /// <inheritdoc />
    /// <remarks>Into the warning, which is the only thing on this screen the shell carries letters into.</remarks>
    public override void Type(char letter) => Warning += letter;

    /// <inheritdoc />
    public override void Backspace() => Warning = Backspaced(Warning);

    /// <summary><c>ctrl-w</c>: hands the typing to the warning, or hands it back.</summary>
    public void WriteTheWarning() => WritingTheWarning = !WritingTheWarning;

    /// <summary>
    ///     How many rows <see cref="Answering" /> takes up at <paramref name="width" /> — the room the shell has to
    ///     leave above the live editor so the two do not draw over one another, since the editor is a separate view
    ///     laid on top of the one these rows are painted on rather than a row range inside it.
    /// </summary>
    public int AnsweringHeight(int width) => Answering(width).Count;

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
    ///     The warning band: a blank, then the field. Both rows from the one method, so what is painted and what
    ///     <see cref="WarningHeight" /> leaves room for cannot come to differ — and the blank lands above the warning
    ///     on every compose screen rather than only on the one whose block used to end in it (#143).
    /// </summary>
    private IReadOnlyList<Line> WarningRows(int width) => [Line.Blank, WarningRow(width)];

    /// <summary>
    ///     The field itself: what this post is going behind, on the row between what is being answered and the
    ///     editor. The mark and the role a warned post's own warning is drawn in (<see cref="PostLines" />), so that a
    ///     warning being written looks like the warning it will become — but not that row itself, which has neither a
    ///     caret nor anything to say about a warning nobody has written yet.
    /// </summary>
    /// <remarks>
    ///     Empty, it says so rather than going blank: a row a reader can type into is a row they have to be able to
    ///     find, and the status row's <c>ctrl-w</c> is the other half of saying so. The caret is a mark rather than a
    ///     colour, the way the search prompt's is, so a terminal with none still says where the typing is going.
    /// </remarks>
    private Line WarningRow(int width)
    {
        const string mark = PostLines.WarningMark;
        var room = Math.Max(0, width - mark.Length);

        if (WritingTheWarning)
        {
            // A column left for the caret, which is the one thing on this row that has to stay visible: a reader who
            // has typed past the width would otherwise be looking at a row with no sign of where their next letter
            // goes.
            return Line.Of(
                new Span(mark, Role.ContentWarning),
                new Span(TextWrap.Clip(Warning, Math.Max(0, room - 1)), Role.ContentWarning),
                new Span("▌", Role.Selection));
        }

        return Warning.Length > 0
            ? Line.Of(
                new Span(mark, Role.ContentWarning),
                new Span(TextWrap.Clip(Warning, room), Role.ContentWarning))
            : Line.Of(
                new Span(mark, Role.Muted),
                new Span(TextWrap.Clip(NoWarningWritten, room), Role.Muted));
    }

    /// <summary>
    ///     What is being answered, which stays on screen above the editor — the thing the rejected "editor under the
    ///     feed" was for. It costs four rows here and no layout at all. The label is the feed's own reply mark, said
    ///     by the one thing that says it (<see cref="PostReplyName" />, #82) — never the bare "↳ reply", since compose
    ///     always holds the full post it answers.
    /// </summary>
    /// <returns>
    ///     The label and up to three rows of what was said, and nothing else: no blank above and none below, so that
    ///     the warning row underneath is spaced the same here as on a compose that has no block at all.
    /// </returns>
    /// <remarks>
    ///     Three rows of what was said, and blank ones are not among them (#141). A post's paragraphs arrive as blank
    ///     lines — <c>PostContent.ToPlainText</c> turns <c>&lt;/p&gt;</c> into two newlines and <c>TextWrap</c> keeps
    ///     the author's own breaks — so a quote that took its three rows in order spent one of them on a gap, and gave
    ///     the reader two rows of words where there was room for three.
    ///     <para>
    ///         Nothing under the last of them, either. The blank that used to end this block belongs to
    ///         <see cref="WarningRows" /> now (#143): it reads as space above the warning either way, and hung off
    ///         the block it appeared on a reply and nowhere else — so the one row all three compose screens have in
    ///         common was the row they spaced differently.
    ///     </para>
    /// </remarks>
    private IReadOnlyList<Line> Answering(int width)
    {
        if (About is not { } answered || Purpose != ComposeFor.Reply)
        {
            return [];
        }

        var lines = new List<Line>();
        var label = PostReplyName.Answering(answered.Account, _aboutIsMine);
        lines.Add(Line.Of(TextWrap.Clip(label, width), Role.Muted));

        var said = TextWrap.Wrap(answered.Content, Math.Max(1, width - 2))
                           .Where(row => row.Length > 0)
                           .Take(3);

        foreach (var row in said)
        {
            lines.Add(Line.Of($"  {row}", Role.Muted));
        }

        return lines;
    }
}
