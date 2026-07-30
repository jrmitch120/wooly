using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the six marking commands do, which is everything except which mark they put on a post and whether
///     they are putting it on or taking it off. Those two are what a subclass supplies, as values rather than as code:
///     the rest — resolving the profile, asking the instance, and choosing between output for a person and output for a
///     program — happens identically, so that <c>unboost</c> cannot come to behave unlike <c>boost</c>.
/// </summary>
/// <param name="wanted">Whether the mark should end up on the post: <see langword="false" /> is the <c>un-</c> verb.</param>
internal abstract class PostMarkCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IPostEngagement posts,
    PostMark mark,
    bool wanted) : AsyncCommand<SinglePostSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SinglePostSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // Nothing is read first to find out whether the post already carries the mark. The instance settles that, and
        // asking would cost a round trip to arrive at an answer that could be stale by the time it was acted on.
        var marked = await posts.Mark(profile, settings.PostId, mark, wanted, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Of(marked));
        }
        else
        {
            PostReport.Marked(console, marked, mark, wanted);
        }

        return (int)ExitCode.Success;
    }
}
