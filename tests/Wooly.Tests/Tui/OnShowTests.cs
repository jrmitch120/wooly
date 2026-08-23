using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The one place a warned post says what it is showing this reader, and the one place the answer is asserted
///     (#145). What each of the two halves hides is <see cref="Post.IsWarned" />'s bargain restated where both the
///     walking side and the drawing side can read it — so the tests here are about the rule rather than about a screen.
/// </summary>
/// <remarks>
///     What the rows themselves look like once the rule has been applied stays where it already was:
///     <see cref="SensitiveMediaTests" />' for a flagged post's attachments, <see cref="WarnedPollTests" />' for a
///     poll, <see cref="ScreenRevealTests" />' for what <c>x</c> answers to.
///     <para>
///         A screen appears in the three tests about the walk and the poll keys, and only there: a walk is something a
///         screen has, so there is nowhere else to watch one from. What each of them expects is said in the case
///         itself rather than asked of the module, which is what keeps the assertion from agreeing with a wrong answer.
///     </para>
/// </remarks>
public class OnShowTests
{
    /// <summary>A post behind neither half of a warning shows everything, with nothing to ask for.</summary>
    [Fact]
    public void Of_ShowsEverythingOnAPostBehindNeitherHalfOfAWarning()
    {
        var show = OnShow.Of(Everything(), revealed: false);

        Assert.True(show.Words);
        Assert.True(show.Media);
        Assert.False(show.Asks);
    }

    /// <summary>A warning its author wrote covers both halves: the words it was written about, and what hangs off them.</summary>
    [Fact]
    public void Of_HidesBothHalvesBehindAWarningItsAuthorWrote()
    {
        var show = OnShow.Of(Everything(contentWarning: "spoilers"), revealed: false);

        Assert.False(show.Words);
        Assert.False(show.Media);
        Assert.True(show.Asks);
    }

    /// <summary>
    ///     The instance's flag is a mark over media: it holds back what hangs off the post and leaves its author's own
    ///     words on screen (#113).
    /// </summary>
    [Fact]
    public void Of_HidesWhatHangsOffASensitivePostAndLeavesItsWordsOnScreen()
    {
        var show = OnShow.Of(Everything(sensitive: true), revealed: false);

        Assert.True(show.Words);
        Assert.False(show.Media);
        Assert.True(show.Asks);
    }

    /// <summary>Asked past, both halves are on screen and there is nothing left for <c>x</c> to offer.</summary>
    [Theory]
    [InlineData("spoilers", false)]
    [InlineData(null, true)]
    [InlineData("spoilers", true)]
    public void Of_ShowsEverythingOnceTheReaderHasAskedPastIt(string? contentWarning, bool sensitive)
    {
        var show = OnShow.Of(Everything(contentWarning, sensitive), revealed: true);

        Assert.True(show.Words);
        Assert.True(show.Media);
        Assert.False(show.Asks);
    }

    /// <summary>
    ///     The flag counts for nothing on a post carrying neither an attachment nor a link preview: there is nothing
    ///     under it to be behind anything, and nothing for a reader to ask past (#113).
    /// </summary>
    [Fact]
    public void Of_CountsTheFlagForNothingOnAPostCarryingNothingBehindIt()
    {
        var show = OnShow.Of(APost.With(sensitive: true), revealed: false);

        Assert.True(show.Words);
        Assert.True(show.Media);
        Assert.False(show.Asks);
    }

    /// <summary>
    ///     A boost is answered for by the post inside it — the post whose author wrote the warning — and that post is
    ///     handed back, so nothing outside has to unwrap it a second time and reach the wrapper's own empty text.
    /// </summary>
    [Fact]
    public void Of_AnswersForThePostInsideABoost()
    {
        var inside = Everything(contentWarning: "spoilers");
        var show = OnShow.Of(APost.With(id: "1", content: string.Empty, boosted: inside), revealed: false);

        Assert.Equal(inside.Id, show.Shown.Id);
        Assert.False(show.Words);
        Assert.False(show.Media);
    }

    /// <summary>
    ///     A poll's answers are words its author typed, so they stand behind the warning text alone: the flag hides a
    ///     flagged post's picture and leaves its poll on screen, votable and announced (#119). Said here in as many
    ///     words because this is the one place the two halves are told apart, and the difference is a real distinction
    ///     rather than a drift.
    /// </summary>
    [Fact]
    public void Words_AreWhatAPollFollows_AndTheFlagHidesNoPoll()
    {
        var post = APost.With(sensitive: true, poll: APost.APoll(), media: [APost.APicture()]);
        var show = OnShow.Of(post, revealed: false);

        Assert.True(show.Words);
        Assert.False(show.Media);

        // The poll is the picked post's while its words are, which is what puts `v` and the digits on the status row.
        Assert.NotNull(Feed(post).Poll);
        Assert.Null(Feed(Everything(contentWarning: "spoilers", poll: APost.APoll())).Poll);
    }

    /// <summary>
    ///     The thing nothing checked before: what is not on show is not in the walk. Asserted against this module
    ///     rather than against a second screen, because the walking side and the drawing side agreeing was exactly the
    ///     question neither of them could put — and a walk more permissive than the drawing steps <c>←</c>/<c>→</c>
    ///     into something the reader was never shown and opens it with <c>⏎</c>.
    /// </summary>
    /// <param name="contentWarning">The warning its author wrote, where there is one.</param>
    /// <param name="sensitive">Whether the instance flagged its media.</param>
    /// <param name="words">Whether the author's words are on show, said here rather than read back off the module.</param>
    /// <param name="media">And whether what hangs off them is.</param>
    [Theory]
    [InlineData(null, false, true, true)]
    [InlineData(null, true, true, false)]
    [InlineData("spoilers", false, false, false)]
    [InlineData("spoilers", true, false, false)]
    public void References_HoldNothingThatIsNotOnShow(string? contentWarning, bool sensitive, bool words, bool media)
    {
        var post = Everything(contentWarning, sensitive);
        var show = OnShow.Of(post, revealed: false);

        // What is on show is stated by the case rather than asked of the module, so that a module answering wrongly
        // fails here instead of taking the walk down with it and agreeing.
        Assert.Equal((words, media), (show.Words, show.Media));

        var references = Feed(post).References;

        Assert.Equal(words, references.Any(reference => reference.Role == Role.Link));
        Assert.Equal(media, references.Any(reference => reference.Role == Role.Media));
    }

    /// <summary>
    ///     And the whole walk comes back the moment the reader asks: the text's address, the video's, and the link
    ///     preview's.
    /// </summary>
    [Fact]
    public void References_HoldEverythingOnceTheReaderHasAskedPastTheWarning()
    {
        var screen = Feed(Everything(contentWarning: "spoilers", sensitive: true));

        Assert.Empty(screen.References);
        Assert.True(screen.Reveal());
        Assert.Equal(3, screen.References.Count);
    }

    /// <summary>
    ///     A post carrying one of each of the things a warning covers: an address in its text, an attachment that is
    ///     walked rather than drawn, and a link preview — so that either half being held back is visible in the walk.
    /// </summary>
    private static Post Everything(string? contentWarning = null, bool sensitive = false, PostPoll? poll = null) =>
        APost.With(
            content: "See https://example.com/sheep",
            contentWarning: contentWarning,
            sensitive: sensitive,
            media: [APost.Attached(MediaKind.Video)],
            poll: poll,
            linkPreview: APost.ALinkPreview());

    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
