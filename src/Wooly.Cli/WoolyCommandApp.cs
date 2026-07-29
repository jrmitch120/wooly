using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Commands;
using Wooly.Cli.Infrastructure;
using Wooly.Core;

namespace Wooly.Cli;

/// <summary>
///     Composes the CLI: the command tree, the container the commands are resolved from, the console they render to,
///     and the failure handling every command inherits. <c>Program</c> is a one-liner over this so tests can drive the
///     exact same pipeline against in-memory consoles.
/// </summary>
public static class WoolyCommandApp
{
    /// <summary>Builds the configured command app.</summary>
    /// <param name="console">
    ///     Where command output is rendered. Defaults to the real terminal's stdout; tests pass an in-memory console.
    /// </param>
    /// <param name="errorConsole">
    ///     Where failures are rendered. Defaults to the real terminal's stderr, keeping error text out of anything
    ///     piping stdout (ADR-0006).
    /// </param>
    /// <param name="configureServices">
    ///     Applied last to the container, so a caller can replace part of the core layer — the seam tests use to fake
    ///     the network instead of reaching a live instance.
    /// </param>
    public static CommandApp Create(
        IAnsiConsole? console = null,
        IAnsiConsole? errorConsole = null,
        Action<IServiceCollection>? configureServices = null)
    {
        console ??= AnsiConsole.Console;
        errorConsole ??= CreateStandardErrorConsole();

        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddSingleton(console);
        configureServices?.Invoke(services);

        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(config =>
        {
            config.SetApplicationName(WoolyClient.Name);
            config.ConfigureConsole(console);

            // Left to itself, Spectre renders failures to stdout and exits -1 — breaching both the stderr-only rule
            // and the reserved exit codes for every failure the CLI can have.
            config.SetExceptionHandler((exception, _) => CommandFailure.Report(exception, errorConsole));

            config.AddCommand<VersionCommand>("version")
                  .WithDescription("Print the client's version.");
        });

        return app;
    }

    private static IAnsiConsole CreateStandardErrorConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
}
