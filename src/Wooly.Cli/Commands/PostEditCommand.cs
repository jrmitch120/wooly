using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Changes what a published post says. Takes far fewer options than composing one, and deliberately: who can see a
///     post and what it replies to are settled when it goes out, and everything this command does not mention is left as
///     it was — the attachments above all, which <see cref="IPostAuthor.Edit" /> carries through.
/// </summary>
internal sealed class PostEditCommand(IAnsiConsole console, IProfileRegistry profiles, IPostAuthor posts)
    : AsyncCommand<PostEditCommand.Settings>
{
    internal sealed class Settings : ProfileScopedSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the post to change, as shown by a timeline.")]
        public string PostId { get; init; } = string.Empty;

        [CommandArgument(1, "<TEXT>")]
        [Description("What the post should now say.")]
        public string Text { get; init; } = string.Empty;

        [CommandOption("--cw <TEXT>")]
        [Description(
            "Change the content warning. Left off, the post keeps whatever warning it had; given empty (--cw \"\"), "
            + "the warning is taken away.")]
        public string? ContentWarning { get; init; }

        [CommandOption("--language <LANGUAGE>")]
        [Description(
            "Change what language the post is in, as a code (fr), its English name (French) or its own name "
            + "(Français). Left off, the post keeps the language it had.")]
        public string? Language { get; init; }

        [CommandOption("--json")]
        [Description("Write the edited post as JSON, for another program to read.")]
        public bool Json { get; init; }

        /// <summary>
        ///     The code of the language the post should now be in, or <see langword="null" /> to leave it as it was. Not
        ///     filled in from <c>default_language</c>: that is a preference about new posts, and fixing a typo should not
        ///     relabel an old one.
        /// </summary>
        public string? LanguageCode => Named?.Code;

        /// <summary>
        ///     The language <see cref="Language" /> spells, read in this one place for both the check and the edit, so
        ///     the two cannot come to read it differently.
        /// </summary>
        private PostLanguage? Named => PostLanguageName.Parse(Language);

        public override ValidationResult Validate()
        {
            var shared = base.Validate();

            if (!shared.Successful)
            {
                return shared;
            }

            return Language is not null && Named is null
                ? ValidationResult.Error(PostLanguageName.Rejection(Language))
                : ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);

        // The three states of --cw, handed on as PostEdit spells them: absent leaves the warning alone, empty takes it
        // away, and anything else replaces it. Nothing is normalised here, because an empty string is the message.
        var edit = new PostEdit
        {
            Text = settings.Text,
            ContentWarning = settings.ContentWarning,
            Language = settings.LanguageCode,
        };

        var edited = await posts.Edit(profile, settings.PostId, edit, cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Of(edited));
        }
        else
        {
            PostReport.Edited(console, edited);
        }

        return (int)ExitCode.Success;
    }
}
