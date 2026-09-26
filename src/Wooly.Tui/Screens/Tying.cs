using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Screens;

/// <summary>
///     Putting one of the three ties on somebody, or taking it off — whichever they do not already have, which is why a
///     tie is on or off rather than an act of its own (ADR-0012).
/// </summary>
/// <remarks>
///     Said once for the two screens that tie: the account screen, which answers all three capitals, and Discover,
///     which answers <c>F</c> alone (#181). The keys are capitals so that a lower-case mark key cannot fire one by
///     accident (<c>docs/tui-shell.md</c>).
/// </remarks>
internal static class Tying
{
    /// <summary>Puts <paramref name="tie" /> on <paramref name="person" />, or takes it off.</summary>
    public static Task Toggle(Reach reach, Account person, AccountTie tie)
    {
        var address = AccountAddress.Parse(person.Address);
        var wanted = !(person.Standing?.Has(tie) ?? false);

        return reach.Put(
            ask => ask.Of(token => reach.Ports.Accounts.Set(reach.Profile, address, tie, wanted, token)),
            eitherWay: stood =>
            {
                reach.Tell(new Change.Tied(stood));
                reach.Say(Said(tie, wanted, stood), isError: false);
            });
    }

    /// <summary>What a tie that has just gone on or come off is worth saying about, in this project's words.</summary>
    /// <remarks>
    ///     A follow is the one that may not have gone through as asked: following a locked account leaves a request
    ///     behind rather than a follow, and the instance's own answer is the only thing that says which happened
    ///     (CONTEXT.md).
    /// </remarks>
    private static string Said(AccountTie tie, bool wanted, Account account) => (tie, wanted) switch
    {
        (AccountTie.Follow, true) when account.Standing?.IsFollowWaiting == true => $"Asked to follow @{account.Address}.",
        (AccountTie.Follow, true) => $"Following @{account.Address}.",
        (AccountTie.Follow, false) => $"No longer following @{account.Address}.",
        (AccountTie.Block, true) => $"Blocked @{account.Address}.",
        (AccountTie.Block, false) => $"Unblocked @{account.Address}.",
        (AccountTie.Mute, true) => $"Muted @{account.Address}.",
        _ => $"Unmuted @{account.Address}.",
    };
}
