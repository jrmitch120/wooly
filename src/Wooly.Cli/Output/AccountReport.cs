using System.Globalization;
using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Paging;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes an account for a person to read — both what became of one, and one in full.
///     <para>
///         In full is <see cref="Write(IAnsiConsole,Account)" />, and it lives here rather than wherever an account
///         happens to be printed so that an account a search turned up and the same account in a followers list cannot
///         come to look like two different accounts. It is the argument ADR-0009 made for a post, at the layer
///         ADR-0011 said an account would need it.
///     </para>
///     Everything that came from an instance is written as text rather than markup: a display name is the account's own
///     and a square bracket in one is not a colour tag.
/// </summary>
internal static class AccountReport
{
    /// <summary>
    ///     What marks a custom field the instance has proved, the same mark the TUI puts there. Mastodon only ever
    ///     verifies a link, so it always lands on an address — and a field nobody proved says nothing extra, absence
    ///     being the honest signal (CONTEXT.md).
    /// </summary>
    private const string VerifiedMark = " ✓";

    /// <summary>Writes one account: who they are, how much of a presence they have, and where to read them.</summary>
    /// <remarks>The address leads, because it is what every <c>account</c> command asks the user to type.</remarks>
    public static void Write(IAnsiConsole console, Account account)
    {
        console.MarkupLineInterpolated($"[bold]{account.Address}[/]  {account.Author}");

        WriteRest(console, account);
    }

    /// <summary>
    ///     Writes one account asked for by name, in full: who they are, when they arrived, what they wrote about
    ///     themselves, the rows they set under it, where the profile stands with them, and where to read them.
    /// </summary>
    /// <remarks>
    ///     Its own writer rather than more rows on <see cref="Write(IAnsiConsole,Account)" />, because that one is what
    ///     a followers list prints for everybody on it and a list has no business printing forty bios (ADR-0019). What
    ///     the two share is the byline and the presence, so the account a list turned up and the account read on its
    ///     own open with the same two rows.
    ///     <para>
    ///         Every section brings its own separator, so an account with no bio, no fields and no note collapses to
    ///         its three rows and its standing with no gaps left behind — the shape the TUI's header block takes, for
    ///         the same reason.
    ///     </para>
    /// </remarks>
    public static void Shown(IAnsiConsole console, Account account)
    {
        console.MarkupLineInterpolated($"[bold]{account.Address}[/]  {account.Author}");
        console.MarkupLineInterpolated($"  [dim]{Presence(account)}[/]");
        console.MarkupLineInterpolated($"  [dim]{Marks(account)}[/]");

        // Their own words, in the console's own colour: it is what the reader asked this command for, and dimming it
        // would put the one thing they came to read behind the counts above it.
        Section(console, Bio(account));
        Section(console, account.Fields.Select(Field));
        Section(console, [Stands(account.Standing), .. Note(account.Standing?.Note)], dim: true);

        console.WriteAddress(account.Url, "  ");
    }

    /// <summary>Writes a list of accounts, or says that there are none of them.</summary>
    /// <param name="whose">
    ///     The account whose list this is, so that an empty one says whose it was — read on its own, "no followers"
    ///     could as easily be about the account that was named as about the profile that asked.
    /// </param>
    public static void Write(IAnsiConsole console, FollowSide side, string? whose, Fetch<Account> fetch)
    {
        if (fetch.Items.Count == 0)
        {
            // Only when the list really is empty. A fetch a rate limit stopped before anything arrived is reported as
            // that failure, and saying "no followers" as well would be saying the opposite of what happened.
            if (fetch.IsComplete)
            {
                console.MarkupLineInterpolated($"{Nobody(side, whose)}");
            }

            return;
        }

        foreach (var account in fetch.Items)
        {
            Write(console, account);
            console.WriteLine();
        }
    }

    /// <summary>Writes the accounts waiting to be let in, each led by the id that answers it.</summary>
    public static void WriteRequests(IAnsiConsole console, Fetch<Account> fetch)
    {
        if (fetch.Items.Count == 0)
        {
            if (fetch.IsComplete)
            {
                console.MarkupLine("No follow requests waiting.");
            }

            return;
        }

        foreach (var account in fetch.Items)
        {
            // The id leads, for the reason a notification's does: it is the one thing on the line that cannot be
            // worked out from the rest of it, and the one thing the next command asks the user to type.
            console.MarkupLineInterpolated($"[bold]{account.Id}[/]  {account.Address}  {account.Author}");

            WriteRest(console, account);
            console.WriteLine();
        }
    }

    /// <summary>Reports the tie that has just been put on an account, or taken off it.</summary>
    public static void Tied(IAnsiConsole console, Account account, AccountTie tie, bool wanted)
    {
        console.MarkupLineInterpolated($"{Did(account, tie, wanted)} [bold]{account.Address}[/].");

        console.WriteAddress(account.Url);
    }

    /// <summary>Reports the follow request that has just been answered.</summary>
    public static void Answered(IAnsiConsole console, Account account, bool accepted)
    {
        console.MarkupLineInterpolated(
            $"{(accepted ? "Accepted the follow request from" : "Rejected the follow request from")} [bold]{account.Address}[/].");

        console.WriteAddress(account.Url);
    }

    /// <summary>
    ///     What just happened, in this project's vocabulary. One table, so that six commands cannot come to describe
    ///     three ties in more than six ways.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Following is the one that does not always do what was asked: a locked account is asked rather than
    ///         followed, and only the standing the instance answered with says which happened. Saying "now following"
    ///         over a request nobody has accepted yet would tell the user their timeline is about to change when it is
    ///         not.
    ///     </para>
    ///     Unfollowing says only that it was done, because the same command also withdraws a follow request that was
    ///     never accepted — and what came back cannot tell the two apart, since either way the profile now neither
    ///     follows the account nor is waiting on it. "No longer following" would claim a follow that may never have
    ///     existed.
    /// </remarks>
    private static string Did(Account account, AccountTie tie, bool wanted) => (tie, wanted) switch
    {
        (AccountTie.Follow, true) when account.Standing is { IsFollowWaiting: true } => "Asked to follow",
        (AccountTie.Follow, true) => "Now following",
        (AccountTie.Follow, false) => "Unfollowed",
        (AccountTie.Block, true) => "Blocked",
        (AccountTie.Block, false) => "Unblocked",
        (AccountTie.Mute, true) => "Muted",
        (AccountTie.Mute, false) => "Unmuted",
        _ => throw new ArgumentOutOfRangeException(nameof(tie), tie, "Not a tie this client keeps with an account."),
    };

    /// <summary>What an empty list says, in the words of the side that was asked for and about whoever was asked about.</summary>
    private static string Nobody(FollowSide side, string? whose) => whose is null
        ? side.Either("No followers.", "Following nobody.")
        : side.Either($"Nobody follows {whose}.", $"{whose} follows nobody.");

    /// <summary>The part of an account that reads the same whichever line named it: its presence, and its address.</summary>
    private static void WriteRest(IAnsiConsole console, Account account)
    {
        console.MarkupLineInterpolated($"  [dim]{Presence(account)}[/]");

        // Where the instance was asked, what the profile has done about this account is worth more than any of the
        // counts — and where it was not asked, saying nothing is the only true thing to say.
        if (account.Standing is not null && Standing(account.Standing) is { Length: > 0 } standing)
        {
            console.MarkupLineInterpolated($"  [dim]{standing}[/]");
        }

        console.WriteAddress(account.Url, "  ");
    }

    /// <summary>
    ///     <paramref name="rows" /> under the blank that separates them from whatever is above, or nothing at all
    ///     where there are none — which is what stops a section that says nothing leaving a gap behind it.
    /// </summary>
    /// <param name="dim">
    ///     Whether the rows are said quietly, which everything about an account is except the account's own writing.
    /// </param>
    private static void Section(IAnsiConsole console, IEnumerable<string> rows, bool dim = false)
    {
        var said = rows.ToList();

        if (said.Count == 0)
        {
            return;
        }

        console.WriteLine();

        foreach (var row in said)
        {
            // Interpolated either way, so that a bracket somebody wrote in their own bio is printed rather than read
            // as a colour tag.
            if (dim)
            {
                console.MarkupLineInterpolated($"  [dim]{row}[/]");
            }
            else
            {
                console.MarkupLineInterpolated($"  {row}");
            }
        }
    }

    /// <summary>
    ///     When they arrived and whichever flags they carry, on one row — the word carrying and the glyph decorating,
    ///     so a terminal that draws either as a box loses nothing. A flag nobody set says nothing rather than saying no.
    /// </summary>
    private static string Marks(Account account)
    {
        List<string> said = [Joined(account)];

        if (account.IsBot)
        {
            said.Add("⚙ bot");
        }

        if (account.IsLocked)
        {
            said.Add("⚿ locked");
        }

        return string.Join(", ", said);
    }

    /// <summary>
    ///     When they arrived, as a month and a year — because nothing about an account measures an age in days, and a
    ///     date here would only invite a time of day to be printed beside it. The same two the TUI says it in.
    /// </summary>
    private static string Joined(Account account) =>
        $"Joined {account.Joined.ToString("MMM yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>
    ///     What they wrote about themselves, a row per line they wrote, or nothing at all where they wrote nothing —
    ///     which draws no section rather than an empty one.
    /// </summary>
    private static IEnumerable<string> Bio(Account account) =>
        string.IsNullOrWhiteSpace(account.Bio) ? [] : account.Bio.Split('\n');

    /// <summary>One of the rows they set under their bio, marked where the instance proved what it says.</summary>
    private static string Field(AccountField field) =>
        $"{field.Label}: {field.Said}{(field.Verified is null ? string.Empty : VerifiedMark)}";

    /// <summary>
    ///     Where the profile stands with them, said even where that is nothing: this is the one command asked the
    ///     question, and a silence here would read as a line that failed to print rather than as an answer.
    /// </summary>
    /// <remarks>
    ///     Absent is not the same as nothing (CONTEXT.md) — an account whose standing was never asked for says so,
    ///     rather than being reported as an account the profile has no ties with. <see cref="IAccountRelationships.Show" /> always asks, so
    ///     this is the honest reading of a state that command cannot reach rather than a case it expects.
    /// </remarks>
    private static string Stands(AccountStanding? standing)
    {
        if (standing is null)
        {
            return "Standing not asked for.";
        }

        return Standing(standing) is { Length: > 0 } said ? said : "No ties either way.";
    }

    /// <summary>
    ///     What the profile wrote to itself about them, or nothing where it wrote nothing. Named as theirs, because a
    ///     note read without saying whose it is reads as something the account wrote.
    /// </summary>
    private static IEnumerable<string> Note(string? note) =>
        string.IsNullOrWhiteSpace(note) ? [] : [$"Your note: {note}"];

    private static string Presence(Account account) =>
        $"{Plural.Of(account.Followers, "follower")}, {Plural.Of(account.Posts, "post")}, "
        + $"following {Plural.Of(account.Following, "account")}";

    /// <summary>
    ///     Where the profile stands with the account, naming only what is in fact the case. An account that is nothing
    ///     to the profile has nothing said about it rather than a line of four negatives.
    /// </summary>
    private static string Standing(AccountStanding standing)
    {
        List<string> said = [];

        if (standing.Following)
        {
            said.Add("following");
        }

        if (standing.FollowRequested)
        {
            said.Add("follow requested");
        }

        if (standing.FollowedBy)
        {
            said.Add("follows you");
        }

        if (standing.Blocking)
        {
            said.Add("blocked");
        }

        if (standing.Muting)
        {
            said.Add("muted");
        }

        return string.Join(", ", said);
    }
}
