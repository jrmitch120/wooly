using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Turns a waiting account away. They are told nothing, and may ask again.</summary>
internal sealed class AccountRequestRejectCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountRequestAnswerCommand(console, profiles, relationships, accepted: false);
