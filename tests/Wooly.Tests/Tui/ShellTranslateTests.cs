using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>t</c> on a picked post: the instance's <b>Translation</b> shown in place of the original, and the original
///     back on the second press (#615, ADR-0037). Driven without a terminal through <see cref="FakePostTranslation" />,
///     as the reveal beside it is (<see cref="ScreenRevealTests" />).
/// </summary>
public class ShellTranslateTests
{
    private static readonly Post German = APost.With(id: "110", content: "Hallo Welt") with { Language = "de" };

    /// <summary>
    ///     <c>t</c> shows the post translated, with a muted line directly under the words saying from what, into what and
    ///     by whom; <c>t</c> again shows it as written.
    /// </summary>
    [Fact]
    public async Task Translate_ShowsThePostTranslatedAndTheSecondPressShowsItAsWritten()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(German),
            Translation = FakePostTranslation.Translating(ATranslation.With(content: "Hello world")),
        };

        var opened = await shell.Opened();

        Assert.True(opened.Press(ShellKey.T));
        shell.Host.Drain();

        var drawn = Drawn(opened);
        var at = drawn.FindIndex(line => line.Text.Contains("Hello world"));

        Assert.True(at >= 0);
        Assert.DoesNotContain(drawn, line => line.Text.Contains("Hallo Welt"));
        Assert.Equal("Translated from German into English · DeepL.com", drawn[at + 1].Text[1..].Trim());
        Assert.Contains(
            drawn[at + 1].Spans,
            span => span is { Role: Role.Muted, Text: var said } && said.StartsWith("Translated"));

        Assert.True(opened.Press(ShellKey.T));
        shell.Host.Drain();

        drawn = Drawn(opened);

        Assert.Contains(drawn, line => line.Text.Contains("Hallo Welt"));
        Assert.DoesNotContain(drawn, line => line.Text.Contains("Hello world"));
        Assert.DoesNotContain(drawn, line => line.Text.Contains("Translated from"));
    }

    /// <summary>
    ///     <c>t</c> is announced on a post its instance would translate, beside the other post keys, and the shell asks
    ///     the instance for the language the config file names — or for none, where it names none.
    /// </summary>
    [Theory]
    [InlineData(PostVisibility.Public, "en")]
    [InlineData(PostVisibility.Unlisted, null)]
    public async Task Translate_IsOfferedOnAPublicOrUnlistedPostWithWords(PostVisibility visibility, string? target)
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(German with { Visibility = visibility }),
            TranslateTo = target,
        };

        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key is { Key: "t", Does: "translate" });

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        var asked = Assert.Single(shell.Translation.Requests);

        Assert.Equal("110", asked.PostId);
        Assert.Equal(target, asked.Target);
        Assert.Equal("token-personal", asked.Token);
    }

    /// <summary>
    ///     And nowhere it would be refused: a post only followers or the people it mentions can see, or one with no
    ///     words at all — where <c>t</c> is neither announced nor asks anything (ADR-0037).
    /// </summary>
    [Theory]
    [MemberData(nameof(Untranslatable))]
    public async Task Translate_IsNotOfferedWhereTheInstanceWouldRefuseIt(string kind)
    {
        var post = kind switch
        {
            "followers" => German with { Visibility = PostVisibility.Followers },
            "direct" => German with { Visibility = PostVisibility.Direct },
            _ => APost.With(id: "110", content: "", media: [APost.APicture() with { Description = null }]),
        };

        var shell = new AShell { Timelines = FakeTimelineReader.Holding(post) };
        var opened = await shell.Opened();

        Assert.DoesNotContain(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Empty(shell.Translation.Requests);
        Assert.Null(opened.Notice);
    }

    /// <summary>The posts an instance would not translate.</summary>
    public static TheoryData<string> Untranslatable => ["followers", "direct", "wordless"];

    /// <summary>
    ///     A post whose only words are what hangs off it — a poll's answers — has words to translate, and the provenance
    ///     line still goes up where its text would be: there is no text to sit under.
    /// </summary>
    [Fact]
    public async Task Translate_SaysWhereTheTranslationCameFromOnAPostWithNoText()
    {
        var polled = APost.With(
            id: "110",
            content: "",
            poll: APost.APoll(options: [APost.AnAnswer("Katzen", 4), APost.AnAnswer("Hunde", 6)], votes: 10));

        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(polled),
            Translation = FakePostTranslation.Translating(ATranslation.With(
                content: "",
                pollId: polled.Poll!.Id,
                pollOptions: ["Cats", "Dogs"])),
        };

        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        var drawn = Texts(opened);

        Assert.Contains(drawn, line => line.Contains("Cats"));
        Assert.DoesNotContain(drawn, line => line.Contains("Katzen"));
        Assert.Contains(drawn, line => line.Contains("Translated from German into English · DeepL.com"));
    }

    /// <summary>
    ///     A translation answered once is held for the session: <c>t</c> back and forth, and on another screen showing
    ///     the same post, asks the instance nothing more.
    /// </summary>
    [Fact]
    public async Task Translate_AsksTheInstanceOnceForAPostTranslatedBeforeThisSession()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(German) };
        var opened = await shell.Opened();

        for (var press = 0; press < 3; press++)
        {
            opened.Press(ShellKey.T);
            shell.Host.Drain();
        }

        await opened.Enter();
        shell.Host.Drain();

        Assert.IsType<PostScreen>(opened.Screen);

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Single(shell.Translation.Requests);
        Assert.Contains(Texts(opened), line => line.Contains("Hello world, translated"));
    }

    /// <summary>The header spinner turns while the instance is being asked, as for any enquiry.</summary>
    [Fact]
    public async Task Translate_ShowsTheSpinnerWhileTheInstanceIsAsked()
    {
        var held = new TaskCompletionSource<Translation>();
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(German),
            Translation = FakePostTranslation.Awaiting(held.Task),
        };

        var opened = await shell.Opened();

        var translating = opened.Translate();

        shell.Host.Drain();

        Assert.True(opened.Fetching);

        held.SetResult(ATranslation.With(content: "Hello world"));
        await translating;
        shell.Host.Drain();

        Assert.False(opened.Fetching);
        Assert.Contains(Texts(opened), line => line.Contains("Hello world"));
    }

    /// <summary>
    ///     A warned post translated stays hidden: its warning is drawn in the reader's language and what it holds back
    ///     is still held back until <c>x</c>, which then shows the text translated (ADR-0037).
    /// </summary>
    [Fact]
    public async Task Translate_LeavesAWarnedPostHiddenWithItsWarningTranslated()
    {
        var warned = German with { ContentWarning = "Spoiler zum Film" };
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(warned),
            Translation = FakePostTranslation.Translating(ATranslation.With(
                content: "Hello world",
                contentWarning: "Movie spoilers")),
        };

        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        var drawn = Texts(opened);

        Assert.Contains(drawn, line => line.Contains("Movie spoilers"));
        Assert.DoesNotContain(drawn, line => line.Contains("Spoiler zum Film"));
        Assert.DoesNotContain(drawn, line => line.Contains("Hello world") || line.Contains("Hallo Welt"));
        Assert.Contains(drawn, line => line.Contains("x  show it"));
        Assert.Contains(drawn, line => line.Contains("Translated from German"));

        opened.Press(ShellKey.X);

        Assert.Contains(Texts(opened), line => line.Contains("Hello world"));
    }

    /// <summary>A boost carries no words of its own, so <c>t</c> on one asks for the boosted post's translation.</summary>
    [Fact]
    public async Task Translate_TranslatesThePostInsideABoost()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "555", content: "", boosted: German)),
            Translation = FakePostTranslation.Translating(ATranslation.With(content: "Hello world")),
        };

        var opened = await shell.Opened();

        Assert.Contains(opened.Keys, key => key.Key == "t");

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Equal("110", Assert.Single(shell.Translation.Requests).PostId);
        Assert.Contains(Texts(opened), line => line.Contains("Hello world"));
        Assert.DoesNotContain(Texts(opened), line => line.Contains("Hallo Welt"));
    }

    /// <summary>
    ///     A refusal, or a translator that failed to answer, is a notice in the instance's own words — and the post
    ///     stays as written.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Translate_SaysWhatTheInstanceSaidAndLeavesThePostAsWritten(bool failing)
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(German),
            Translation = failing
                ? FakePostTranslation.Failing("Translation quota exceeded")
                : FakePostTranslation.Refusing("Translation is not available for this post"),
        };

        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Contains(failing ? "quota exceeded" : "not available for this post", opened.Notice);
        Assert.True(opened.NoticeIsError);
        Assert.Contains(Texts(opened), line => line.Contains("Hallo Welt"));
        Assert.DoesNotContain(Texts(opened), line => line.Contains("Translated"));
    }

    /// <summary>
    ///     A mark puts a fresh copy of the post on the screen and changes none of its words, so the translation stands —
    ///     as a reveal does (<see cref="ScreenRevealTests.Reveal_StandsWhenAMarkReplacesThePostInPlace" />).
    /// </summary>
    [Fact]
    public async Task Translate_StandsWhenAMarkReplacesThePostInPlace()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(German),
            Engagement = FakePostEngagement.Answering(German with { Marks = APost.Marked(favorited: true) }),
        };

        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();

        Assert.True(opened.Screen.Picked?.Marks.Favorited);
        Assert.Contains(Texts(opened), line => line.Contains("Hello world, translated"));
    }

    /// <summary>
    ///     An edit makes the translation of what it replaced untrue: the edited post shows as written, and <c>t</c> on it
    ///     asks the instance anew rather than showing the translation held for the words it no longer says.
    /// </summary>
    [Fact]
    public async Task Translate_LetsGoOfThePostsTranslationWhenItIsEdited()
    {
        var mine = German with { IsMine = true };
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(mine),
            Author = FakePostAuthor.Answering(mine),
        };

        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        shell.Author.Edited = mine with { Content = "Hallo schöne Welt" };

        opened.Press(ShellKey.E);
        ((ComposeScreen)opened.Screen).Text += " schöne";

        await opened.Drafting.Send();
        shell.Host.Drain();

        var drawn = Texts(opened);

        Assert.Contains(drawn, line => line.Contains("Hallo schöne Welt"));
        Assert.DoesNotContain(drawn, line => line.Contains("Translated"));

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Equal(2, shell.Translation.Requests.Count);
    }

    /// <summary>
    ///     What shows translated belongs to the screen it was asked on, as a reveal does (#121): <c>esc</c> out of a post
    ///     screen and in again finds it as written, while the screen walked back out to still shows what it showed.
    /// </summary>
    [Fact]
    public async Task Translate_LapsesWithTheScreenItWasAskedOnWhenThatScreenIsPopped()
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(German) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        await opened.Enter();
        shell.Host.Drain();

        Assert.Contains(Texts(opened), line => line.Contains("Hallo Welt"));

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        opened.Back();

        Assert.Contains(Texts(opened), line => line.Contains("Hello world, translated"));

        await opened.Enter();
        shell.Host.Drain();

        Assert.Contains(Texts(opened), line => line.Contains("Hallo Welt"));
        Assert.DoesNotContain(Texts(opened), line => line.Contains("Translated"));
    }

    /// <summary>A refresh and an arrival each put up a screen that has been asked nothing, so the post shows as written.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Translate_LapsesWhenTheScreenIsRefreshedOrArrivedAtAgain(bool arriving)
    {
        var shell = new AShell { Timelines = FakeTimelineReader.Holding(German) };
        var opened = await shell.Opened();

        opened.Press(ShellKey.T);
        shell.Host.Drain();

        Assert.Contains(Texts(opened), line => line.Contains("Hello world, translated"));

        if (arriving)
        {
            opened.Arrive(1);
            shell.Host.Drain();
            opened.Arrive(0);
            shell.Host.Drain();
        }
        else
        {
            await opened.Refresh();
            shell.Host.Drain();
        }

        Assert.Contains(Texts(opened), line => line.Contains("Hallo Welt"));
        Assert.DoesNotContain(Texts(opened), line => line.Contains("Translated"));
    }

    /// <summary>The rows the screen in front draws at 61 columns, past the gutter.</summary>
    private static IReadOnlyList<string> Texts(Wooly.Tui.Shell.Shell opened) => AShell.Drawn(opened.Screen);

    /// <summary>What the screen in front draws at 61 columns.</summary>
    private static List<Line> Drawn(Wooly.Tui.Shell.Shell opened) =>
        [.. opened.Screen.Lines(new Drawing(61, AShell.Now))];
}
