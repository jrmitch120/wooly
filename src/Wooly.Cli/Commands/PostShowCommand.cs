using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Shows one post, named by its id and read on its own — for looking at something a timeline has scrolled past, or
///     for a script that holds an id and wants the post behind it. The post itself reads exactly as it does on a
///     timeline, because both ask <see cref="PostReport.Write" /> for it, with the web address added underneath for the
///     reason <see cref="PostReport.Shown" /> gives.
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
            PostReport.Shown(console, post);
        }

        return (int)ExitCode.Success;
    }
}
