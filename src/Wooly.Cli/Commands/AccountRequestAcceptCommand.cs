using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Lets a waiting account follow the profile.</summary>
internal sealed class AccountRequestAcceptCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountRequestAnswerCommand(console, profiles, relationships, accepted: true);
