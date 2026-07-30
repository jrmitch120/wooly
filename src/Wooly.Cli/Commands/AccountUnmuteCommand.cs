using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Lifts a mute, so the account is shown again wherever it was already followed.</summary>
internal sealed class AccountUnmuteCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountTieCommand(console, profiles, relationships, AccountTie.Mute, wanted: false);
