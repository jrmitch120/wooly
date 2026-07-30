using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the two list commands do, which is everything except which side of a follow they list. That one
///     difference is what a subclass supplies; the rest — resolving the profile, deciding that nobody named means the
///     profile's own account, asking for the accounts, and what a rate limit means — happens identically.
/// </summary>
internal abstract class AccountListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships,
    FollowSide side) : AsyncCommand<AccountListCommand.Settings>
{
    internal sealed class Settings : PagedListSettings
    {
        [CommandArgument(0, "[ACCOUNT]")]
        [Description("Whose follows to list, as user@instance. Defaults to the account you are acting as.")]
        public string? Account { get; init; }

        /// <summary>
        ///     The account named, or <see langword="null" /> where none was — which is the profile's own, and the one
        ///     account a user never has to name.
        /// </summary>
        public AccountAddress? Address => Account is null ? null : AccountAddress.Parse(Account);

        /// <inheritdoc />
        protected override string Counted => "account";

        public override ValidationResult Validate() =>
            Account is null || AccountAddress.IsWellFormed(Account)
                ? base.Validate()
                : ValidationResult.Error(AccountAddress.Rejection(Account));
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var fetch = await relationships.List(profile, side, settings.Address, settings.Limit, cancellationToken);

        // Whose list this is, said the way the user would read it back: what they typed, or — where they named nobody
        // — the account the profile signs in as, which is who the instance was asked about.
        var whose = settings.Address?.Text ?? profile.Account;

        if (settings.Json)
        {
            AccountJson.Write(console, side, whose, fetch);
        }
        else
        {
            AccountReport.Write(console, side, whose, fetch);
        }

        // The accounts that did arrive are worth having, so they are written before the limit that stopped the rest is
        // reported at all. Reporting it is ADR-0006's one handler's job — hence throwing rather than printing, which is
        // also what puts the rate-limited exit code on the process.
        if (fetch.StoppedBy is not null)
        {
            throw fetch.StoppedBy;
        }

        return (int)ExitCode.Success;
    }
}
