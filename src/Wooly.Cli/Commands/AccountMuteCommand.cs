using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Mutes an account: still followed and still able to follow, simply not shown.</summary>
internal sealed class AccountMuteCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountTieCommand(console, profiles, relationships, AccountTie.Mute, wanted: true);
