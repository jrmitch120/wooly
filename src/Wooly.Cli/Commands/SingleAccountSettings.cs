using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Accounts;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every command that names one account takes: the account's address, and whether the answer is for a person
///     or for another program. Declared once because the six ties all take exactly this, and six copies would be six
///     chances for <c>unmute</c> to accept an address <c>mute</c> turns down.
/// </summary>
internal class SingleAccountSettings : ProfileScopedSettings
{
    [CommandArgument(0, "<ACCOUNT>")]
    [Description("The account, as user@instance — or a bare username for somebody on your own instance.")]
    public string Account { get; init; } = string.Empty;

    [CommandOption("--json")]
    [Description("Write the account as JSON, for another program to read.")]
    public bool Json { get; init; }

    /// <summary>
    ///     The account named, which <see cref="Validate" /> has already established is one — an address that is not
    ///     cannot reach here.
    /// </summary>
    public AccountAddress Address => AccountAddress.Parse(Account);

    public override ValidationResult Validate() =>
        AccountAddress.IsWellFormed(Account)
            ? ValidationResult.Success()
            : ValidationResult.Error(AccountAddress.Rejection(Account));
}
