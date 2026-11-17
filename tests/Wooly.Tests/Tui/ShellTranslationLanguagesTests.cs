using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>t</c> offered only where the instance would translate (#616, ADR-0037): its languages map, read once per
///     instance per session, has to take the post's language — <c>und</c> for a post with none — to the reader's
///     <c>translate_to</c>, compared by base language. Driven without a terminal, the map scripted on
///     <see cref="FakePostTranslation" />.
/// </summary>
public class ShellTranslationLanguagesTests
{
    /// <summary>
    ///     Where the map takes the post's language to <c>translate_to</c> — by base language either side, and with
    ///     <c>und</c> standing for a post that says none — <c>t</c> is on the status row and asks the instance.
    /// </summary>
    [Theory]
    [MemberData(nameof(Taken))]
    public async Task Translate_IsOfferedWhereTheMapTakesThePostsLanguageToTheReaders(
        string? language,
        TranslationLanguages map,
        string target)
    {
        var shell = Reading(Post(language), map, target);
        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Equal(target, Assert.Single(shell.Translation.Requests).Target);
    }

    /// <summary>The pairs a map takes: plain, a region on either side, and a post with no language.</summary>
    public static TheoryData<string?, TranslationLanguages, string> Taken => new()
    {
        { "de", FakePostTranslation.Map(("de", ["en"])), "en" },
        { "de", FakePostTranslation.Map(("de", ["en-GB", "pt-BR"])), "en" },
        { "pt-BR", FakePostTranslation.Map(("pt", ["en"])), "en" },
        { null, FakePostTranslation.Map(("und", ["en"])), "en" },
    };

    /// <summary>
    ///     And nowhere it does not: a pair the map has no line for, a post with no language on an instance whose map has
    ///     no <c>und</c>, and a post already in the reader's language — however the map spells its regions.
    /// </summary>
    [Theory]
    [MemberData(nameof(Untaken))]
    public async Task Translate_IsNotOfferedWhereTheMapDoesNotTakeThePostsLanguageToTheReaders(
        string? language,
        TranslationLanguages map,
        string target)
    {
        var shell = Reading(Post(language), map, target);
        var opened = await shell.Opened();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Empty(shell.Translation.Requests);
        Assert.Null(opened.Notice);
    }

    /// <summary>The pairs a map does not take.</summary>
    public static TheoryData<string?, TranslationLanguages, string> Untaken => new()
    {
        { "de", FakePostTranslation.Map(("de", ["fr"])), "en" },
        { "ja", FakePostTranslation.Map(("de", ["en"])), "en" },
        { null, FakePostTranslation.Map(("de", ["en"])), "en" },
        { "en", FakePostTranslation.Map(("en", ["en-GB", "de"])), "en" },
    };

    /// <summary>
    ///     With no <c>translate_to</c> the instance chooses the language, which no API says, so the language check is
    ///     dropped: any public or unlisted post with words is offered, in whatever language — or none — the map lists.
    /// </summary>
    [Theory]
    [InlineData("ja")]
    [InlineData("en")]
    [InlineData(null)]
    public async Task Translate_IsOfferedOnEveryPostWithWordsWhereNoTargetIsSet(string? language)
    {
        var shell = Reading(Post(language), FakePostTranslation.Map(("de", ["en"])), target: null);
        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Null(Assert.Single(shell.Translation.Requests).Target);
    }

    /// <summary>
    ///     An instance with no translator answers an empty map — and one older than 4.2, with no such endpoint, is read
    ///     as one (<see cref="IPostTranslation.Languages" />) — which offers <c>t</c> nowhere, target or none, on the
    ///     status row or in <c>?</c>.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData(null)]
    public async Task Translate_IsOfferedNowhereByAnInstanceWithNoTranslator(string? target)
    {
        var shell = Reading(Post("de"), TranslationLanguages.None, target);
        var opened = await shell.Opened();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Empty(shell.Translation.Requests);

        opened.Press(ShellKey.Question);

        Assert.IsType<HelpScreen>(opened.Screen);
        Assert.DoesNotContain(AShell.Drawn(opened.Screen), line => line.Contains("translate"));
    }

    /// <summary><c>?</c> lists <c>t</c> where the map takes the post, as the status row does.</summary>
    [Fact]
    public async Task Translate_IsListedInTheKeysWhereTheMapTakesThePost()
    {
        var shell = Reading(Post("de"), FakePostTranslation.Map(("de", ["en"])), "en");
        var opened = await shell.Opened();

        opened.Press(ShellKey.Question);

        Assert.Contains(AShell.Drawn(opened.Screen), line => line.Contains("translate"));
    }

    /// <summary>
    ///     An instance that failed to answer its map is taken to translate nothing — never offered on a guess — and the
    ///     shell carries on as ever.
    /// </summary>
    [Fact]
    public async Task Translate_IsOfferedNowhereWhereTheMapWasNotAnswered()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(Post("de")),
            Translation = FakePostTranslation.Translating().Taking(
                Task.FromException<TranslationLanguages>(new Wooly.Core.Errors.InstanceFailedException(
                    "mastodon.social",
                    System.Net.HttpStatusCode.ServiceUnavailable))),
        };

        var opened = await shell.Opened();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "t");
        Assert.Contains(AShell.Drawn(opened.Screen), line => line.Contains("Hallo Welt"));
    }

    /// <summary>
    ///     Until the map lands nothing is offered, and its landing is drawn: <c>t</c> comes onto the row then, without a
    ///     key pressed.
    /// </summary>
    [Fact]
    public async Task Translate_IsOfferedOnceTheMapLands()
    {
        var map = new TaskCompletionSource<TranslationLanguages>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(Post("de")),
            Translation = FakePostTranslation.Translating().Taking(map.Task),
            TranslateTo = "en",
        };

        var opened = await shell.Opened();
        var changed = 0;
        opened.Changed += () => changed++;

        Assert.DoesNotContain(opened.Keys, key => key.Key == "t");

        map.SetResult(FakePostTranslation.Map(("de", ["en"])));
        await Task.Yield();
        shell.Host.Drain();

        Assert.True(changed > 0);
        Assert.Contains(opened.Keys, key => key.Key == "t");
    }

    /// <summary>
    ///     The map is read once per instance for the session: not again for another post, a screen drilled into, a
    ///     refresh, an arrival or a translation — nor for another profile on the same instance. A profile on another
    ///     instance has its own read, as that profile.
    /// </summary>
    [Fact]
    public async Task Languages_AreReadOncePerInstanceNotPerPostOrPerScreen()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                Post("de"),
                APost.With(id: "111", content: "Bonjour") with { Language = "fr" }),
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("other", "mastodon.social", "jeffrey@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            TranslateTo = "en",
        };

        var opened = await shell.Opened();

        Assert.Equal(("personal", "token-personal"), Assert.Single(shell.Translation.LanguageReads));

        _ = opened.Keys;
        opened.Move(1);
        _ = opened.Keys;
        opened.Move(-1);
        opened.Press(ShellKey.T);
        shell.Host.Drain();
        await opened.Enter();
        shell.Host.Drain();
        _ = opened.Keys;
        opened.Back();
        await opened.Refresh();
        shell.Host.Drain();
        opened.Arrive(1);
        shell.Host.Drain();
        opened.Arrive(0);
        shell.Host.Drain();
        _ = opened.Keys;

        Assert.Single(shell.Translation.LanguageReads);

        SwitchTo(shell, opened, "other");

        Assert.Single(shell.Translation.LanguageReads);

        SwitchTo(shell, opened, "work");

        Assert.Equal(
            [("personal", "token-personal"), ("work", "token-work")],
            shell.Translation.LanguageReads);
    }

    /// <summary>Switches to <paramref name="name" /> on the profiles screen, and lets everything it asked for land.</summary>
    private static void SwitchTo(AShell shell, Wooly.Tui.Shell.Shell opened, string name)
    {
        opened.Press(ShellKey.CtrlP);
        Assert.IsType<ProfilesScreen>(opened.Screen).Pick(name);
        Assert.True(opened.Press(ShellKey.Enter));
        shell.Host.Drain();
    }

    /// <summary>A public post with words, written in <paramref name="language" /> — or saying none.</summary>
    private static Post Post(string? language) =>
        APost.With(id: "110", content: "Hallo Welt") with { Language = language };

    /// <summary>A shell whose home holds <paramref name="post" />, on an instance whose map is <paramref name="map" />.</summary>
    private static AShell Reading(Post post, TranslationLanguages map, string? target) => new()
    {
        Timelines = FakeTimelineReader.Holding(post),
        Translation = FakePostTranslation.Translating().Taking(map),
        TranslateTo = target,
    };
}
