using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Lists the accounts that follow one — the profile's own, unless another is named.</summary>
internal sealed class AccountFollowersCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountListCommand(console, profiles, relationships, FollowSide.Followers);
