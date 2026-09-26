using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What one reading of an account came back with, which is everything an account screen is built from: who they
///     are, their timeline with anything pinned taken out of it, what they have pinned, and which of the people the
///     reader follows follow them too.
/// </summary>
/// <remarks>
///     A record rather than a tuple, since #182 made it four things — a positional tuple of four
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
internal sealed record AccountRead(
    Account Account,
    IReadOnlyList<Post> Posts,
    IReadOnlyList<Post>? Pinned,
    IReadOnlyList<Account>? Familiar,
    bool WithReplies) : Found
{
    /// <inheritdoc />
    /// <remarks>
    ///     Said once, so that the run a screen says it shows is the run it was read with.
    /// </remarks>
    public override Screen Becomes(Screen? standing) =>
        new AccountScreen(Account, Posts, Pinned, Familiar, WithReplies);
}
