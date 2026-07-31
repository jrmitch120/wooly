using System.ComponentModel;
using Spectre.Console.Cli;

namespace Wooly.Cli.Commands;

/// <summary>
///     What every command that names one conversation takes: the conversation's id, and whether the answer is for a
///     person or for another program.
/// </summary>
/// <remarks>
///     The id is the conversation's own, never that of a post in it — a distinction worth stating in the help text,
///     because a user looking at a printed thread has both kinds of id in front of them and only one of them works.
/// </remarks>
internal class SingleConversationSettings : ProfileScopedSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("The id of the conversation, as shown by dm list. Not the id of a post in it.")]
    public string ConversationId { get; init; } = string.Empty;

    [CommandOption("--json")]
    [Description("Write the conversation as JSON, for another program to read.")]
    public bool Json { get; init; }
}
