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

            // An option this client does not have is a user expecting something of it that it does not do — most
            // pointedly --password, which ADR-0004 rules out. Left relaxed, Spectre collects such an option as a
            // leftover argument and carries on, so the user is answered by silence.
            config.UseStrictParsing();

            // Left to itself, Spectre renders failures to stdout and exits -1 — breaching both the stderr-only rule
            // and the reserved exit codes for every failure the CLI can have.
            config.SetExceptionHandler((exception, _) => CommandFailure.Report(exception, errorConsole));

            config.AddCommand<VersionCommand>("version")
                  .WithDescription("Print the client's version.");

            config.AddBranch("profile", profile =>
            {
                profile.SetDescription("Manage the local profiles this client acts as.");

                profile.AddCommand<ProfileAddCommand>("add")
                       .WithDescription("Connect a profile to a Mastodon account, through your browser by default.");

                profile.AddCommand<ProfileListCommand>("list")
                       .WithDescription("List the profiles set up on this machine.");

                profile.AddCommand<ProfileShowCommand>("show")
                       .WithDescription("Report the profile this client would act as.");

                profile.AddCommand<ProfileSwitchCommand>("switch")
                       .WithDescription("Change the profile commands act as by default.");
            });

            config.AddBranch("post", post =>
            {
                post.SetDescription("Write, change and take down the current profile's own posts.");

                post.AddCommand<PostCreateCommand>("create")
                    .WithDescription("Publish a new post, optionally with a content warning, files or a poll.");

                post.AddCommand<PostReplyCommand>("reply")
                    .WithDescription("Publish a post answering another one, composed the same way as any other post.");

                post.AddCommand<PostEditCommand>("edit")
                    .WithDescription("Change what one of your posts says, leaving the rest of it as it was.");

                post.AddCommand<PostDeleteCommand>("delete")
                    .WithDescription("Take one of your posts down. This cannot be undone.");
            });

            config.AddBranch("timeline", timeline =>
            {
                timeline.SetDescription("Read a timeline as the current profile.");

                timeline.AddCommand<TimelineHomeCommand>("home")
                        .WithDescription("Read the posts of the accounts you follow.");

                timeline.AddCommand<TimelineLocalCommand>("local")
                        .WithDescription("Read the public posts of accounts on your own instance.");

                timeline.AddCommand<TimelineFederatedCommand>("federated")
                        .WithDescription("Read the public posts reaching your instance from everywhere it federates with.");

                timeline.AddCommand<TimelineTagCommand>("tag")
                        .WithDescription("Read the public posts carrying a hashtag.");
            });
        });

        return app;
    }

    private static IAnsiConsole CreateStandardErrorConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
}
