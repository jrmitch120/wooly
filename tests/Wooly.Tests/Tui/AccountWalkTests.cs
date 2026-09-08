using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The account screen's header block as the first thing a reader walks (#179, ADR-0019): what the pick opens on,
///     what <c>←</c> and <c>→</c> reach inside a bio and a custom field, what <c>⏎</c> opens there, and which keys go
///     quiet while what is picked out is not a post at all.
/// </summary>
/// <remarks>
///     Asked of the screen where the question is what the screen holds and draws, and of the shell where it is a
///     decision — which screen to push, whether a mark reaches an instance. No terminal in the room either way, which
///     is what <see cref="Screen" /> is shaped for.
/// </remarks>
public class AccountWalkTests
{
    /// <summary>A bio with one of each kind of reference in it, in the order they are written.</summary>
    private const string Bio = "Cat photographer. Notes at https://maria.example/notes, ask @jon@fosstodon.org #cats";

    /// <summary>The three the header carries that are walked, in the order they are drawn — the mention is not one.</summary>
    private static readonly string[] Walked =
    [
        "https://maria.example/notes",
        "#cats",
        "https://maria.example",
    ];

    /// <summary>Somebody who wrote all of that about themselves, with one custom field carrying an address.</summary>
    private static Account Maria() => AnAccount.With(
        address: "maria@example.social",
        author: "Maria Example",
        bio: Bio,
        fields:
        [
            AnAccount.Field("Web", "https://maria.example", AShell.Now),
            AnAccount.Field("Pronouns", "she/her"),
        ]);

    /// <summary>Their account screen, with two posts of theirs under the header block.</summary>
    private static AccountScreen Opened() =>
        new(Maria(), [APost.With(id: "110"), APost.With(id: "220")], pinned: []);

    /// <summary>The rows that screen draws at the width the contract is written for.</summary>
    private static IReadOnlyList<Line> Drawn(Screen screen) => screen.Lines(new Drawing(61, AShell.Now));

    /// <summary>
    ///     The status row with room for every key on it, since what is being asked here is which keys the screen
    ///     announces rather than which of them survive the cut at the right of an 80-column row.
    /// </summary>
    private static string Status(Screen screen) =>
        ChromeLines.Status(screen.Keys, notice: null, noticeIsError: false, asking: null, 200).Text;

    /// <summary>
    ///     The pick opens on the header, not on the first post: the screen is about the person, and landing below them
    ///     would make the bio something you walk back to.
    /// </summary>
    [Fact]
    public void TheScreenOpensWithTheHeaderPickedAndNoPostPickedOut()
    {
        var screen = Opened();

        Assert.Null(screen.Picked);

        screen.Move(1);

        Assert.Equal("110", screen.Picked?.Id);

        screen.Move(-1);

        Assert.Null(screen.Picked);
    }

    /// <summary>And the walk along the screen goes on as it always did once it is past the header.</summary>
    [Fact]
    public void TheHeaderIsOneThingToLandOnAndThePostsFollowIt()
    {
        var screen = Opened();

        screen.Move(1);
        screen.Move(1);

        Assert.Equal("220", screen.Picked?.Id);

        // And it clamps at either end the way every other walk on this shell does.
        screen.Move(4);

        Assert.Equal("220", screen.Picked?.Id);

        screen.Move(int.MinValue);

        Assert.Null(screen.Picked);
    }

    /// <summary>
    ///     The whole block is one thing to land on: the counts line, the bio and the fields are rows of it rather than
    ///     stops of their own, so the screen holds the header and its posts and nothing else.
    /// </summary>
    [Fact]
    public void TheWholeHeaderBlockIsOneThingToLandOn()
    {
        var things = Drawn(Opened())
            .Select(line => line.Item)
            .Where(item => item is not null)
            .Distinct()
            .ToList();

        Assert.Equal([0, 1, 2], things);
    }

    /// <summary>
    ///     <c>esc</c> is up one level of whichever kind of level is open here too: the first press lets a reference in
    ///     the bio go, and only the next one pops the screen.
    /// </summary>
    [Fact]
    public async Task Esc_LetsAReferenceInTheHeaderGoBeforeItPopsTheScreen()
    {
        var (shell, _) = await OnTheAccountScreen();

        shell.WalkReference(1);

        Assert.NotNull(shell.Screen.Reference);

        shell.Back();

        Assert.Null(shell.Screen.Reference);
        Assert.Equal(2, shell.Depth);

        shell.Back();

        Assert.Equal(1, shell.Depth);
    }

    /// <summary>
    ///     <c>→</c> enters at the first reference in the bio and <c>←</c> at the last of everything the block carries,
    ///     which is in a custom field's value.
    /// </summary>
    [Theory]
    [InlineData(true, "https://maria.example/notes")]
    [InlineData(false, "https://maria.example")]
    public void TheArrowsEnterTheHeaderAtOneEndOrTheOther(bool forwards, string expected)
    {
        var screen = Opened();

        Assert.True(screen.WalkReference(forwards ? 1 : -1));
        Assert.Equal(expected, screen.Reference?.Text);
    }

    /// <summary>The whole walk, across the bio and then the fields, in the order the block draws them.</summary>
    [Fact]
    public void TheWalkRunsAcrossTheBioAndThenTheFieldsInDrawOrder()
    {
        var screen = Opened();

        Assert.Equal(Walked, screen.References.Select(reference => reference.Text));
    }

    /// <summary>
    ///     A mention is drawn as one and never walked: <c>⏎</c> resolves a mention off the post that carries it, and a
    ///     bio carries no such list — so a bare handle here would open whoever the reader's own instance has by that
    ///     name.
    /// </summary>
    [Fact]
    public void AMentionInABioIsDrawnAndNeverWalked()
    {
        var screen = Opened();

        Assert.DoesNotContain(screen.References, reference => reference.Role == Role.Mention);

        var spans = Drawn(screen).SelectMany(line => line.Spans).ToList();

        Assert.Contains(spans, span => span.Role == Role.Mention && span.Text == "@jon@fosstodon.org");

        // And no amount of walking reaches it: every stop is one of the three the block offers.
        for (var walked = 0; walked < 6; walked++)
        {
            screen.WalkReference(1);

            Assert.Contains(screen.Reference?.Text, Walked);
        }
    }

    /// <summary>The one picked out is bracketed where it is written, in a bio and in a field's value alike.</summary>
    [Theory]
    [InlineData(1, "‹https://maria.example/notes›")]
    [InlineData(-1, "‹https://maria.example›")]
    public void ThePickedReferenceIsBracketedWhereItIsWritten(int by, string expected)
    {
        var screen = Opened();

        Assert.DoesNotContain(Drawn(screen).SelectMany(line => line.Spans), span => span.Role == Role.ReferencePicked);

        screen.WalkReference(by);

        var lines = Drawn(screen);

        Assert.Contains(lines, line => line.Text.Contains(expected, StringComparison.Ordinal));
        Assert.Equal(2, lines.SelectMany(line => line.Spans).Count(span => span.Role == Role.ReferencePicked));
    }

    /// <summary>
    ///     The header block is the screen's first thing: its rows say they are part of it, they carry the mark that
    ///     says which thing is picked out, and the posts are numbered after it.
    /// </summary>
    [Fact]
    public void TheHeaderIsTheFirstThingOnTheScreenAndCarriesTheSelection()
    {
        var screen = Opened();
        var lines = Drawn(screen);

        Assert.Equal(0, lines[0].Item);
        Assert.True(lines[0].Has(Role.Selection));

        Assert.Contains(lines, line => line.Item == 1);

        screen.Move(1);

        var walked = Drawn(screen);

        Assert.False(walked[0].Has(Role.Selection));
        Assert.Contains(walked, line => line.Item == 1 && line.Has(Role.Selection));
        Assert.DoesNotContain(walked, line => line.Item == 0 && line.Has(Role.Selection));
    }

    /// <summary>
    ///     A post taken down brings the pick back inside what is left, and the screen goes on picking out the thing it
    ///     draws as picked — the header and the posts being numbered together, there is one pick to re-clamp and not
    ///     two.
    /// </summary>
    [Fact]
    public void APostTakenDownLeavesTheScreenPickingOutWhatItDraws()
    {
        var screen = new AccountScreen(Maria(), [APost.With(id: "110"), APost.With(id: "220")], pinned: []);

        screen.Move(2);

        Assert.Equal("220", screen.Picked?.Id);

        screen.Remove("220");

        Assert.Equal("110", screen.Picked?.Id);
        Assert.Contains(Drawn(screen), line => line.Item == 1 && line.Has(Role.Selection));

        // And with the last of them gone there is only the header left to stand on.
        screen.Remove("110");

        Assert.Null(screen.Picked);
        Assert.True(Drawn(screen)[0].Has(Role.Selection));
    }

    /// <summary>
    ///     Walking off the header lets its reference pick go, the same way walking off a post does — the reader has
    ///     left the thing it was inside.
    /// </summary>
    [Fact]
    public void WalkingOnToAPostLetsTheHeadersPickGoAndWalksThePostInstead()
    {
        var screen = Opened();

        screen.WalkReference(1);

        Assert.Equal("https://maria.example/notes", screen.Reference?.Text);

        screen.Move(1);

        Assert.Null(screen.Reference);
        Assert.Equal(APost.With().Content, screen.Picked?.Content);
    }

    /// <summary>
    ///     Every key that acts on a post is announced nowhere while the header is picked, because there is none for
    ///     them to act on — the precedent a follow notification set, read off the status row itself here. <c>c</c> is
    ///     the one of them that stays: a fresh post is written from anywhere (#179, widened by #193).
    /// </summary>
    [Fact]
    public void TheKeysThatActOnAPostGoQuietWhileTheHeaderIsPicked()
    {
        var screen = Opened();
        var onTheHeader = Status(screen);

        foreach (var key in PostKeys.OnAPost.Where(key => key != PostKeys.Composing))
        {
            Assert.DoesNotContain(key.ToString(), onTheHeader);
        }

        // The screen's own keys are untouched: a tie is with the person, and this is their screen either way.
        Assert.Contains("F:follow", onTheHeader);
        Assert.Contains("j/k:post", onTheHeader);
        Assert.Contains("c:compose", onTheHeader);

        screen.Move(1);

        var onAPost = Status(screen);

        foreach (var key in PostKeys.OnAPost)
        {
            Assert.Contains(key.ToString(), onAPost);
        }
    }

    /// <summary>And pressing one does nothing at all: nothing is asked of the instance and nothing is asked of the reader.</summary>
    [Theory]
    [InlineData(ShellKey.B)]
    [InlineData(ShellKey.F)]
    [InlineData(ShellKey.D)]
    [InlineData(ShellKey.R)]
    public async Task ThoseFourKeysAnswerNothingWhileTheHeaderIsPicked(ShellKey key)
    {
        var (shell, built) = await OnTheAccountScreen();

        shell.Press(key);

        built.Host.Drain();

        Assert.Empty(built.Engagement.Marks);
        Assert.Null(shell.Asking);
        Assert.IsType<AccountScreen>(shell.Screen);
    }

    /// <summary>A hashtag in a bio opens its timeline, exactly as one inside a post does.</summary>
    [Fact]
    public async Task Enter_OpensAHashtagPickedInABio()
    {
        var (shell, built) = await OnTheAccountScreen();

        shell.WalkReference(1);
        shell.WalkReference(1);

        Assert.Equal("#cats", shell.Screen.Reference?.Text);

        await shell.OpenReference();

        built.Host.Drain();

        Assert.Equal("cats", built.Timelines.Reads[^1].Timeline.Hashtag);
        Assert.Equal("home › @maria@example.social › #cats", shell.Breadcrumb);
    }

    /// <summary>And the address on a verified field opens in the platform's browser, through the path a link already takes.</summary>
    [Fact]
    public async Task Enter_OpensAnAddressPickedInAFieldInTheBrowser()
    {
        var (shell, built) = await OnTheAccountScreen();

        shell.WalkReference(-1);

        await shell.OpenReference();

        built.Host.Drain();

        Assert.Equal("https://maria.example/", Assert.Single(built.Browser.Opened).AbsoluteUri);
        Assert.Equal(2, shell.Depth);
    }

    /// <summary>A shell standing on Maria's account screen, with the header picked as an arrival leaves it.</summary>
    private static async Task<(Wooly.Tui.Shell.Shell Shell, AShell Built)> OnTheAccountScreen()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "maria@example.social")),
            Accounts = FakeAccountRelationships.Holding(Maria()),
        };

        var shell = await built.Opened();

        await shell.OpenAuthor();

        built.Host.Drain();

        return (shell, built);
    }
}
