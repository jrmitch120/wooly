using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything both answers to a follow request do, which is everything except which answer they give. Accepting and
///     rejecting take the same id, report the same account and differ in one word, so they are one command with a value
///     supplied rather than two that could drift apart.
/// </summary>
/// <param name="accepted"><see langword="true" /> lets them follow; <see langword="false" /> turns them away.</param>
internal abstract class AccountRequestAnswerCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships,
    bool accepted) : AsyncCommand<AccountRequestAnswerCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the account that asked, as shown by 'account requests list'.")]
        public string RequestId { get; init; } = string.Empty;

        [CommandOption("--json")]
        [Description("Write the account as JSON, for another program to read.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var account = await relationships.Answer(profile, settings.RequestId, accepted, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, AccountDocument.Of(account));
        }
        else
        {
            AccountReport.Answered(console, account, accepted);
        }

        return (int)ExitCode.Success;
    }
}
