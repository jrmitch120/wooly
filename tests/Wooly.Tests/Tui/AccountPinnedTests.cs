using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pinned posts as a run of their own on the account screen (#182, ADR-0019): the heading over them, the timeline
///     underneath, what <c>[</c> and <c>]</c> do about the two, and what a read the instance refused draws instead.
/// </summary>
/// <remarks>
///     Asked of the screen where the question is what it holds and draws, and of the shell where it is a decision —
///     which calls an arrival makes, in what order, and which run a post that came back twice is left in. No terminal
///     in the room either way.
/// </remarks>
public class AccountPinnedTests
{
    /// <summary>Whoever the account screen is about in these tests.</summary>
    private static readonly string Whose = "ben@hachyderm.io";

    /// <summary>Their screen, with <paramref name="pinned" /> above <paramref name="posts" />.</summary>
    private static AccountScreen Opened(IReadOnlyList<Post>? pinned, params Post[] posts) =>
        new(AnAccount.With(address: Whose), posts, pinned);

    private static IReadOnlyList<Line> Drawn(Screen screen) => screen.Lines(new Drawing(61, AShell.Now));

    /// <summary>What the rows head their runs with, in the order they are drawn.</summary>
    private static IEnumerable<string> Headings(Screen screen) =>
        Drawn(screen).Where(line => line.Heads).Select(line => line.Text);

    /// <summary>
    ///     The shape the screen draws: the pinned run under a counted heading, then the timeline under the uncounted
    ///     one it has always had.
    /// </summary>
    [Fact]
    public void AnAccountWithPinsDrawsThePinnedRunAboveTheirPosts()
    {
        var screen = Opened([APost.With(id: "10"), APost.With(id: "20")], APost.With(id: "30"));

        Assert.Equal(["── 2 pinned ──", "── their posts ──"], Headings(screen));

        // One list of three, in the order the runs are drawn — the pinned ones first, whatever their dates.
        Assert.Equal(["10", "20", "30"], screen.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     The rows of both runs are numbered as the one walk the screen has, so <c>j</c> from the header lands on the
    ///     first pinned post and walks on into the timeline without a seam.
    /// </summary>
    [Fact]
    public void BothRunsAreOneWalkNumberedAfterTheHeaderBlock()
    {
        var screen = Opened([APost.With(id: "10")], APost.With(id: "30"));

        screen.Move(1);

        Assert.Equal("10", screen.Picked?.Id);

        screen.Move(1);

        Assert.Equal("30", screen.Picked?.Id);

        Assert.Equal([0, 1, 2], Drawn(screen).Select(line => line.Item).Where(item => item is not null).Distinct());
    }

    /// <summary>
    ///     Nothing pinned is the common case, and it costs the screen nothing at all: no heading, no rows and no gap
    ///     where the run would have been.
    /// </summary>
    [Fact]
    public void AnAccountWithNoPinsDrawsNoPinnedHeadingAndNoGap()
    {
        var screen = Opened([], APost.With(id: "30"));

        Assert.Equal(["── their posts ──"], Headings(screen));
        Assert.DoesNotContain(Drawn(screen), line => line.Text.Contains("pinned", StringComparison.Ordinal));

        // The same rows a screen that has never heard of a pinned run draws — nothing is left behind.
        Assert.Equal(
            Drawn(new AccountScreen(AnAccount.With(address: Whose), [APost.With(id: "30")], pinned: []))
                .Select(line => line.Text),
            Drawn(screen).Select(line => line.Text));
    }

    /// <summary>
    ///     A read the instance refused draws a row where the section would be, rather than the nothing that would say
    ///     they have pinned none — the distinction <c>Standing not asked for.</c> already draws.
    /// </summary>
    [Fact]
    public void APinnedReadThatWasNeverAnsweredSaysSoWhereTheSectionWouldBe()
    {
        var drawn = Drawn(Opened(pinned: null, APost.With(id: "30"))).Select(line => line.Text).ToList();

        Assert.Contains("Pinned posts not asked for.", drawn);

        // Where the section would be: above the heading over their posts, not among them.
        Assert.True(
            drawn.IndexOf("Pinned posts not asked for.") < drawn.FindIndex(text => text == "── their posts ──"));
    }

    /// <summary>And an empty run is not an unasked one: nothing pinned says nothing at all.</summary>
    [Fact]
    public void AnEmptyPinnedRunSaysNothingAboutNotBeingAsked() =>
        Assert.DoesNotContain(
            Drawn(Opened([], APost.With(id: "30"))),
            line => line.Text.Contains("Pinned posts not asked for.", StringComparison.Ordinal));

    /// <summary>
    ///     <c>]</c> from the header block reaches the pinned run and then the timeline; <c>[</c> comes back and clamps
    ///     at the first pinned post, the header block having no heading to bring with it.
    /// </summary>
    [Fact]
    public void SectionKeysMoveBetweenTheTwoRunsAndClampAtTheHeader()
    {
        var screen = Opened([APost.With(id: "10"), APost.With(id: "20")], APost.With(id: "30"));

        Assert.Equal(1, Sections.Along(Drawn(screen), by: 1, reclaiming: null));

        screen.Pick(1);

        Assert.Equal(3, Sections.Along(Drawn(screen), by: 1, reclaiming: null));
        Assert.Null(Sections.Along(Drawn(screen), by: -1, reclaiming: null));

        screen.Pick(3);

        Assert.Equal(1, Sections.Along(Drawn(screen), by: -1, reclaiming: null));
        Assert.Null(Sections.Along(Drawn(screen), by: 1, reclaiming: null));
    }

    /// <summary>
    ///     The key is announced where both runs are there to move between, immediately after the key that walks the
    ///     posts — and the movement keys stay together, ahead of the three ties.
    /// </summary>
    [Fact]
    public void TheSectionKeyIsSaidWhereBothRunsHaveSomethingInThem()
    {
        var screen = Opened([APost.With(id: "10")], APost.With(id: "30"));

        Assert.Equal(["j/k", "[/]"], screen.Keys.Take(2).Select(key => key.Key));
        Assert.Equal("section", screen.Keys[1].Does);
        Assert.Equal("F", screen.Keys[2].Key);
    }

    /// <summary>
    ///     And left off the common account screen, which has one run: a key announced where it does nothing reads as
    ///     a shell that missed the press.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSectionKeyIsLeftOffAScreenWithOneRun(bool asked) =>
        Assert.DoesNotContain(
            Opened(asked ? [] : null, APost.With(id: "30")).Keys,
            key => key.Key == "[/]");

    /// <summary>An account with pins and nothing else read is one run too — there is nowhere to jump to.</summary>
    [Fact]
    public void TheSectionKeyIsLeftOffAScreenWhosePinsAreAllItHas() =>
        Assert.DoesNotContain(Opened([APost.With(id: "10")]).Keys, key => key.Key == "[/]");

    /// <summary>
    ///     <c>p</c> moves nothing: an un-pin inside the run leaves the post where it is and takes only its word off,
    ///     and the heading goes on saying what the fetch found.
    /// </summary>
    [Fact]
    public void UnpinningInsideTheRunLeavesThePostWhereItIs()
    {
        var pinned = APost.With(id: "10", marks: APost.Marked(pinned: true));
        var screen = Opened([pinned, APost.With(id: "20", marks: APost.Marked(pinned: true))], APost.With(id: "30"));

        screen.Replace(pinned with { Marks = APost.Marked(pinned: false) });

        Assert.Equal(["10", "20", "30"], screen.Posts.Select(post => post.Id));
        Assert.Equal(["── 2 pinned ──", "── their posts ──"], Headings(screen));
        // The word is on the one row that still carries the mark, and nowhere else — the heading over the run says
        // what the fetch found rather than what is true now.
        Assert.Equal(1, Drawn(screen).Count(line => line.Text.Contains("   pinned", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     The arrival asks for the pinned run, and asks for it before the one row familiar followers decorates: it is
    ///     content, and content takes the better odds against a rate limit.
    /// </summary>
    [Fact]
    public async Task OpeningAnAccountReadsItsPinnedPostsAfterItsTimeline()
    {
        var (fakes, _) = await OnTheAccountScreen(pinned: [APost.With(id: "10", account: Whose)]);

        Assert.Equal(
            [TimelineScope.Home, TimelineScope.Account, TimelineScope.Pinned],
            fakes.Timelines.Reads.Select(read => read.Timeline.Scope));

        Assert.Equal(Whose, fakes.Timelines.Reads[^1].Timeline.Account?.Address.Text);
    }

    /// <summary>
    ///     A post the instance sent in both runs is drawn once, in the pinned run — dropped from the timeline where
    ///     the shell reads them, so the screen is handed two disjoint lists.
    /// </summary>
    [Fact]
    public async Task APostInBothRunsIsDrawnOnceAndInThePinnedRun()
    {
        var (_, shell) = await OnTheAccountScreen(
            pinned: [APost.With(id: "110", account: Whose)],
            posts: [APost.With(id: "110", account: Whose), APost.With(id: "220", account: Whose)]);

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(["110", "220"], screen.Posts.Select(post => post.Id));
        Assert.Equal(["── 1 pinned ──", "── their posts ──"], Headings(screen));
    }

    /// <summary>
    ///     A pinned reply is reachable, though the timeline run leaves replies out — which is the whole reason the run
    ///     is read by naming the account rather than filtered out of the posts already in hand.
    /// </summary>
    [Fact]
    public async Task APinnedReplyIsOnTheScreenThoughTheTimelineLeavesRepliesOut()
    {
        var reply = APost.With(
            id: "440",
            account: Whose,
            inReplyTo: new PostReplyTarget { PostId = "1", Handle = "sam@hachyderm.io" });

        var (_, shell) = await OnTheAccountScreen(pinned: [reply], posts: [APost.With(id: "220", account: Whose)]);

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(["440", "220"], screen.Posts.Select(post => post.Id));
    }

    /// <summary>
    ///     A rate limit that stopped the pinned read leaves the screen saying the question went unput, rather than
    ///     saying they have pinned nothing — and takes neither the account nor its timeline down with it.
    /// </summary>
    [Fact]
    public async Task ARateLimitOnThePinnedReadLeavesTheRestOfTheScreenStanding()
    {
        var (_, shell) = await OnTheAccountScreen(pinned: null, posts: [APost.With(id: "220", account: Whose)]);

        var screen = Assert.IsType<AccountScreen>(shell.Screen);

        Assert.Equal(["220"], screen.Posts.Select(post => post.Id));
        Assert.Contains("Pinned posts not asked for.", Drawn(screen).Select(line => line.Text));
    }

    /// <summary>
    ///     A shell drilled into <see cref="Whose" />'s account screen, the instance answering the pinned run with
    ///     <paramref name="pinned" /> — or refusing it, where that is null.
    /// </summary>
    private static async Task<(AShell Fakes, Shell Shell)> OnTheAccountScreen(
        IReadOnlyList<Post>? pinned,
        IReadOnlyList<Post>? posts = null)
    {
        var fakes = new AShell
        {
            Timelines = FakeTimelineReader.Answering(timeline => timeline.Scope switch
            {
                TimelineScope.Pinned => pinned is null
                    ? Fetch<Post>.StoppedShort(
                        [],
                        new RateLimitedException("hachyderm.io", AShell.Now.AddMinutes(5)))
                    : Fetch<Post>.Complete(pinned),
                TimelineScope.Account => Fetch<Post>.Complete(posts ?? [APost.With(id: "220", account: Whose)]),
                _ => Fetch<Post>.Complete([APost.With(id: "110", account: Whose)]),
            }),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: Whose)),
        };

        var shell = await fakes.Opened();

        await shell.OpenAuthor();

        fakes.Host.Drain();

        return (fakes, shell);
    }
}
