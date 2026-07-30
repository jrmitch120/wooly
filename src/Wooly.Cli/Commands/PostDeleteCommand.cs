using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Takes one of the profile's own posts down. The only command here whose effect cannot be undone by running another
///     one, which is why it asks first — but only where there is somebody to ask.
/// </summary>
internal sealed class PostDeleteCommand(IAnsiConsole console, IProfileRegistry profiles, IPostAuthor posts)
    : AsyncCommand<PostDeleteCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the post to take down, as shown by a timeline.")]
        public string PostId { get; init; } = string.Empty;

        [CommandOption("--yes")]
        [Description("Delete without asking. Implied where there is no terminal to ask at.")]
        public bool Yes { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        if (!ConfirmedBy(settings))
        {
            console.MarkupLineInterpolated($"Left post [bold]{settings.PostId}[/] alone.");

            // Nothing went wrong: the user was asked, and answered. A script cannot reach this at all — see below.
            return (int)ExitCode.Success;
        }

        await posts.Delete(profile, settings.PostId, cancellationToken);

        PostReport.Deleted(console, settings.PostId);

        return (int)ExitCode.Success;
    }

    /// <summary>
    ///     Whether to go ahead. A person at a terminal is asked, because a mistyped id here is a post nobody can get
    ///     back. A script is not: there is nothing to prompt at and nobody to read the prompt, and stopping to ask would
    ///     make this command unusable in the automation the CLI exists for. Typing the command with an id in it is that
    ///     invocation's consent, and <c>--yes</c> is how a person says the same thing.
    /// </summary>
    private bool ConfirmedBy(Settings settings)
    {
        if (settings.Yes || !console.Profile.Capabilities.Interactive)
        {
            return true;
        }

        return console.Confirm($"Delete post {settings.PostId}? This cannot be undone.", defaultValue: false);
    }
}
