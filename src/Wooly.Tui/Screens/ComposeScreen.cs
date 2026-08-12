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
    private readonly bool _aboutIsMine;

    /// <param name="purpose">What this screen was opened to do.</param>
    /// <param name="about">The post being replied to or edited.</param>
    /// <param name="addressing">
    ///     Who this is being written to, as the mentions that reach them, or <see langword="null" /> for a post that
    ///     addresses nobody — with a space after it, because their own words go after the recipient rather than into
    ///     their name. Two things ask for one: a direct reply, which reaches the accounts its text mentions and nobody
    ///     else (ADR-0013), so the mention is put where the reader can see and edit it rather than added silently on
    ///     the way out; and a fresh post opened on a picked mention, which is a reader saying who they mean to write
    ///     to before they have written anything (#85).
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
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("ctrl-s", Purpose == ComposeFor.Edit ? "save" : "send"),
        new("esc", "throw it away"),
        new("?", "keys"),
    ];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var lines = new List<Line>(Answering(width));

        lines.AddRange(TextWrap.Wrap(Text.Length == 0 ? " " : Text, width).Select(row => Line.Of(row, Role.Body)));

        return lines;
    }

    /// <summary>
    ///     How many rows <see cref="Answering" /> takes up at <paramref name="width" /> — the room the shell has to
    ///     leave above the live editor so the two do not draw over one another, since the editor is a separate view
    ///     laid on top of the one these rows are painted on rather than a row range inside it.
    /// </summary>
    public int AnsweringHeight(int width) => Answering(width).Count;

    /// <summary>
    ///     What is being answered, which stays on screen above the editor — the thing the rejected "editor under the
    ///     feed" was for. It costs four rows here and no layout at all. The label is the feed's own reply mark, said
    ///     by the one thing that says it (<see cref="PostReplyName" />, #82) — never the bare "↳ reply", since compose
    ///     always holds the full post it answers.
    /// </summary>
    private IReadOnlyList<Line> Answering(int width)
    {
        if (About is not { } answered || Purpose != ComposeFor.Reply)
        {
            return [];
        }

        var lines = new List<Line>();
        var label = PostReplyName.Answering(answered.Account, _aboutIsMine);
        lines.Add(Line.Of(TextWrap.Clip(label, width), Role.Muted));

        foreach (var row in TextWrap.Wrap(answered.Content, Math.Max(1, width - 2)).Take(3))
        {
            lines.Add(Line.Of($"  {row}", Role.Muted));
        }

        lines.Add(Line.Blank);

        return lines;
    }
}
