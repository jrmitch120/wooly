using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a post says about itself before it says anything else: who wrote it, over two rows with their avatar
///     beside both; what it answers, on the <c>↳</c> row above that; and the blank that turns the three counts into a
///     footer rather than one more line of the body (#62, #63, #77).
/// </summary>
/// <remarks>
///     Held against <see cref="PostLines" /> rather than against a screen, which is the point of the shape living
///     there: the feed, the post screen, a conversation, search, direct messages and an account all share these rows,
///     and a test per screen would be six chances for them to drift apart.
/// </remarks>
public class PostBylineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    private const string Avatar = "https://files.mastodon.social/avatars/maria.png";

    /// <summary>A post by somebody whose name and handle are easy to tell apart from anything else on the row.</summary>
    private static Post By(string account = "maria@fosstodon.org", string author = "Maria Ochoa", string? avatar = null) =>
        APost.With(account: account, author: author, avatarUrl: avatar);

    /// <summary>The post's rows as a feed shows them.</summary>
    private static IReadOnlyList<Line> Feed(Post post, int width = 61, IPictures? pictures = null) =>
        PostLines.Feed(post, width, revealed: false, Now, pictures);

    /// <summary>Which row the first one <paramref name="which" /> picks out is, so a test can say "the row under it".</summary>
    private static int Row(IReadOnlyList<Line> lines, Func<Line, bool> which)
    {
        for (var at = 0; at < lines.Count; at++)
        {
            if (which(lines[at]))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>
    ///     The byline is two rows, not one: the name and the audience/age tail on the first, the handle on the second.
    ///     One thin row was the whole of what said where a post began (#62).
    /// </summary>
    [Fact]
    public void Feed_SplitsTheBylineAcrossTwoRows()
    {
        var lines = Feed(By());

        var name = lines.First(line => line.Has(Role.BylineName));
        var handle = lines.First(line => line.Has(Role.BylineHandle));

        Assert.Contains(name.Spans, span => span is { Role: Role.BylineName, Text: "Maria Ochoa" });
        Assert.Contains(name.Spans, span => span.Role == Role.Audience);
        Assert.DoesNotContain(name.Spans, span => span.Role == Role.BylineHandle);

        Assert.Equal("@maria@fosstodon.org", handle.Text.Trim());
        Assert.Equal(Row(lines, line => line.Has(Role.BylineName)) + 1, Row(lines, line => line.Has(Role.BylineHandle)));
    }

    /// <summary>The blank the two-row shape wants between the byline and the body, so the two do not run together.</summary>
    [Fact]
    public void Feed_LeavesABlankRowBetweenTheBylineAndTheBody()
    {
        var lines = Feed(By() with { Content = "The office cat has opinions." });

        var handle = Row(lines, line => line.Has(Role.BylineHandle));

        Assert.Empty(lines[handle + 1].Spans);
        Assert.Contains("The office cat", lines[handle + 2].Text);
    }

    /// <summary>
    ///     The avatar gets a box four columns wide standing beside both byline rows, drawn through
    ///     <see cref="IPictures" /> the same way an attachment is — and body, media and counts stay full width.
    /// </summary>
    [Fact]
    public void Feed_GivesTheAvatarAFourColumnBoxBesideBothBylineRows()
    {
        var lines = Feed(
            By(avatar: Avatar),
            pictures: FakePictures.With().HoldingAvatarOf("maria@fosstodon.org"));

        var box = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal(4, box.Columns);
        Assert.Equal(2, box.Rows);
        Assert.Equal(0, box.Column);
        Assert.Equal("avatar:maria@fosstodon.org", box.Drawn.Id);

        var name = lines.First(line => line.Has(Role.BylineName));
        var handle = lines.First(line => line.Has(Role.BylineHandle));

        Assert.StartsWith("     ", name.Text, StringComparison.Ordinal);
        Assert.StartsWith("     ", handle.Text, StringComparison.Ordinal);
        // The name row is padded out so the audience/age tail sits hard right; the handle row takes only what it needs.
        Assert.Equal(61, name.Width);
        Assert.True(handle.Width <= 61, $"'{handle.Text}' is {handle.Width} columns");
    }

    /// <summary>
    ///     The columns are taken before the pixels land, and the byline says it wants them — so the picture appears
    ///     beside a byline that is already where it will stay, rather than shoving the name five columns sideways.
    /// </summary>
    [Fact]
    public void Feed_ReservesTheAvatarColumnsWhileThePixelsAreStillOnTheirWay()
    {
        var pictures = FakePictures.With();
        var lines = Feed(By(avatar: Avatar), pictures: pictures);

        Assert.Empty(lines.SelectMany(line => line.Insets));

        var name = lines.First(line => line.Has(Role.BylineName));

        Assert.StartsWith("     ", name.Text, StringComparison.Ordinal);
        Assert.Equal("avatar:maria@fosstodon.org", name.Wants?.Id);
    }

    /// <summary>
    ///     No avatar, no columns. A terminal offering neither sixel nor the Kitty graphics protocol will never draw
    ///     one, and an instance that named none has none to draw — spending five of sixty-one columns on either would
    ///     be spending them on a permanent blank (ADR-0016).
    /// </summary>
    [Fact]
    public void Feed_SpendsNoColumnsOnAnAvatarThatWillNeverBeDrawn()
    {
        var onATerminalThatDrawsNothing = Feed(By(avatar: Avatar), pictures: FakePictures.DrawingNothing());
        var whereTheInstanceNamedNone = Feed(By(), pictures: FakePictures.With());

        foreach (var lines in new[] { onATerminalThatDrawsNothing, whereTheInstanceNamedNone })
        {
            Shape(lines);
        }

        static void Shape(IReadOnlyList<Line> lines)
        {
            var name = lines.First(line => line.Has(Role.BylineName));

            Assert.StartsWith("Maria Ochoa", name.Text, StringComparison.Ordinal);
            Assert.Null(name.Wants);
            Assert.Empty(lines.SelectMany(line => line.Insets));
        }
    }

    /// <summary>
    ///     The picked post's <c>▌</c> sits to the left of the avatar rather than over it — the one collision #62 named
    ///     as a constraint on the whole shape.
    /// </summary>
    [Fact]
    public void Rows_PutTheGutterToTheLeftOfTheAvatar()
    {
        var posts = new Picked<Post>([By(avatar: Avatar)]);
        var pictures = FakePictures.With().HoldingAvatarOf("maria@fosstodon.org");

        var lines = posts.Rows(61, (post, _, room) => PostLines.Feed(post, room, revealed: false, Now, pictures));

        var box = Assert.Single(lines.SelectMany(line => line.Insets));
        var name = lines.First(line => line.Has(Role.BylineName));

        Assert.Equal(1, box.Column);
        Assert.Equal("▌", name.Spans[0].Text);
        Assert.Equal(Role.Selection, name.Spans[0].Role);
        Assert.Equal(61, name.Width);
    }

    /// <summary>
    ///     A blank row ahead of the counts, so the three marks read as a footer rather than as one more line of the
    ///     post — the amendment reacting to the prototype turned up (#62).
    /// </summary>
    [Fact]
    public void Feed_GivesTheCountsABlankRowOfTheirOwn()
    {
        var lines = Feed(By());

        var counts = lines.Count - 1;

        Assert.Contains("↺ 3", lines[counts].Text);
        Assert.Empty(lines[counts - 1].Spans);
    }

    /// <summary>Whole draws the same avatar beside the same two rows, so a post reads the same drilled into.</summary>
    [Fact]
    public void Whole_DrawsTheAvatarBesideItsOwnTwoBylineRows()
    {
        var lines = PostLines.Whole(
            By(avatar: Avatar),
            61,
            revealed: false,
            Now,
            FakePictures.With().HoldingAvatarOf("maria@fosstodon.org"));

        var box = Assert.Single(lines.SelectMany(line => line.Insets));

        Assert.Equal(4, box.Columns);
        Assert.Equal(2, box.Rows);
        Assert.StartsWith("     Maria Ochoa", lines.First(line => line.Has(Role.BylineName)).Text, StringComparison.Ordinal);
        Assert.StartsWith("     @maria", lines.First(line => line.Has(Role.BylineHandle)).Text, StringComparison.Ordinal);

        // The exact moment is stepped in with the two rows above it, though the box beside it has run out: three rows
        // starting in one column and a fourth starting in another would read as two things.
        Assert.StartsWith("     29 Jul", lines[Row(lines, line => line.Has(Role.BylineHandle)) + 1].Text, StringComparison.Ordinal);
    }

    /// <summary>A boost draws the boosted author's avatar, because the boosted post is the one being read.</summary>
    [Fact]
    public void Feed_DrawsTheAvatarOfWhoeverWroteWhatIsBeingRead()
    {
        var lines = Feed(
            APost.With(account: "jeff@mastodon.social", boosted: By(avatar: Avatar)),
            pictures: FakePictures.With().HoldingAvatarOf("maria@fosstodon.org"));

        Assert.Equal("avatar:maria@fosstodon.org", Assert.Single(lines.SelectMany(line => line.Insets)).Drawn.Id);
    }

    /// <summary>
    ///     What a reply answers, said on the row above the byline — named off the post's own mentions, so it costs no
    ///     fetch (#63). A self-reply is somebody continuing their own thread; an answered account the post does not
    ///     name is all a bare <c>↳ reply</c> can honestly say.
    /// </summary>
    [Theory]
    [InlineData("jeff@mastodon.social", "↳ answering @jeff@mastodon.social")]
    [InlineData("maria@fosstodon.org", "↳ continuing")]
    [InlineData(null, "↳ reply")]
    public void Feed_SaysWhatAReplyAnswersAboveTheByline(string? answered, string expected)
    {
        var lines = Feed(By() with { InReplyTo = new PostReplyTarget { PostId = "99", Handle = answered } });

        Assert.Equal(expected, lines[0].Text);
        Assert.Equal(Role.Muted, lines[0].Role);
        Assert.True(lines[1].Has(Role.BylineName));
    }

    /// <summary>A post answering nothing says nothing, rather than a row saying so.</summary>
    [Fact]
    public void Feed_SaysNothingAboveTheBylineOfAPostThatAnswersNothing()
    {
        Assert.True(Feed(By())[0].Has(Role.BylineName));
    }

    /// <summary>
    ///     Both at once: the boost row first, then the reply mark, then the byline — the order #63 settled, so the
    ///     two rows above a byline are always in the same two places.
    /// </summary>
    [Fact]
    public void Feed_PutsTheBoostRowAheadOfTheReplyMark()
    {
        var lines = Feed(
            APost.With(
                author: "Jeff",
                boosted: By() with { InReplyTo = new PostReplyTarget { PostId = "99", Handle = "sam@hachyderm.io" } }));

        Assert.Equal("↺ Jeff boosted", lines[0].Text);
        Assert.Equal("↳ answering @sam@hachyderm.io", lines[1].Text);
        Assert.True(lines[2].Has(Role.BylineName));
    }

    /// <summary>
    ///     The mark is drawn identically wherever the rows are shared — the feed, the post screen, a conversation,
    ///     search, direct messages and an account all read it off the same two methods, and neither suppresses it.
    /// </summary>
    [Fact]
    public void FeedAndWhole_DrawTheSameReplyMark()
    {
        var post = By() with { InReplyTo = new PostReplyTarget { PostId = "99", Handle = "sam@hachyderm.io" } };

        var feed = Feed(post);
        var whole = PostLines.Whole(post, 61, revealed: false, Now);

        Assert.Equal("↳ answering @sam@hachyderm.io", feed[0].Text);
        Assert.Equal(feed[0].Text, whole[0].Text);
        Assert.Equal(feed[0].Role, whole[0].Role);
    }

    /// <summary>A mark too long for the room is cut rather than run past it, like every other row.</summary>
    [Fact]
    public void Feed_KeepsTheReplyMarkInsideTheRoomItWasGiven()
    {
        var lines = Feed(
            By() with
            {
                InReplyTo = new PostReplyTarget
                {
                    PostId = "99",
                    Handle = "somebody@an-extremely-long-instance-domain.example",
                },
            },
            20);

        Assert.All(lines, line => Assert.True(line.Width <= 20, $"'{line.Text}' is {line.Width} columns"));
        Assert.StartsWith("↳ answering", lines[0].Text, StringComparison.Ordinal);
    }

    /// <summary>Nothing a byline draws runs past the room it was given, avatar and all.</summary>
    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(61)]
    public void Feed_KeepsTheBylineInsideTheRoomItWasGiven(int width)
    {
        var lines = Feed(
            By(
                account: "somebody@an-extremely-long-instance-domain.example",
                author: "Somebody With A Very Long Display Name Indeed",
                avatar: Avatar),
            width,
            FakePictures.With().HoldingAvatarOf("somebody@an-extremely-long-instance-domain.example"));

        Assert.All(lines, line => Assert.True(line.Width <= width, $"'{line.Text}' is {line.Width} columns"));
        Assert.All(
            lines.SelectMany(line => line.Insets),
            box => Assert.True(box.Column + box.Columns <= width, "the avatar box runs past the room"));
    }
}
