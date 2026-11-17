using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Shows one post as its instance translates it — the instance being the only translator there is (ADR-0037) — laid
///     out as <see cref="PostShowCommand" /> lays it out, with a line saying where the translation came from (#614).
/// </summary>
/// <remarks>
///     Nothing is checked before asking: no languages map is read, and a refusal — a followers-only or direct post, a
///     pair the translator does not take, no translator — is the instance's to say, and an error under ADR-0006.
/// </remarks>
internal sealed class PostTranslateCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IPostEngagement posts,
    IPostTranslation translations,
    IConfigStore config)
    : AsyncCommand<PostTranslateCommand.Settings>
{
    internal sealed class Settings : SinglePostSettings
    {
        [CommandOption("--to <LANGUAGE>")]
        [Description(
            "The language to translate into, as a code (fr), its English name (French) or its own name (Français). "
            + "Defaults to translate_to in the config file, else your instance decides.")]
        public string? To { get; init; }

        /// <summary>
        ///     The code <see cref="To" /> names, or <see langword="null" /> where it was not given. A word that names no
        ///     language cannot reach here — <see cref="Validate" /> has already turned the invocation down.
        /// </summary>
        public string? Target => PostLanguageName.Parse(To)?.Code;

        public override ValidationResult Validate() =>
            To is not null && PostLanguageName.Parse(To) is null
                ? ValidationResult.Error(PostLanguageName.Rejection(To))
                : ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var profile = profiles.Resolve(settings.Profile);
        var post = await posts.Show(profile, settings.PostId, cancellationToken);

        // A boost carries no words of its own, so it is the boosted post that is asked for, never the wrapper. --to for
        // this command, else the reader's translate_to, else nothing, and the instance chooses.
        var translation = await translations.Translate(
            profile,
            (post.Boosted ?? post).Id,
            settings.Target ?? config.Load().Preferences.TranslateTo,
            cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, PostDocument.Translated(post, translation));
        }
        else
        {
            PostReport.Translated(console, post, translation);
        }

        return (int)ExitCode.Success;
    }
}
