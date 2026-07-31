using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything the composing commands do, which is everything except where on the command line the text is, whether
///     it answers another post, and who it should reach. Those differences live in the settings; the rest — resolving
///     the profile, falling back to the profile's preferred visibility, publishing, and choosing between output for a
///     person and output for a program — happens identically, so a reply cannot come to behave unlike a post, and a
///     direct message cannot come to behave unlike either (ADR-0013).
/// </summary>
internal abstract class PostComposeCommand<TSettings>(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IConfigStore config,
    IPostAuthor posts) : AsyncCommand<TSettings>
    where TSettings : PostComposeSettings
{
    /// <summary>Where a subclass overriding <see cref="Report" /> writes its own sentence.</summary>
    protected IAnsiConsole Console => console;

    /// <summary>
    ///     Says what became of the post, for a person to read. Overridden by <c>dm send</c>, which has something to say
    ///     that the id and the visibility do not cover: who the message went to.
    /// </summary>
    /// <remarks>
    ///     Only the prose differs. <c>--json</c> is written the one way for every composing command, because a script
    ///     reading a published post should not have to know which one published it.
    /// </remarks>
    protected virtual void Report(TSettings settings, Post published) => PostReport.Published(console, published);

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
            Report(settings, published);
        }

        return (int)ExitCode.Success;
    }
}
