using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A post's poll as part of what its <em>content warning</em> covers: nothing drawn and nothing votable until the
///     reader has asked past it with <c>x</c> (#119). A poll's answers are words its author wrote, so it stands behind
///     the warning text on exactly the terms <see cref="PostLines" />'s body already does — and behind the instance's
///     sensitive flag on none of them, that flag being a mark over media.
/// </summary>
/// <remarks>
///     The other half of the gate — that the poll reads as it always did once it is on screen — is
///     <see cref="PostPollLinesTests" />', which is where every question about what a poll's rows say already lives.
///     What <c>x</c> itself answers to is <see cref="ScreenRevealTests" />'.
/// </remarks>
public class WarnedPollTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The first acceptance criterion: no options, no bar, no counts and no closing time.</summary>
    [Fact]
    public void Feed_DrawsNothingOfAWarnedPostsPoll()
    {
        var lines = PostLines.Feed(Polled("spoilers"), 61, default, Now);

        Assert.DoesNotContain(lines, line => line.Has(Role.Poll));

        foreach (var said in new[] { "Cats", "Dogs", "▓", "░", "votes", "Closes", "Choose as many" })
        {
            Assert.DoesNotContain(lines, line => line.Text.Contains(said, StringComparison.Ordinal));
        }
    }

    /// <summary>
    ///     The second acceptance criterion: asked past, the poll reads exactly as the same poll reads on a post with
    ///     nothing written over it — bars, counts, closing time and all. Said as the two being the same rows rather
    ///     than as a list of what each says, because "unchanged" is the whole claim.
    /// </summary>
    [Fact]
    public void Feed_ReadsExactlyAsAnUnwarnedPostsPollOnceTheReaderHasAskedPastTheWarning()
    {
        var revealed = PostLines.Feed(Polled("spoilers"), 61, new Reading(Revealed: true), Now);
        var plain = PostLines.Feed(Polled(contentWarning: null), 61, default, Now);

        Assert.Equal(PollRows(plain), PollRows(revealed));
        Assert.NotEmpty(PollRows(revealed));
    }

    /// <summary>The post screen honours it the same way, since both draw their poll down the one path.</summary>
    [Fact]
    public void Whole_HidesAWarnedPostsPollAndShowsItOnceAsked()
    {
        var post = Polled("spoilers");
        var hidden = PostLines.Whole(post, 61, default, Now);

        Assert.DoesNotContain(hidden, line => line.Has(Role.Poll));
        Assert.DoesNotContain(hidden, line => line.Text.Contains("votes", StringComparison.Ordinal));

        Assert.Equal(
            PollRows(PostLines.Whole(Polled(contentWarning: null), 61, default, Now)),
            PollRows(PostLines.Whole(post, 61, new Reading(Revealed: true), Now)));
    }

    /// <summary>
    ///     The third acceptance criterion: the sensitive flag alone hides no poll. Mastodon marks media with it, and a
    ///     poll's answers are words the author typed — the same reason a sensitive post's own text is on screen.
    /// </summary>
    /// <param name="attached">
    ///     Whether the post carries a picture too, which is what makes the flag mean anything at all
    ///     (<see cref="Post.IsWarned" />). Either way the poll is drawn, and either way the picture behind the flag is
    ///     not — the two halves hide different things.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Feed_DrawsThePollOfASensitivePostWithNothingWrittenOverIt(bool attached)
    {
        var lines = PostLines.Feed(
            APost.With(sensitive: true, poll: APost.APoll(), media: attached ? [APost.APicture()] : []),
            61,
            default,
            Now,
            FakePictures.With());

        Assert.Contains(lines, line => line.Has(Role.Poll));
        Assert.Contains(lines, line => line.Text == "10 votes");
    }

    /// <summary>
    ///     The fifth acceptance criterion: nothing stands where the poll was. The warning over the text is already up
    ///     and already naming <c>x</c>, and a second prompt under it would be the same offer made twice — the rule
    ///     <see cref="PostLines" /> follows for a warned post's attachments (#113).
    /// </summary>
    [Fact]
    public void Feed_LeavesTheWarningAlreadyUpToAskForThePoll()
    {
        var lines = PostLines.Feed(Polled("spoilers"), 61, default, Now);

        Assert.Equal(1, lines.Count(line => line.Text.Contains("x  show it", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, line => line.Text.Contains("poll", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     The fourth acceptance criterion: a poll nobody has been shown is not a poll to announce <c>v</c> and the
    ///     digits for, by the rule <see cref="PostKeys" /> states in the other direction — a key that acts on nothing
    ///     here must not be on the row. Both come back the moment the reader asks past the warning.
    /// </summary>
    [Fact]
    public void Keys_SayNothingAboutVotingInAPollBehindAWarning()
    {
        var screen = Feed(Polled("spoilers"));

        Assert.Null(screen.Poll);
        Assert.DoesNotContain(screen.Keys, key => key.Key == "1-0");
        Assert.DoesNotContain(screen.Keys, key => key.Key == "v");

        Assert.True(screen.Reveal());

        Assert.NotNull(screen.Poll);
        Assert.Contains(screen.Keys, key => key.Key == "1-0");
        Assert.Contains(screen.Keys, key => key.Key == "v");
    }

    /// <summary>
    ///     And the digits do nothing while it is hidden: a reader cannot fill in a ballot on a post they have not been
    ///     shown, and a key reported as used is a shell claiming to have acted.
    /// </summary>
    [Fact]
    public void Toggle_ChoosesNothingInAPollBehindAWarning()
    {
        var screen = Feed(Polled("spoilers"));

        Assert.False(screen.Toggle(0));
        Assert.Empty(screen.Chosen);

        screen.Reveal();

        Assert.True(screen.Toggle(0));
        Assert.Equal([0], screen.Chosen);
    }

    /// <summary>
    ///     The keys of a sensitive post's poll are on the row, since the poll itself is: what is announced follows what
    ///     is drawn, on both halves of the question.
    /// </summary>
    [Fact]
    public void Keys_SayHowToVoteInThePollOfASensitivePostCarryingAPicture()
    {
        var screen = Feed(APost.With(sensitive: true, poll: APost.APoll(), media: [APost.APicture()]));

        Assert.NotNull(screen.Poll);
        Assert.Contains(screen.Keys, key => key.Key == "v");
    }

    /// <summary>
    ///     A boost is asked about by the post inside it, since that is the post whose author wrote the warning and
    ///     whose poll a vote would be cast in — the same rule every other mark follows.
    /// </summary>
    [Fact]
    public void Feed_HidesThePollOfAWarnedPostInsideABoost()
    {
        var boost = APost.With(id: "1", content: string.Empty, boosted: Polled("spoilers"));

        Assert.DoesNotContain(PostLines.Feed(boost, 61, default, Now), line => line.Has(Role.Poll));

        var screen = Feed(boost);

        Assert.Null(screen.Poll);
        Assert.True(screen.Reveal());
        Assert.NotNull(screen.Poll);
    }

    /// <summary>A post carrying a poll and, where one is asked for, a warning written over the words it is among.</summary>
    private static Post Polled(string? contentWarning) => APost.With(
        contentWarning: contentWarning,
        poll: APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10,
            expiresAt: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)));

    /// <summary>
    ///     The rows a poll draws: its own, and the muted ones underneath saying when it closes and how many have voted.
    ///     The same slice <see cref="PostPollLinesTests.FeedAndWhole_DrawTheSamePollRows" /> takes, and for the same
    ///     reason — a post with nothing on it but a poll has no other muted row.
    /// </summary>
    private static IEnumerable<string> PollRows(IEnumerable<Line> lines) =>
        lines.Where(line => line.Has(Role.Poll) || line.Role == Role.Muted).Select(line => line.Text);

    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "Home"), posts);
}
