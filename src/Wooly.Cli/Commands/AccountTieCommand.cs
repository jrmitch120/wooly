using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the six tie commands do, which is everything except which tie they put on an account and whether they
///     are putting it on or taking it off. Those two are what a subclass supplies, as values rather than as code — the
///     shape ADR-0009 gave a post's marks, for the same reason: the rest happens identically, so <c>unfollow</c> cannot
///     come to behave unlike <c>follow</c>.
/// </summary>
/// <param name="wanted">Whether the tie should end up in place: <see langword="false" /> is the <c>un-</c> verb.</param>
internal abstract class AccountTieCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships,
    AccountTie tie,
    bool wanted) : AsyncCommand<SingleAccountSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SingleAccountSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // Nothing is read first to find out whether the tie is already there. The instance settles that, and asking
        // would cost a round trip to arrive at an answer that could be stale by the time it was acted on.
        var account = await relationships.Set(profile, settings.Address, tie, wanted, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, AccountDocument.Of(account));
        }
        else
        {
            AccountReport.Tied(console, account, tie, wanted);
        }

        return (int)ExitCode.Success;
    }
}
