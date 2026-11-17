using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reads the posts sharing a <b>Trending link</b> (#603): what people are saying about an article, which is why it
///     trends. A link that is not trending has none, and is reported as not found rather than as an empty list
///     (<see cref="Core.Errors.LinkNotTrendingException" />), so that a script can tell the two apart by exit code.
/// </summary>
internal sealed class TimelineLinkCommand(IAnsiConsole console, IProfileRegistry profiles, ITimelineReader timelines)
    : TimelineCommand<TimelineLinkCommand.Settings>(console, profiles, timelines)
{
    internal sealed class Settings : TimelineSettings
    {
        [CommandArgument(0, "<URL>")]
        [Description("The trending link's address, in full: https://example.com/article.")]
        public string Link { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            var shared = base.Validate();

            if (!shared.Successful)
            {
                return shared;
            }

            // Checked here as well as in the domain, as a tag is, so that an address that is no web page is answered as
            // a usage error against what the user just typed rather than as a defect further in. Both sites ask
            // SharedLink, so neither can start saying something the other does not.
            return SharedLink.Parse(Link) is null
                ? ValidationResult.Error(SharedLink.Rejection(Link))
                : ValidationResult.Success();
        }
    }

    protected override Timeline TimelineToRead(ActiveProfile profile, Settings settings) =>
        Timeline.Sharing(SharedLink.Parse(settings.Link)!);
}
