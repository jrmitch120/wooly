using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Commands;
using Wooly.Cli.Infrastructure;
using Wooly.Core;

namespace Wooly.Cli;

/// <summary>
///     Composes the CLI: the command tree, the container the commands are resolved from, and the console they render
///     to. <c>Program</c> is a one-liner over this so tests can drive the exact same pipeline against an in-memory
///     console.
/// </summary>
public static class WoolyCommandApp
{
    /// <summary>Builds the configured command app.</summary>
    /// <param name="console">
    ///     Where output is rendered. Defaults to the real terminal; tests pass an in-memory console.
    /// </param>
    public static CommandApp Create(IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddSingleton(console);

        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(config =>
        {
            config.SetApplicationName(WoolyClient.Name);
            config.ConfigureConsole(console);

            config.AddCommand<VersionCommand>("version")
                  .WithDescription("Print the client's version.");
        });

        return app;
    }
}
