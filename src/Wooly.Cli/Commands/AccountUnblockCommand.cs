using Spectre.Console;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Commands;

/// <summary>Lifts a block. Nothing that the block undid comes back — a follow it broke has to be made again.</summary>
internal sealed class AccountUnblockCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccountRelationships relationships)
    : AccountTieCommand(console, profiles, relationships, AccountTie.Block, wanted: false);
