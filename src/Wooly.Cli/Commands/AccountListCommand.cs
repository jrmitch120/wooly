using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the two list commands read, which is one side of an account's follows and differs only in which side. That
///     one difference is what a subclass supplies, along with deciding that nobody named means the profile's own
///     account; the rest is <see cref="PagedListCommand{TSettings,TItem}" />'s.
/// </summary>
internal abstract class AccountListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships,
    FollowSide side) : PagedListCommand<AccountListCommand.Settings, Account>(profiles)
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

    protected override PagedList<Account> Listing(ActiveProfile profile, Settings settings)
    {
        // Whose list this is, said the way the user would read it back: what they typed, or — where they named nobody
        // — the account the profile signs in as, which is who the instance was asked about.
        var whose = settings.Address?.Text ?? profile.Account;

        return new PagedList<Account>(
            token => relationships.List(profile, side, settings.Address, settings.Limit, token),
            fetch => AccountJson.Write(console, side, whose, fetch),
            fetch => AccountReport.Write(console, side, whose, fetch));
    }
}
