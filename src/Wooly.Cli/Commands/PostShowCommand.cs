using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Shows one post, named by its id and read on its own — for looking at something a timeline has scrolled past, or
///     for a script that holds an id and wants the post behind it. What it prints is what a timeline prints for the
///     same post, because both ask <see cref="PostReport.Write" /> for it.
/// </summary>
internal sealed class PostShowCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : AsyncCommand<SinglePostSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SinglePostSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var post = await posts.Show(profile, settings.PostId, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Of(post));
        }
        else
        {
            PostReport.Write(console, post);
        }

        return (int)ExitCode.Success;
    }
}
