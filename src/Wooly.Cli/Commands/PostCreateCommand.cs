using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Publishes a new post.</summary>
internal sealed class PostCreateCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IConfigStore config,
    IPostAuthor posts) : PostComposeCommand<PostCreateCommand.Settings>(console, profiles, config, posts)
{
    internal sealed class Settings : PostPublishSettings
    {
        [CommandArgument(0, "<TEXT>")]
        [Description("What the post says. May be empty only for a post that is nothing but attached files.")]
        public string PostText { get; init; } = string.Empty;

        /// <inheritdoc />
        public override string Text => PostText;
    }
}
