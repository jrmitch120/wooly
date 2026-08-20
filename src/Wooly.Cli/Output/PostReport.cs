using Spectre.Console;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a post for a person to read — both what became of one, and one in full.
///     <para>
///         What became of one is short on purpose: an author who has just published knows what they wrote, and what they
///         do not know is the id, which is how every later command names this post, and that it went out as narrowly as
///         they asked.
///     </para>
///     <para>
///         In full is <see cref="Write" />, and it lives here rather than wherever a post happens to be printed so that a
///         post read on its own reads the same as the same post read on a timeline. Everything that came from an instance
///         is written as text rather than markup, because a post's content is the author's and a square bracket in it is
///         not a colour tag.
///     </para>
/// </summary>
internal static class PostReport
{
    /// <summary>
    ///     What stands in front of an address the reader can follow for themselves — an attachment's, and a link
    ///     preview's. One mark rather than two, the same one the TUI puts in front of the rows <c>⏎</c> opens: it says
    ///     there is somewhere to go from here rather than what is on the other end of it.
    /// </summary>
    private const string LinkMark = "⏵";

    /// <summary>Reports the post that has just been published.</summary>
    public static void Published(IAnsiConsole console, Post post)
    {
        // The visibility comes off the published post rather than off the draft, so this reports what the instance
        // actually did — which for a draft that left the choice to the account is the only place it is knowable.
        console.MarkupLineInterpolated($"Posted [bold]{post.Id}[/] ({PostVisibilityName.Of(post.Visibility)}).");

        console.WriteAddress(post.Url);
    }

    /// <summary>Reports the post that has just been changed.</summary>
    public static void Edited(IAnsiConsole console, Post post)
    {
        console.MarkupLineInterpolated($"Edited [bold]{post.Id}[/].");

        console.WriteAddress(post.Url);
    }

    /// <summary>Reports the post that has just been taken down, which there is nothing left to link to.</summary>
    public static void Deleted(IAnsiConsole console, string postId) =>
        console.MarkupLineInterpolated($"Deleted post [bold]{postId}[/].");

    /// <summary>Reports the mark that has just been put on a post, or taken off it.</summary>
    /// <remarks>
    ///     The post named is the one the user named. Boosting answers with a post of the booster's own, and reporting
    ///     that one's id would hand back an id nothing else knows the post by — which is why the port unwraps it.
    /// </remarks>
    public static void Marked(IAnsiConsole console, Post post, PostMark mark, bool wanted)
    {
        console.MarkupLineInterpolated($"{Did(mark, wanted)} [bold]{post.Id}[/].");

        console.WriteAddress(post.Url);
    }

    /// <summary>Reports the vote just cast, and the poll as the instance now has it.</summary>
    /// <remarks>
    ///     The poll rather than the whole post, which the voter was looking at when they chose: what they do not yet
    ///     know is where their vote left the counts, and that is the whole of what came back.
    /// </remarks>
    public static void Voted(IAnsiConsole console, Post post)
    {
        console.MarkupLineInterpolated($"Voted in the poll on [bold]{post.Id}[/].");

        if (post.Poll is { } poll)
        {
            WritePoll(console, poll);
        }

        console.WriteAddress(post.Url);
    }

    /// <summary>Reports one post asked for by id: the post itself, and where to read it on the web.</summary>
    /// <remarks>
    ///     The address is what a single post gets that a timeline's posts do not. A timeline is read down, and one
    ///     address per post would be a line of noise on every one of them; a post asked for by id is being looked at,
    ///     and the address is the thing that cannot be worked out from what is on screen.
    /// </remarks>
    public static void Shown(IAnsiConsole console, Post post)
    {
        Write(console, post);

        console.WriteAddress(post.Url);
    }

    /// <summary>Writes the post itself: who wrote it, when, what it says, and how it has been received.</summary>
    public static void Write(IAnsiConsole console, Post post)
    {
        // A boost carries none of its own text, so what gets shown is the post it points at — with the account that
        // boosted it named above, which is the only part of the boost worth reading.
        var shown = post.Boosted ?? post;

        if (post.IsBoost)
        {
            console.MarkupLineInterpolated(
                $"[bold]{post.Account}[/] boosted [bold]{shown.Account}[/]  [dim]{PostedAt(shown)}[/]");
        }
        else
        {
            console.MarkupLineInterpolated($"[bold]{shown.Account}[/]  [dim]{PostedAt(shown)}[/]");
        }

        if (PostReplyName.Of(shown) is { } answers)
        {
            console.MarkupLineInterpolated($"  [dim]{answers}[/]");
        }

        if (shown.ContentWarning is not null)
        {
            console.MarkupLineInterpolated($"  [yellow]content warning:[/] {shown.ContentWarning}");
        }

        // A post can have no text at all — one that is nothing but media, or a poll. Splitting an empty string would
        // yield one empty line, and print a post's worth of blank space for it.
        string[] lines = shown.Content.Length == 0 ? [] : shown.Content.Split('\n');

        foreach (var line in lines)
        {
            // The blank line between two paragraphs is indented like the rest of the post only if you cannot see it;
            // on screen it would be two spaces of trailing whitespace, and in a pipe it would be two spaces.
            if (line.Length == 0)
            {
                console.WriteLine();

                continue;
            }

            console.MarkupLineInterpolated($"  {line}");
        }

        WriteMedia(console, shown);
        WriteLinkPreview(console, shown);

        if (shown.Poll is { } poll)
        {
            WritePoll(console, poll);
        }

        console.MarkupLineInterpolated($"  [dim]{Counts(shown)}[/]");
    }

    /// <summary>
    ///     What is attached, as a link and what it shows — a picture no differently from a video (stories 50 and 51).
    ///     The CLI never attempts to draw anything: this output is as likely to be in a pipe or a log as on a terminal,
    ///     and a client that drew a picture where the terminal happened to allow it would make the same command produce
    ///     different bytes on two machines.
    /// </summary>
    /// <remarks>
    ///     One line per attachment, so that a script picking media out of a post can take it a line at a time. The
    ///     description follows the address rather than leading it, because the address is the fixed-shape part and a
    ///     description is whatever length its author made it.
    ///     <para>
    ///         Written on every post, including a timeline's, which <see cref="Shown" /> declines to do with a post's
    ///         own web address. The two are not the same thing: a post's address is another way to reach what is
    ///         already on screen, so a line of it per post is a line of noise per post, whereas an attachment's address
    ///         is the only way to reach the attachment at all. Leaving it off a timeline would make <c>timeline home</c>
    ///         the one place a picture is mentioned and cannot be opened.
    ///     </para>
    ///     <para>
    ///         This row is <em>not</em> the duplication a link preview's was, and stays as it is (#125). The one
    ///         attachment both surfaces describe the same way is a picture no terminal is drawing, and the sentence
    ///         they describe it with already lives once — <see cref="PostMedia.Shows" />, which this row and the TUI's
    ///         <c>LinkedImage</c> both read, differing only in where each puts the address. Every other kind the TUI
    ///         does not describe at all: it walks to a label naming the kind and prints no address, because <c>⏎</c>
    ///         opens the thing. So there is one sentence, said once, and a second surface saying something else — not
    ///         one rule written twice.
    ///     </para>
    /// </remarks>
    private static void WriteMedia(IAnsiConsole console, Post post)
    {
        foreach (var attached in post.Media)
        {
            // Interpolated rather than concatenated, so the address and the description are escaped on the way in:
            // both are somebody else's text, and a square bracket in either is a character rather than a colour tag.
            console.MarkupLineInterpolated($"  {LinkMark} {attached.Url} — {attached.Shows}");
        }
    }

    /// <summary>
    ///     What an instance made of a link the post's own text already carries: the address to reach it by and the
    ///     title beside it, then the site, the description and the page's byline a step further in. Nothing at all
    ///     for a post the instance made nothing of.
    /// </summary>
    /// <remarks>
    ///     After the attachments, which is the order both surfaces render a post in (ADR-0018), and written the same
    ///     way one is: the mark, the address, and then whatever there is to say about it — so a script reading media
    ///     out of a post line by line reads this the same way.
    ///     <para>
    ///         Written whether or not the post is <see cref="Post.IsWarned" />, unlike the TUI, which hides it until
    ///         the reader asks past the warning. The asymmetry is the one #113 already settled for attachments: the
    ///         CLI draws no picture for a warning to be about, and offers no key to ask past one with, so hiding an
    ///         address here would only make the link unreachable.
    ///     </para>
    ///     <para>
    ///         What it says is <see cref="LinkPreview.Called" />'s and <see cref="LinkPreview.Says" />'s, read by the
    ///         TUI too so that the two cannot come to describe the same page differently (#125). The address is on
    ///         this row whatever the instance named the page, which is why this asks what it was <c>Called</c> rather
    ///         than for its <c>Name</c>: the last step of that fallback is the address, and taking it would write the
    ///         address twice on the one row. The escaping and the two-then-four-space indent are this surface's own.
    ///     </para>
    /// </remarks>
    private static void WriteLinkPreview(IAnsiConsole console, Post post)
    {
        if (post.LinkPreview is not { } link)
        {
            return;
        }

        // Interpolated for the reason WriteMedia gives: every part of this is somebody else's text, and a square
        // bracket anywhere in it is a character rather than a colour tag.
        if (link.Called is { } named)
        {
            console.MarkupLineInterpolated($"  {LinkMark} {link.Url} — {named}");
        }
        else
        {
            console.MarkupLineInterpolated($"  {LinkMark} {link.Url}");
        }

        foreach (var row in link.Says)
        {
            console.MarkupLineInterpolated($"    {row}");
        }
    }

    /// <summary>
    ///     A poll in full: one line per option — a block bar, the share and raw count beside it, and a leading mark on
    ///     the option this profile picked — followed by whether and when it closes, and a note where more than one
    ///     answer may be chosen. Plain text throughout: this is a post's own content, not something the CLI themes.
    /// </summary>
    private static void WritePoll(IAnsiConsole console, PostPoll poll)
    {
        foreach (var option in poll.Options)
        {
            console.MarkupLineInterpolated($"  {PollOptionLine(poll, option)}");
        }

        if (poll.Closed)
        {
            console.WriteLine("  Poll closed.");
        }
        else if (poll.ExpiresAt is { } expires)
        {
            console.WriteLine($"  Poll closes {LocalMoment.Of(expires)}.");
        }

        if (poll.MultipleChoice)
        {
            console.WriteLine("  Choose as many as you like.");
        }

        console.WriteLine($"  {VoteCountLine(poll)}");
    }

    /// <summary>
    ///     How many votes the poll has drawn. Multiple choice lets one account cast several, so the count says how
    ///     many accounts that was too — but only once an instance has actually reported that number, which
    ///     <see cref="PostPoll.Voters" /> being <see langword="null" /> says it has not.
    /// </summary>
    private static string VoteCountLine(PostPoll poll) => poll.MultipleChoice && poll.Voters is { } voters
        ? $"{Plural.Of(poll.Votes, "vote")} from {Plural.Of(voters, "account")}"
        : Plural.Of(poll.Votes, "vote");

    /// <summary>
    ///     One option's line: a leading <c>✓</c> where this profile picked it, then a <c>▓</c>/<c>░</c> bar sized to
    ///     the share of the vote it drew, the percentage and raw count, and the option's own text. An option whose
    ///     count is withheld — real until this profile votes or the poll closes, not the same thing as a genuine zero
    ///     — gets no bar at all rather than one guessed at.
    /// </summary>
    private static string PollOptionLine(PostPoll poll, PostPollOption option)
    {
        var mark = option.Picked ? "✓ " : "  ";

        if (option.Votes is not { } votes)
        {
            return $"{mark}{option.Text}";
        }

        var percent = PollBar.PercentOf(poll, votes);

        return $"{mark}{PollBar.Of(percent)} {percent}% ({votes})  {option.Text}";
    }

    /// <summary>
    ///     What just happened, in this project's vocabulary. One table, so that six commands cannot come to describe
    ///     three marks in more than six ways.
    /// </summary>
    private static string Did(PostMark mark, bool wanted) => (mark, wanted) switch
    {
        (PostMark.Boost, true) => "Boosted",
        (PostMark.Boost, false) => "Unboosted",
        (PostMark.Favorite, true) => "Favorited",
        (PostMark.Favorite, false) => "Unfavorited",
        (PostMark.Pin, true) => "Pinned",
        (PostMark.Pin, false) => "Unpinned",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "Not a mark this client puts on a post."),
    };

    private static string PostedAt(Post post) => LocalMoment.Of(post.PostedAt);

    private static string Counts(Post post) =>
        $"{Plural.Of(post.Boosts, "boost")}, {Plural.Of(post.Favorites, "favorite")}, "
        + $"{Plural.Of(post.Replies, "reply", "replies")}";
}
