using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Options;
using Wooly.Core.Posts;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything a post can be composed of, whichever command is composing it. Inherited rather than repeated, because
///     the ticket asks that replying offer exactly what posting offers — and two lists of options kept in step by hand
///     is how one of them comes to be missing the newest one.
///     <para>
///         The text itself is not here: <c>post create</c> takes it as its first argument and <c>post reply</c> as its
///         second, so each command declares its own and answers <see cref="Text" /> with it.
///     </para>
/// </summary>
internal abstract class PostComposeSettings : ProfileScopedSettings
{
    /// <summary>
    ///     How long a poll stays open when the command line does not say. A day is long enough for a timeline to have
    ///     shown the post to everyone it is going to, and short enough that an answer is still worth having.
    /// </summary>
    private static readonly TimeSpan DefaultPollOpenFor = TimeSpan.FromHours(24);

    [CommandOption("--cw <TEXT>")]
    [Description("Put the post behind a content warning, which readers see instead of its text.")]
    public string? ContentWarning { get; init; }

    [CommandOption("--media <PATH>")]
    [Description("Attach a file, with optional alt text after a colon — 'cat.png:a ginger cat'. Repeat for more.")]
    public string[] Media { get; init; } = [];

    [CommandOption("--poll <ANSWER>")]
    [Description("Offer an answer to vote for. Repeat for each answer; a poll needs at least two.")]
    public string[] PollAnswers { get; init; } = [];

    [CommandOption("--poll-open <DURATION>")]
    [Description("How long the poll accepts votes for — 30m, 6h, 7d. Defaults to 24h.")]
    public string? PollOpenFor { get; init; }

    [CommandOption("--poll-multiple")]
    [Description("Let a voter choose more than one answer.")]
    public bool PollMultipleChoice { get; init; }

    [CommandOption("--json")]
    [Description("Write the published post as JSON, for another program to read.")]
    public bool Json { get; init; }

    /// <summary>The post's own text, wherever on the command line this command takes it.</summary>
    public abstract string Text { get; }

    /// <summary>The id of the post being answered, or <see langword="null" /> for a post of its own.</summary>
    public virtual string? InReplyTo => null;

    /// <summary>
    ///     Who the post should reach, and whether this invocation settled that or inherited it.
    /// </summary>
    /// <remarks>
    ///     A hook rather than an option declared here, because not every composing command has the question to ask.
    ///     <c>post create</c> and <c>post reply</c> offer <c>--visibility</c> (<see cref="PostPublishSettings" />);
    ///     <c>dm send</c> answers direct whatever anyone types, because a direct message that went out any other way is
    ///     not one — and offering a flag that can only be given one value is an invitation to give it another.
    /// </remarks>
    /// <param name="whenUnsaid">
    ///     The profile's own preferred visibility from the config file, or <see langword="null" /> to leave the choice
    ///     to the account's setting on the instance.
    /// </param>
    /// <param name="audience">
    ///     Who the post should reach, or <see langword="null" /> where what was typed names nobody. Nothing partial is
    ///     handed back on failure: a <see cref="ComposedVisibility" /> holding a null visibility reads as the perfectly
    ///     good "leave it to the instance", and a caller that missed the problem would act on it.
    /// </param>
    /// <param name="problem">What is wrong with what was typed, or <see langword="null" /> if nothing is.</param>
    /// <remarks>The <c>Try</c> shape is <see cref="TryCompose" />'s, so both halves of composing read the same way.</remarks>
    protected virtual bool TryChooseAudience(
        PostVisibility? whenUnsaid,
        [NotNullWhen(true)] out ComposedVisibility? audience,
        [NotNullWhen(false)] out string? problem)
    {
        audience = new ComposedVisibility(whenUnsaid, Chosen: false);
        problem = null;

        return true;
    }

    /// <summary>
    ///     The draft these settings describe.
    /// </summary>
    /// <param name="visibilityWhenUnsaid">
    ///     The profile's own preferred visibility, used when <c>--visibility</c> does not say — or
    ///     <see langword="null" /> to leave the choice to the account's setting on the instance.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     These settings describe no draft at all. Unreachable through the command app, which calls
    ///     <see cref="Validate" /> first; said out loud rather than silently publishing something else.
    /// </exception>
    public PostDraft ToDraft(PostVisibility? visibilityWhenUnsaid) =>
        TryCompose(visibilityWhenUnsaid, out var draft, out var problem)
            ? draft
            : throw new InvalidOperationException($"These settings describe no post to publish: {problem}");

    public override ValidationResult Validate()
    {
        var shared = base.Validate();

        if (!shared.Successful)
        {
            return shared;
        }

        // Every rule is asked by composing the draft, rather than checked over again here: two lists of rules is how
        // the parser comes to accept something the composer then cannot build.
        return TryCompose(visibilityWhenUnsaid: null, out _, out var problem)
            ? ValidationResult.Success()
            : ValidationResult.Error(problem);
    }

    /// <summary>
    ///     Composes the draft these settings describe, or says what is wrong with them. One method rather than a parse
    ///     and a matching set of checks, so that what the argument parser turns down and what the command could not have
    ///     built are the same thing by construction.
    /// </summary>
    private bool TryCompose(
        PostVisibility? visibilityWhenUnsaid,
        [NotNullWhen(true)] out PostDraft? draft,
        [NotNullWhen(false)] out string? problem)
    {
        draft = null;

        if (!TryChooseAudience(visibilityWhenUnsaid, out var audience, out problem))
        {
            return false;
        }

        foreach (var value in Media)
        {
            if (!MediaOption.IsWellFormed(value))
            {
                problem = MediaOption.Rejection(value);

                return false;
            }
        }

        var poll = ComposePoll(out problem);

        if (problem is not null)
        {
            return false;
        }

        var composed = new PostDraft
        {
            Text = Text,

            // An empty --cw is a flag somebody passed and left blank, which is not a warning to put a post behind —
            // the same reading a field being typed into gets, said in the one place both surfaces read (#146). The
            // third state this surface has is untouched by that: it is --cw being absent from the command line
            // altogether, which is a fact about the invocation rather than about what was written in the option.
            ContentWarning = ContentWarnings.Written(ContentWarning),
            Visibility = audience.Visibility,
            VisibilityChosen = audience.Chosen,
            InReplyTo = InReplyTo,
            Media = Media.Select(MediaOption.Parse).ToList(),
            Poll = poll,
        };

        problem = composed.Problem;

        if (problem is not null)
        {
            return false;
        }

        draft = composed;

        return true;
    }

    /// <summary>
    ///     The poll these settings describe, or <see langword="null" /> if they describe none — which is not the same as
    ///     describing a broken one, hence <paramref name="problem" /> rather than a null for both.
    /// </summary>
    private PollDraft? ComposePoll(out string? problem)
    {
        problem = null;

        if (PollAnswers.Length == 0)
        {
            // Options that only mean something alongside --poll, passed without it. Ignoring them would be the silence
            // ADR-0006 turned strict parsing on to stop: the user asked for something and got a post with no poll.
            if (PollOpenFor is not null || PollMultipleChoice)
            {
                problem = "--poll-open and --poll-multiple describe a poll, so pass --poll <ANSWER> as well.";
            }

            return null;
        }

        var openFor = DefaultPollOpenFor;

        if (PollOpenFor is not null)
        {
            if (DurationOption.Parse(PollOpenFor) is not { } given)
            {
                problem = DurationOption.Rejection(PollOpenFor);

                return null;
            }

            openFor = given;
        }

        problem = PollDraft.Problem(PollAnswers, openFor);

        return problem is null ? PollDraft.Of(PollAnswers, openFor, PollMultipleChoice) : null;
    }
}
