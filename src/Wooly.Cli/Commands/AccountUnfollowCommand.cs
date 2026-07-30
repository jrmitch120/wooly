using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Stops following an account, and withdraws a follow request that has not been answered yet.</summary>
internal sealed class AccountUnfollowCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountTieCommand(console, profiles, relationships, AccountTie.Follow, wanted: false);
