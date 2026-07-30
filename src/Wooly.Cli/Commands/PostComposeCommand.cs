using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the two composing commands do, which is everything except where on the command line the text is and
///     whether it answers another post. Those differences live in the settings; the rest — resolving the profile, falling
///     back to the profile's preferred visibility, publishing, and choosing between output for a person and output for a
///     program — happens identically, so a reply cannot come to behave unlike a post.
/// </summary>
internal abstract class PostComposeCommand<TSettings>(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IConfigStore config,
    IPostAuthor posts) : AsyncCommand<TSettings>
    where TSettings : PostComposeSettings
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // What the user did not say on the command line, they may have said once in the config file. Read here rather
        // than inside the settings, which have no way to reach a config file and should not learn one.
        var draft = settings.ToDraft(config.Load().Preferences.DefaultVisibility);

        var published = await posts.Publish(profile, draft, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Of(published));
        }
        else
        {
            PostReport.Published(console, published);
        }

        return (int)ExitCode.Success;
    }
}
