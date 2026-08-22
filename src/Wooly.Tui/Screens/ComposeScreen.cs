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

        // A reply opens on the warning of what it answers, since a reply to a warned post is usually about the warned
        // thing and an author who has to remember to re-type the warning is one who sometimes will not (#123). Only
        // the words the author wrote carry across: the instance's sensitive flag is a mark over somebody else's
        // attachments, and a fresh compose has none for it to be about — which is also why a post answering nothing
        // opens on an empty field rather than no field at all (#139).
        Warning = purpose == ComposeFor.Reply ? about?.ContentWarning ?? string.Empty : string.Empty;
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
    ///     What the field holds, letter for letter — pre-filled on a reply from the post being answered, and from
    ///     there the author's to keep, edit or clear. It is their post.
    /// </summary>
    /// <remarks>
    ///     Distinct from <see cref="ContentWarning" />, which is this same warning as it goes out on a draft: this is
    ///     the row on screen and the thing a keystroke changes, that is what an instance is asked for.
    /// </remarks>
    public string Warning { get; set; }

    /// <summary>
    ///     Whether this screen has a warning to write at all: both of the two that publish a post, which a reply
    ///     opens pre-filled (#123) and a fresh post opens empty, having nothing to have been filled from (#139).
    /// </summary>
    /// <remarks>
    ///     <see cref="ComposeFor.Edit" /> is the one left out, and for a reason of its own rather than for not having
    ///     been asked about: <see cref="PostEdit" /> tells "leave the warning alone" from "take it away", and a field
    ///     that opens empty says neither. A field opening on the post's own warning would say both — which is #140,
    ///     and is not this.
    /// </remarks>
    public bool TakesAWarning => Purpose != ComposeFor.Edit;

    /// <summary>
    ///     Whether what is typed is going into the warning rather than into the post. Both are on screen at once and
    ///     <c>ctrl-w</c> moves between them, since a terminal editor takes the keys of whichever field has them.
    /// </summary>
    public bool WritingTheWarning { get; private set; }

    /// <inheritdoc />
    /// <remarks>Only while the warning is taking letters, a post's own text being the editor widget's to take.</remarks>
    public override bool IsTyping => WritingTheWarning;

    /// <summary>
    ///     What the warning is as it goes out with the draft: nothing at all where the field holds nothing but spaces.
    ///     An instance reads an empty warning as no warning, so a cleared field sends none rather than putting the
    ///     post behind a blank (<see cref="PostDraft.ContentWarning" />).
    /// </summary>
    /// <remarks>
    ///     Whitespace decides only between a warning and none; it is not tidied out of one there is. What the author
    ///     left in the field is what they wrote, and a client that trimmed it would be editing their words on the way
    ///     past.
    /// </remarks>
    public string? ContentWarning => string.IsNullOrWhiteSpace(Warning) ? null : Warning;

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
        .. TakesAWarning
            ? new KeyHint[] { new("ctrl-w", WritingTheWarning ? "back to the post" : "content warning") }
            : [],
        new("esc", "throw it away"),
        .. WritingTheWarning ? [] : new KeyHint[] { new("?", "keys") },
    ];

    /// <summary>
    ///     How many rows the warning row takes up, which is one on every compose screen — the field where there is
    ///     one, and a blank held in its place where there is not (#142). Room the shell has to leave above the live
    ///     editor, the same way <see cref="AnsweringHeight" /> is, and kept whether or not anything has been typed
    ///     into it, since a field is there to be typed into.
    /// </summary>
    /// <remarks>
    ///     A row held empty is not this project's habit — <c>PostLines.Parts</c> skips a part with nothing in it
    ///     rather than spacing it. This is the exception that earns it: the row is not spacing around a part, it is
    ///     the one part of a compose screen that is sometimes absent, and an editor that starts a row lower on
    ///     <c>c</c> than on <c>e</c> moves the thing the reader is typing into. When #140 gives an edit a field of its
    ///     own, the row is already there and nothing else shifts.
    /// </remarks>
    public int WarningHeight => 1;

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var lines = new List<Line>(Answering(width)) { WarningRow(width) };

        lines.AddRange(TextWrap.Wrap(Text.Length == 0 ? " " : Text, width).Select(row => Line.Of(row, Role.Body)));

        return lines;
    }

    /// <inheritdoc />
    /// <remarks>Into the warning, which is the only thing on this screen the shell carries letters into.</remarks>
    public override void Type(char letter) => Warning += letter;

    /// <inheritdoc />
    public override void Backspace() => Warning = Backspaced(Warning);

    /// <summary>
    ///     <c>ctrl-w</c>: hands the typing to the warning, or hands it back. Answers whether it did, so that the key
    ///     is inert on a screen with no warning field rather than redrawing over nothing.
    /// </summary>
    public bool WriteTheWarning()
    {
        if (!TakesAWarning)
        {
            return false;
        }

        WritingTheWarning = !WritingTheWarning;

        return true;
    }

    /// <summary>
    ///     How many rows <see cref="Answering" /> takes up at <paramref name="width" /> — the room the shell has to
    ///     leave above the live editor so the two do not draw over one another, since the editor is a separate view
    ///     laid on top of the one these rows are painted on rather than a row range inside it.
    /// </summary>
    public int AnsweringHeight(int width) => Answering(width).Count;

    /// <summary>
    ///     The warning field: what this post is going behind, on the row between what is being answered and the
    ///     editor — or a blank held in its place on a screen with no warning to write (#142). The mark and the role a
    ///     warned post's own warning is drawn in (<see cref="PostLines" />), so that a warning being written looks
    ///     like the warning it will become — but not that row itself, which has neither a caret nor anything to say
    ///     about a warning nobody has written yet.
    /// </summary>
    /// <remarks>
    ///     Empty, it says so rather than going blank: a row a reader can type into is a row they have to be able to
    ///     find, and the status row's <c>ctrl-w</c> is the other half of saying so. The caret is a mark rather than a
    ///     colour, the way the search prompt's is, so a terminal with none still says where the typing is going.
    /// </remarks>
    private Line WarningRow(int width)
    {
        if (!TakesAWarning)
        {
            // An edit has no warning to write until #140 settles what changing an already-published one means. The
            // row is held anyway, so that the editor starts in the same place whichever key opened the compose.
            return Line.Blank;
        }

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
    /// <remarks>
    ///     Three rows of what was said, and blank ones are not among them (#141). A post's paragraphs arrive as blank
    ///     lines — <c>PostContent.ToPlainText</c> turns <c>&lt;/p&gt;</c> into two newlines and <c>TextWrap</c> keeps
    ///     the author's own breaks — so a quote that took its three rows in order spent one of them on a gap, and gave
    ///     the reader two rows of words where there was room for three.
    ///     <para>
    ///         The blank underneath is a different thing and stays: it is the seam between what is being answered and
    ///         what is being written, rather than a hole in the middle of a quotation.
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

        lines.Add(Line.Blank);

        return lines;
    }
}
