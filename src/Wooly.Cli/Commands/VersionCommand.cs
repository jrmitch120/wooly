using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core;

namespace Wooly.Cli.Commands;

/// <summary>Prints the running client's name and version.</summary>
internal sealed class VersionCommand(IAnsiConsole console, IClientInfo clientInfo) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        console.WriteLine($"{clientInfo.Name} {clientInfo.Version}");

        return 0;
    }
}
