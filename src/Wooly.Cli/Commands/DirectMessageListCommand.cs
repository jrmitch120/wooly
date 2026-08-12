using Spectre.Console;
using Wooly.Cli.Output;
using Wooly.Core.Conversations;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Lists the direct conversations the profile is in, most recently spoken in first: who each is with, whether
///     anything in it is unread, and the last thing said.
/// </summary>
internal sealed class DirectMessageListCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IDirectMessages messages) : PagedListCommand<DirectMessageListCommand.Settings, Conversation>(profiles)
{
    protected override PagedList<Conversation> Listing(ActiveProfile profile, Settings settings) =>
        new(
            token => messages.List(profile, settings.Limit, token),
            fetch => ConversationJson.Write(console, fetch),
            fetch => ConversationReport.Write(console, fetch));

    internal sealed class Settings : PagedListSettings
    {
        /// <inheritdoc />
        protected override string Counted => "conversation";
    }
}
