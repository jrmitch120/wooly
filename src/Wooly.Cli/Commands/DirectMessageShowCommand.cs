using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Shows one conversation in full: everything said in it, oldest first. The posts read exactly as they do anywhere
///     else in this client, because they are written by <see cref="PostReport.Write" /> — a direct message is a post,
///     and one shown in a thread should not look like a different kind of thing from one shown on a timeline.
///     <para>
///         Reading a conversation does not mark it read; <c>dm read</c> is what does that. Clearing the mark for
///         somebody who only wanted to look would leave them no way to find their way back to it.
///     </para>
/// </summary>
internal sealed class DirectMessageShowCommand(
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
        var thread = await messages.Show(profile, settings.ConversationId, cancellationToken);

        if (settings.Json)
        {
            ConversationJson.WriteThread(console, thread);
        }
        else
        {
            ConversationReport.WriteThread(console, thread);
        }

        return (int)ExitCode.Success;
    }
}
