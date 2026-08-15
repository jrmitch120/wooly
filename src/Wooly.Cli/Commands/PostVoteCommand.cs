using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Infrastructure;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Votes in the poll on a post. One of the boost/favorite/pin family in everything but one respect: an instance
///     refuses a second vote outright rather than replacing the first, so this is asked about the way <c>post delete</c>
///     is, and for the same reason — nothing the user runs afterwards undoes it.
/// </summary>
/// <remarks>
///     The options are numbered as they are read, from 1, rather than from the zero the API counts them by: what a
///     person has in front of them is the list this client printed, and a command line that meant the second answer by
///     saying <c>1</c> would be a vote miscast by design.
///     <para>
///         The post is read first, because Mastodon votes on the poll rather than on the post carrying it and the
///         poll's id is only knowable from the post (<see cref="IPostEngagement.Vote" />). That read is also what makes
///         a mistyped choice answerable here — with the options in hand, "3" on a poll of two is a usage error rather
///         than a request the instance has to turn down.
///     </para>
/// </remarks>
internal sealed class PostVoteCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : AsyncCommand<PostVoteCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the post whose poll to vote in, as shown by a timeline.")]
        public string PostId { get; init; } = string.Empty;

        [CommandArgument(1, "<CHOICE>")]
        [Description("Which answer to vote for, numbered from 1 as the poll lists them. Repeat only where the poll lets a voter choose several.")]
        public int[] Choices { get; init; } = [];

        [CommandOption("--yes")]
        [Description("Vote without asking. Implied where there is no terminal to ask at.")]
        public bool Yes { get; init; }

        [CommandOption("--json")]
        [Description("Write the post as JSON, for another program to read.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var post = await posts.Show(profile, settings.PostId, cancellationToken);

        // A boost carries no poll of its own: what would be voted in is the poll on the post inside it.
        var about = post.Boosted ?? post;
        var choices = AnswersNamed(settings, about);

        // A vote nothing takes back, cast on whichever post the id happened to name, so a person at a terminal is
        // asked first — the same bargain post delete strikes, and struck by the same rule (Consent).
        if (!Consent.Given(console, settings.Yes, $"Vote in the poll on post {about.Id}? A vote cannot be taken back."))
        {
            console.MarkupLineInterpolated($"Left the poll on post [bold]{about.Id}[/] alone.");

            // Nothing went wrong: the user was asked, and answered.
            return (int)ExitCode.Success;
        }

        var voted = await posts.Vote(profile, about, choices, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Of(voted));
        }
        else
        {
            PostReport.Voted(console, voted);
        }

        return (int)ExitCode.Success;
    }

    /// <summary>
    ///     The answers named on the command line, as indices into the poll's own options — which is how the port names
    ///     them, and one less than how a person counts them.
    /// </summary>
    /// <exception cref="UsageException">
    ///     The post carries no poll, or a number was given that is not one of its answers. Both are values on the
    ///     command line that are wrong rather than a client that could not do its job.
    /// </exception>
    private static IReadOnlyList<int> AnswersNamed(Settings settings, Post post)
    {
        if (post.Poll is not { } poll)
        {
            throw new UsageException($"Post {post.Id} has no poll on it to vote in.");
        }

        // Answered here rather than by the instance, which is the one thing about a vote this client does settle: the
        // options came back on the read above, so a number that is not one of them is a mistyped command line and can
        // be said so where it was typed — the same bargain PollDraft.Problem strikes on the way up. Nothing further is
        // checked. Whether the poll is still open, whether this account has already voted, and what being told the
        // same answer twice means are all rules the instance holds and this client deliberately does not copy.
        foreach (var choice in settings.Choices)
        {
            if (choice < 1 || choice > poll.Options.Count)
            {
                throw new UsageException(
                    $"There is no answer {choice} on that poll. It offers {Plural.Of(poll.Options.Count, "answer")}, "
                    + "numbered from 1.");
            }
        }

        return [.. settings.Choices.Select(choice => choice - 1)];
    }
}
