using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>
///     Follows an account, so its posts reach the profile's home timeline. A locked account is asked rather than
///     followed: what comes back is a request waiting for them to accept it.
/// </summary>
internal sealed class AccountFollowCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountTieCommand(console, profiles, relationships, AccountTie.Follow, wanted: true);
