using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Cli.Commands;

/// <summary>Reads the public posts carrying one hashtag.</summary>
internal sealed class TimelineTagCommand(IAnsiConsole console, IProfileRegistry profiles, ITimelineReader timelines)
    : TimelineCommand<TimelineTagCommand.Settings>(console, profiles, timelines)
{
    internal sealed class Settings : TimelineSettings
    {
        [CommandArgument(0, "<HASHTAG>")]
        [Description("The hashtag to read, with or without its leading #.")]
        public string Hashtag { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            var shared = base.Validate();

            if (!shared.Successful)
            {
                return shared;
            }

            // Checked here as well as in the domain so that the commonest way of getting this wrong — a lone "#" left
            // by quoting the hash and forgetting the tag, or a phrase where a word belongs — is answered by the
            // argument parser, as a usage error against the value the user just typed, rather than as a defect further
            // in. Both sites ask the same rule, so neither can start saying something the other does not.
            return Core.Timelines.Hashtag.IsWellFormed(Hashtag)
                ? ValidationResult.Success()
                : ValidationResult.Error(Core.Timelines.Hashtag.Rejection(Hashtag));
        }
    }

    protected override Timeline TimelineToRead(Settings settings) => Timeline.Tag(settings.Hashtag);
}
