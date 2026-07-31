using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Publishes a post answering another. Everything about composing it is <see cref="PostComposeCommand{TSettings}" />'s,
///     which is the point: a reply offers what a post offers because it is the same command with one more argument.
/// </summary>
internal sealed class PostReplyCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IConfigStore config,
    IPostAuthor posts) : PostComposeCommand<PostReplyCommand.Settings>(console, profiles, config, posts)
{
    internal sealed class Settings : PostPublishSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the post to answer, as shown by a timeline.")]
        public string PostId { get; init; } = string.Empty;

        [CommandArgument(1, "<TEXT>")]
        [Description("What the reply says. May be empty only for a reply that is nothing but attached files.")]
        public string PostText { get; init; } = string.Empty;

        /// <inheritdoc />
        public override string Text => PostText;

        /// <inheritdoc />
        public override string? InReplyTo => PostId;
    }
}
