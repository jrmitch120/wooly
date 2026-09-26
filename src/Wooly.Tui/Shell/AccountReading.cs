using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What an account screen is read with: who they are, what they have posted, and which of the people the reader
///     follows follow them too.
/// </summary>
/// <remarks>
///     Said once for the three places that read an account — opening one, asking it for what is there now, and the
///     account screen's own <c>s</c> — so that a refresh and a swap are the same calls the screen was opened by rather
///     than a second opinion about what an account screen is made of (#84, #229). Out of the shell rather than in it,
///     because the third of those is the screen's and reaches no further than <see cref="Reach" /> (#232).
/// </remarks>
public static class AccountReading
{
    /// <summary>Reads the account <paramref name="address" /> names.</summary>
    /// <remarks>
    ///     Four calls under one enquiry, so it is checked once at the end rather than after each: what matters is
    ///     whether the reader is still where they were when they asked, not how far the answer got.
    ///     <para>
    ///         The pinned run is read by naming the account rather than by marking the posts already in hand, because
    ///         an instance reports a post's own pin mark only to whoever wrote it — and it is read <em>before</em>
    ///         familiar followers, being content where the other is one decorative row, so it takes the better odds
    ///         against a rate limit (#182).
    ///     </para>
    ///     <para>
    ///         Familiar followers is asked <em>last</em>, and is the one of the four that answers rather than throws
    ///         where the instance refuses it: it decorates a single row, so a rate limit reached here leaves the whole
    ///         screen standing with that row missing rather than taking the account and its posts down with it
    ///         (ADR-0012's amendment).
    ///     </para>
    /// </remarks>
    /// <param name="ask">The enquiry the four calls are put under.</param>
    /// <param name="ports">What they are asked through.</param>
    /// <param name="profile">Who is asking.</param>
    /// <param name="address">Whose account.</param>
    /// <param name="withReplies">
    ///     Whether their timeline is read with their replies in. Opening an account never asks for it — the
    ///     screen-reader reasoning in ADR-0019 is the default — and only <c>s</c>, and a <c>g</c> on the screen
    ///     <c>s</c> widened, do (#229).
    /// </param>
    public static async Task<AccountRead> Read(
        Enquiry.Ask ask,
        ShellPorts ports,
        ActiveProfile profile,
        AccountAddress address,
        bool withReplies)
    {
        var account = await ask.Of(token => ports.Accounts.Show(profile, address, token));

        // The three reads that follow travel on the resolution this one just made rather than on the address it was
        // made from, which is what makes the arrival one lookup instead of three (ADR-0012's second amendment). It is
        // always the id Show answered with and never one the caller arrived holding: a refresh is the one command
        // meaning "check this is still true", so it must be the one command that can correct a wrong id.
        var whose = NamedAccount.Resolved(account);

        var posts = await ask.Of(token =>
            ports.Timelines.Read(
                profile,
                withReplies ? Timeline.WithReplies(whose) : Timeline.By(whose),
                Arrival.PostsWanted,
                token));

        var pinned = await ask.Of(token =>
            ports.Timelines.Read(profile, Timeline.Pinned(whose), Arrival.PostsWanted, token));

        var familiar = await ask.Of(token => ports.Accounts.FamiliarFollowers(profile, account.Id, token));

        // A rate limit that stopped this read is a question that went unput rather than an account with nothing
        // pinned, and the screen says which of the two it was. Nothing is salvaged from a stopped one: a pinned run is
        // a single page in the account's own order, so what a limit stops it holds none of, and a count drawn over
        // part of one would head a run the instance never finished listing.
        var pins = pinned.IsComplete ? pinned.Items : null;

        // Dropped from the timeline rather than from the pinned run, and dropped here rather than on the screen, so
        // the two lists reach it disjoint and it cannot disagree with itself about which run a post is in. A recent
        // pinned normal post can be in both (#182), and with replies read in, so can a recent pinned reply (#229).
        var pinnedIds = (pins ?? []).Select(post => post.Id).ToHashSet(StringComparer.Ordinal);

        return new AccountRead(
            account,
            [.. posts.Items.Where(post => !pinnedIds.Contains(post.Id))],
            pins,
            familiar,
            withReplies);
    }
}

/// <summary>
///     What one reading of an account came back with, which is everything an account screen is built from: who they
///     are, their timeline with anything pinned taken out of it, what they have pinned, and which of the people the
///     reader follows follow them too.
/// </summary>
/// <remarks>
///     A record rather than a tuple, since #182 made it four things two call sites unpack — a positional tuple of four
///     would be four chances to hand the screen its posts as its pinned run.
/// </remarks>
/// <param name="Account">Who they are, as the instance last answered.</param>
/// <param name="Posts">Their timeline, disjoint from <paramref name="Pinned" />.</param>
/// <param name="Pinned">
///     What they have pinned, in the instance's own order — or <see langword="null" /> where a rate limit stopped the
///     question being answered at all.
/// </param>
/// <param name="Familiar">Who the reader knows in common, or <see langword="null" /> where the instance never answered.</param>
/// <param name="WithReplies">Whether <paramref name="Posts" /> was read with their replies in.</param>
public sealed record AccountRead(
    Account Account,
    IReadOnlyList<Post> Posts,
    IReadOnlyList<Post>? Pinned,
    IReadOnlyList<Account>? Familiar,
    bool WithReplies)
{
    /// <summary>
    ///     The account screen this reading builds — said once, so that the run a screen says it shows is the run it
    ///     was read with.
    /// </summary>
    public AccountScreen Screen() => new(Account, Posts, Pinned, Familiar, WithReplies);
}
