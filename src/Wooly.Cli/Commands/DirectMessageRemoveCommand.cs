using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Takes one conversation off the profile's own list — removing a conversation (#620). Named <c>remove</c> rather
///     than <c>delete</c> because nothing is deleted: the posts stand, the others in the conversation keep it, and a new
///     message in the thread brings it back (ADR-0013's amendment).
/// </summary>
/// <remarks>
///     It asks first all the same, through the consent <c>post delete</c> asks through: what a mistyped id takes off the
///     list is a conversation that only comes back if somebody writes in it again.
/// </remarks>
internal sealed class DirectMessageRemoveCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IDirectMessages messages) : AsyncCommand<DirectMessageRemoveCommand.Settings>
{
    /// <summary>
    ///     The conversation's id, as <see cref="SingleConversationSettings" /> takes it, without its <c>--json</c>: as for
    ///     <c>post delete</c>, there is nothing to report but that it happened.
    /// </summary>
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the conversation, as shown by dm list. Not the id of a post in it.")]
        public string ConversationId { get; init; } = string.Empty;

        [CommandOption("--yes")]
        [Description("Remove without asking. Implied where there is no terminal to ask at.")]
        public bool Yes { get; init; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        if (!Consent.Given(console, settings.Yes, $"Remove conversation {settings.ConversationId} from your list?"))
        {
            console.MarkupLineInterpolated($"Left conversation [bold]{settings.ConversationId}[/] alone.");

            // Nothing went wrong: the user was asked, and answered.
            return (int)ExitCode.Success;
        }

        await messages.Remove(profile, settings.ConversationId, cancellationToken);

        ConversationReport.Removed(console, settings.ConversationId);

        return (int)ExitCode.Success;
    }
}
