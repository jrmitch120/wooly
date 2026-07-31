using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Clears the unread mark on one conversation. Nothing is asked first: a conversation marked read by mistake costs
///     a user an indicator, and everything said in it is still there to be read.
/// </summary>
internal sealed class DirectMessageReadCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IDirectMessages messages) : AsyncCommand<SingleConversationSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SingleConversationSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var conversation = await messages.MarkRead(profile, settings.ConversationId, cancellationToken);

        if (settings.Json)
        {
            ConversationJson.WriteConversation(console, conversation);
        }
        else
        {
            ConversationReport.MarkedRead(console, conversation);
        }

        return (int)ExitCode.Success;
    }
}
