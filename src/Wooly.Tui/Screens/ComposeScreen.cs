using Wooly.Core.Posts;
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
/// <param name="purpose">What this screen was opened to do.</param>
/// <param name="about">The post being replied to or edited.</param>
/// <param name="opening">
///     What is already in the editor when it opens, for a reply the shell has to address: a direct message reaches the
///     accounts its text mentions and nobody else (ADR-0013), so the mention is put where the reader can see and edit
///     it rather than added silently on the way out.
/// </param>
public sealed class ComposeScreen(ComposeFor purpose, Post? about = null, string? opening = null) : Screen
{
    /// <summary>What this screen was opened to do.</summary>
    public ComposeFor Purpose { get; } = purpose;

    /// <summary>The post being replied to or edited, or <see langword="null" /> for one answering nothing.</summary>
    public Post? About { get; } = about;

    /// <summary>
    ///     What was in the editor before anybody typed: the post itself for an edit, the mention for a reply that had
    ///     to be addressed, and nothing for anything else.
    /// </summary>
    public string Opening { get; } = Opened(purpose, about, opening);

    /// <summary>What has been written so far.</summary>
    public string Text { get; set; } = Opened(purpose, about, opening);

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
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("ctrl-s", Purpose == ComposeFor.Edit ? "save" : "send"),
        new("esc", "throw it away"),
        new("?", "keys"),
    ];

    /// <summary>
    ///     What the editor opens with. A mention gets a space after it, because the reader's own words go after the
    ///     recipient rather than into their name — which is the one place this differs from
    ///     <see cref="Wooly.Core.Conversations.DirectMessage.To(Wooly.Core.Accounts.AccountAddress,string)" />, whose
    ///     job is what gets sent rather than what is being typed into.
    /// </summary>
    private static string Opened(ComposeFor purpose, Post? about, string? opening) => purpose switch
    {
        ComposeFor.Edit => about?.Content ?? string.Empty,
        _ => string.IsNullOrEmpty(opening) ? string.Empty : $"{opening} ",
    };

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var lines = new List<Line>();

        if (About is { } answered && Purpose == ComposeFor.Reply)
        {
            // What is being answered stays on screen, which is the thing the rejected "editor under the feed" was for.
            // It costs four rows here and no layout at all.
            lines.Add(Line.Of(TextWrap.Clip($"Answering @{answered.Account}:", width), Role.Muted));

            foreach (var row in TextWrap.Wrap(answered.Content, Math.Max(1, width - 2)).Take(3))
            {
                lines.Add(Line.Of($"  {row}", Role.Muted));
            }

            lines.Add(Line.Blank);
        }

        lines.AddRange(TextWrap.Wrap(Text.Length == 0 ? " " : Text, width).Select(row => Line.Of(row, Role.Body)));

        return lines;
    }
}
