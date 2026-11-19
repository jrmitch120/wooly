using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     <c>post translate</c> driven the way a user drives it: whole commands through the real command app, over a real
///     config file and token store in a scratch directory, with the post faked at <see cref="IPostEngagement" /> and its
///     translation at <see cref="IPostTranslation" /> — ADR-0005's primary seam (#614).
/// </summary>
public class PostTranslateCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakePostEngagement _posts = FakePostEngagement.Answering();
    private FakePostTranslation _translations = FakePostTranslation.Translating();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    ///     The post laid out as <c>post show</c> lays it out, its words the instance's translation of them, and a line
    ///     saying what it was translated from and into and by whom.
    /// </summary>
    [Fact]
    public void Translate_PrintsThePostTranslatedAndSaysWhereTheTranslationCameFrom()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(content: "Hallo Welt"));
        _translations = FakePostTranslation.Translating(ATranslation.With(content: "Hello world"));

        var run = Run(["post", "translate", "110"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        Assert.Contains("jeff@mastodon.social", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.DoesNotContain("Hallo Welt", run.Output);
        Assert.Contains("translated from German into English by DeepL.com", run.Output);
        Assert.Contains("3 boosts, 5 favorites, 1 reply", run.Output);
        Assert.Contains("https://mastodon.social/@jeff/110", run.Output);

        var asked = Assert.Single(_translations.Requests);
        Assert.Equal("personal", asked.Profile);
        Assert.Equal("110", asked.PostId);
    }

    /// <summary>
    ///     The whole post's words are the translation's — the warning, the poll's answers and what each attachment shows
    ///     with the text — and none of the original is left beside them.
    /// </summary>
    [Fact]
    public void Translate_TranslatesTheWarningThePollAndTheAttachmentsWithTheText()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            content: "Hallo Welt",
            contentWarning: "Katzen",
            media: [APost.APicture(id: "m1", description: "Ein Schaf")],
            poll: APost.APoll(
                options: [APost.AnAnswer("Ja", 4), APost.AnAnswer("Nein", 6)],
                id: "7")));
        _translations = FakePostTranslation.Translating(ATranslation.With(
            content: "Hello world",
            contentWarning: "Cats",
            pollId: "7",
            pollOptions: ["Yes", "No"],
            descriptions: new Dictionary<string, string> { ["m1"] = "A sheep" }));

        var output = Run(["post", "translate", "110"]).Output;

        Assert.Contains("content warning: Cats", output);
        Assert.Contains("A sheep", output);
        Assert.Contains("Yes", output);
        Assert.Contains("No", output);

        foreach (var original in new[] { "Hallo Welt", "Katzen", "Ein Schaf", "Ja", "Nein" })
        {
            Assert.DoesNotContain(original, output);
        }
    }

    /// <summary>
    ///     Said directly under the translated text, before what hangs off the post — where the TUI says it too — so a
    ///     reader knows which words are the translator's.
    /// </summary>
    [Fact]
    public void Translate_SaysWhereTheTranslationCameFromUnderTheTextBeforeWhatIsAttached()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(media: [APost.APicture()]));

        var output = Run(["post", "translate", "110"]).Output;

        var text = output.IndexOf("Hello world, translated", StringComparison.Ordinal);
        var provenance = output.IndexOf("translated from German", StringComparison.Ordinal);
        var attached = output.IndexOf("https://files.mastodon.social/m1/original.png", StringComparison.Ordinal);

        Assert.True(text >= 0 && text < provenance && provenance < attached, output);
    }

    /// <summary>
    ///     The same post document every command writes, with the translated words in its fields and a
    ///     <c>translation</c> object — so a script can tell a translation from the original, and see which languages and
    ///     translator made it.
    /// </summary>
    [Fact]
    public void Translate_WritesThePostDocumentTranslatedWithWhereTheTranslationCameFrom()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            content: "Hallo Welt",
            contentWarning: "Katzen",
            media: [APost.APicture(id: "m1", description: "Ein Schaf")]));
        _translations = FakePostTranslation.Translating(ATranslation.With(
            content: "Hello world",
            contentWarning: "Cats",
            descriptions: new Dictionary<string, string> { ["m1"] = "A sheep" },
            from: "de",
            to: "en",
            provider: "DeepL.com"));

        var run = Run(["post", "translate", "110", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var post = JsonDocument.Parse(run.Output).RootElement;

        Assert.Equal("110", post.GetProperty("id").GetString());
        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal("Cats", post.GetProperty("contentWarning").GetString());
        Assert.Equal("A sheep", post.GetProperty("media")[0].GetProperty("description").GetString());

        var translation = post.GetProperty("translation");
        Assert.Equal("de", translation.GetProperty("from").GetString());
        Assert.Equal("en", translation.GetProperty("to").GetString());
        Assert.Equal("DeepL.com", translation.GetProperty("provider").GetString());
    }

    /// <summary>
    ///     A boost carries no words of its own, so the instance is asked for the boosted post's translation, by the
    ///     boosted post's id — and the boost is shown as one, of the post translated.
    /// </summary>
    [Fact]
    public void Translate_TranslatesABoostThroughTheBoostedPostsId()
    {
        AddProfile();
        _posts = FakePostEngagement.Answering(APost.With(
            id: "555",
            account: "sam@hachyderm.io",
            boosted: APost.With(id: "110", content: "Hallo Welt")));
        _translations = FakePostTranslation.Translating(ATranslation.With(content: "Hello world"));

        var run = Run(["post", "translate", "555"]);

        Assert.Equal("110", Assert.Single(_translations.Requests).PostId);
        Assert.Contains("sam@hachyderm.io boosted jeff@mastodon.social", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.DoesNotContain("Hallo Welt", run.Output);

        var json = JsonDocument.Parse(Run(["post", "translate", "555", "--json"]).Output).RootElement;
        Assert.Equal("555", json.GetProperty("id").GetString());
        Assert.Equal("Hello world", json.GetProperty("boosted").GetProperty("content").GetString());
        Assert.Equal("DeepL.com", json.GetProperty("translation").GetProperty("provider").GetString());
    }

    /// <summary>
    ///     With neither <c>--to</c> nor <c>translate_to</c> no target is sent at all, and the instance chooses — the
    ///     account's web-interface language, which no API exposes.
    /// </summary>
    [Fact]
    public void Translate_SendsNoTargetWhereNeitherTheCommandLineNorTheConfigFileSaysOne()
    {
        AddProfile();

        Run(["post", "translate", "110"]);

        Assert.Null(Assert.Single(_translations.Requests).Target);
    }

    /// <summary>
    ///     <c>--to</c> takes a language as <c>--language</c> does — its code, its English name or its own — and sends the
    ///     code it names.
    /// </summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("French")]
    [InlineData("Français")]
    public void Translate_SendsTheLanguageToNamesAsItsCode(string written)
    {
        AddProfile();

        Run(["post", "translate", "110", "--to", written]);

        Assert.Equal("fr", Assert.Single(_translations.Requests).Target);
    }

    /// <summary>A language it cannot read is the command line's mistake, turned down before anything is asked.</summary>
    [Fact]
    public void Translate_ReportsAToItCannotReadAsAUsageError()
    {
        AddProfile();

        var run = Run(["post", "translate", "110", "--to", "Klingon"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("Klingon", run.ErrorOutput);
        Assert.Empty(_translations.Requests);
        Assert.Empty(_posts.Reads);
    }

    /// <summary>The reader's <c>translate_to</c> is the target where the command line names none.</summary>
    [Fact]
    public void Translate_FallsBackToTheLanguageTheConfigFileTranslatesTo()
    {
        AddProfile();
        PreferTranslateTo("German");

        Run(["post", "translate", "110"]);

        Assert.Equal("de", Assert.Single(_translations.Requests).Target);
    }

    /// <summary><c>--to</c> is for this one command, and wins over <c>translate_to</c>.</summary>
    [Fact]
    public void Translate_LetsToOverrideTheLanguageTheConfigFileTranslatesTo()
    {
        AddProfile();
        PreferTranslateTo("de");

        Run(["post", "translate", "110", "--to", "fr"]);

        Assert.Equal("fr", Assert.Single(_translations.Requests).Target);
    }

    /// <summary>
    ///     A <c>translate_to</c> naming no language is the config file's mistake, said naming the file — as a
    ///     <c>default_language</c> naming none is — rather than quietly letting the instance choose.
    /// </summary>
    [Fact]
    public void Translate_ReportsATranslateToItCannotReadAsAConfigurationError()
    {
        AddProfile();
        PreferTranslateTo("Klingon");

        var run = Run(["post", "translate", "110"]);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("Klingon", run.ErrorOutput);
        Assert.Contains("config.toml", run.ErrorOutput);
        Assert.Empty(_translations.Requests);
    }

    /// <summary>
    ///     The instance honours a target only where it is one of its own interface locales, and quietly uses the
    ///     account's otherwise — so the line names the language it actually used, not the one asked for.
    /// </summary>
    [Fact]
    public void Translate_NamesTheLanguageTheInstanceUsedRatherThanTheOneAskedFor()
    {
        AddProfile();
        _translations = FakePostTranslation.Translating(ATranslation.With(from: "ja", to: "en-GB"));

        var run = Run(["post", "translate", "110", "--to", "Welsh"]);

        Assert.Contains("translated from Japanese into English by DeepL.com", run.Output);
    }

    /// <summary>
    ///     A refusal — a followers-only or direct post, a pair the translator does not take, no translator — is the
    ///     instance's to explain, in its own words, on stderr with the general failure's exit code, and nothing is
    ///     written to stdout.
    /// </summary>
    [Fact]
    public void Translate_ReportsARefusalInTheInstancesOwnWords()
    {
        AddProfile();
        _translations = FakePostTranslation.Refusing("This action is not allowed");

        var run = Run(["post", "translate", "110"]);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("This action is not allowed", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    /// <summary>A translator out of quota or rate-limited fails the command in the instance's words.</summary>
    [Fact]
    public void Translate_ReportsATranslatorThatFailedInTheInstancesOwnWords()
    {
        AddProfile();
        _translations = FakePostTranslation.Failing("There have been too many requests to the translation service recently.");

        var run = Run(["post", "translate", "110"]);

        Assert.Equal((int)ExitCode.Error, run.ExitCode);
        Assert.Contains("There have been too many requests to the translation service recently.", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    /// <summary>The instance's own rate limit is ADR-0006's: its own exit code, so a script can wait it out.</summary>
    [Fact]
    public void Translate_ReportsARateLimitWithTheRateLimitedExitCode()
    {
        AddProfile();
        _translations = FakePostTranslation.Refusing(
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero)));

        var run = Run(["post", "translate", "110"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
    }

    [Fact]
    public void Translate_AsksAsTheProfileNamedByTheOverride()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        Run(["post", "translate", "110", "--profile", "work"]);

        var asked = Assert.Single(_translations.Requests);
        Assert.Equal("work", asked.Profile);
        Assert.Equal("token-work", asked.Token);
    }

    /// <summary>A post not translated is not one: <c>post show --json</c> writes no <c>translation</c> at all.</summary>
    [Fact]
    public void Show_WritesNoTranslationOnAPostAsWritten()
    {
        AddProfile();

        var post = JsonDocument.Parse(Run(["post", "show", "110", "--json"]).Output).RootElement;

        Assert.False(post.TryGetProperty("translation", out _));
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    /// <summary>Writes the reader's language as they would by hand, there being no command that sets it.</summary>
    private void PreferTranslateTo(string language) =>
        File.AppendAllText(
            new WoolyPaths(_directory.Path).ConfigFile,
            $"{Environment.NewLine}[preferences]{Environment.NewLine}translate_to = \"{language}\"{Environment.NewLine}");

    private CommandRun Run(string[] args)
    {
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IPostEngagement>(_posts);
            services.AddSingleton<IPostTranslation>(_translations);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
