using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which role a drawn thing takes. ADR-0014 calls this the only part of rendering that is assertable without a
///     terminal, and the part where a mistake shows up as the wrong thing being emphasised on somebody's screen —
///     a boost of the reader's own drawn as if it were anybody's, an unread count drawn as ordinary text.
/// </summary>
public class RoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>Every role in the contract has a name, and the built-in theme has an answer for it.</summary>
    [Fact]
    public void Theme_AnswersEveryRoleInTheContract()
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RoleName.Of(role)));

            // Would throw if the built-in theme had forgotten one, which is what a role table exists to prevent.
            Themes.Dark.For(role);
            Themes.Plain.For(role);
        }
    }

    /// <summary>A name written in C# is not the key somebody has in their config file, so the two are kept apart.</summary>
    [Theory]
    [InlineData(Role.BylineHandle, "byline-handle")]
    [InlineData(Role.BoostMine, "boost-mine")]
    [InlineData(Role.RailUnread, "rail-unread")]
    [InlineData(Role.QuotaLow, "quota-low")]
    public void RoleName_IsTheNameTheContractUses(Role role, string expected)
    {
        Assert.Equal(expected, RoleName.Of(role));
        Assert.Equal(role, RoleName.For(expected));
    }

    /// <summary>ADR-0014's own example: a boost that is mine is drawn as mine rather than as anybody's.</summary>
    [Fact]
    public void Feed_DrawsAMarkOfTheReadersOwnInItsOwnRole()
    {
        var anybodys = PostLines.Feed(APost.With(), 61, revealed: false, Now);
        var mine = PostLines.Feed(
            APost.With(marks: APost.Marked(boosted: true, favorited: true)),
            61,
            revealed: false,
            Now);

        Assert.Contains(anybodys, line => line.Has(Role.Boost));
        Assert.Contains(anybodys, line => line.Has(Role.Favorite));
        Assert.DoesNotContain(anybodys, line => line.Has(Role.BoostMine));

        Assert.Contains(mine, line => line.Has(Role.BoostMine));
        Assert.Contains(mine, line => line.Has(Role.FavoriteMine));
        Assert.DoesNotContain(mine, line => line.Has(Role.Boost));
    }

    /// <summary>A byline is two different things side by side, and they are two roles rather than one.</summary>
    [Fact]
    public void Feed_DrawsANameAndAHandleInTheirOwnRoles()
    {
        var lines = PostLines.Feed(APost.With(author: "Maria Ochoa", account: "maria@fosstodon.org"), 61, false, Now);
        var byline = lines.First(line => line.Has(Role.BylineName));

        Assert.Contains(byline.Spans, span => span is { Role: Role.BylineName, Text: "Maria Ochoa" });
        Assert.Contains(byline.Spans, span => span.Role == Role.BylineHandle && span.Text.Contains('@'));
        Assert.Contains(byline.Spans, span => span.Role == Role.Audience);
    }

    /// <summary>A warning is a warning whether or not there is colour to draw it in, so it carries its own glyph.</summary>
    [Fact]
    public void Feed_DrawsAContentWarningInItsOwnRoleAndWithItsOwnGlyph()
    {
        var lines = PostLines.Feed(APost.With(contentWarning: "spoilers"), 61, revealed: false, Now);
        var warned = lines.First(line => line.Has(Role.ContentWarning));

        Assert.Contains("⚠", warned.Text);
        Assert.Contains("spoilers", warned.Text);
    }

    /// <summary>Every audience has a glyph, because colour alone says nothing on a terminal that has none.</summary>
    [Theory]
    [InlineData(PostVisibility.Public, "○")]
    [InlineData(PostVisibility.Unlisted, "◌")]
    [InlineData(PostVisibility.Private, "●")]
    [InlineData(PostVisibility.Direct, "✉")]
    public void Feed_MarksWhoCanSeeAPostWithAGlyph(PostVisibility visibility, string glyph)
    {
        var lines = PostLines.Feed(APost.With(visibility: visibility), 61, revealed: false, Now);

        Assert.Contains(lines, line => line.Text.Contains(glyph, StringComparison.Ordinal));
        Assert.Equal(glyph, PostLines.Audience(visibility));
    }

    /// <summary>An attachment nobody described says so, in the muted role, rather than pretending to a description.</summary>
    [Fact]
    public void Feed_DrawsAnAttachmentAndSaysWhereItHasNoDescription()
    {
        var described = PostLines.Feed(
            APost.With(media: [APost.APicture(description: "A cartoon sheep")]),
            61,
            revealed: false,
            Now);

        var undescribed = PostLines.Feed(
            APost.With(media: [APost.APicture(description: null)]),
            61,
            revealed: false,
            Now);

        var first = described.First(line => line.Has(Role.Media));
        Assert.Contains("▒▒▒▒", first.Text);
        Assert.Contains("A cartoon sheep", first.Text);

        var second = undescribed.First(line => line.Has(Role.Media));
        Assert.Contains("undescribed", second.Text);
        Assert.Contains(second.Spans, span => span.Role == Role.Muted);
    }

    /// <summary>The selected row is told apart by a gutter mark as well as by its role.</summary>
    [Fact]
    public void Feed_MarksTheSelectedRowInTheGutter()
    {
        var screen = new FeedScreen(
            new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
            [APost.With(id: "1"), APost.With(id: "2")]);

        var picked = screen.Lines(61, Now).Where(line => line.Has(Role.Selection)).ToList();

        Assert.NotEmpty(picked);
        Assert.All(picked, line => Assert.StartsWith("▌", line.Text, StringComparison.Ordinal));

        screen.Move(1);

        // Exactly one post is picked out at a time, so moving does not leave the last one lit.
        var after = screen.Lines(61, Now).Where(line => line.Has(Role.Selection)).ToList();
        Assert.NotEmpty(after);
        Assert.NotEqual(picked.Count + after.Count, screen.Lines(61, Now).Count);
    }

    /// <summary>The rail's two marks live in the same left column, and the unread count takes its own role.</summary>
    [Fact]
    public void Rail_DrawsItsTwoMarksAndItsUnreadCounts()
    {
        var host = new FakeShellHost();
        var rail = new Rail(
            [
                new Destination(DestinationKind.Home, "Home"),
                new Destination(DestinationKind.Local, "Local"),
                new Destination(DestinationKind.Notifications, "Notifications") { Unread = 4 },
            ],
            host,
            TimeSpan.FromMilliseconds(250));

        rail.Step(1);

        var lines = RailLines.Of(rail, quota: null, height: 10);

        // The cursor has moved and the selection has not, so the two marks are on different rows.
        Assert.StartsWith(" ▸", lines[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("▶ ", lines[1].Text, StringComparison.Ordinal);

        Assert.Equal(Role.RailCurrent, lines[0].Spans[0].Role);
        Assert.Equal(Role.Rail, lines[1].Spans[0].Role);

        var counted = lines.First(line => line.Text.Contains("Notification", StringComparison.Ordinal));
        Assert.Contains(counted.Spans, span => span is { Role: Role.RailUnread, Text: "4" });
    }

    /// <summary>The rail is 18 columns however long a destination is called (docs/tui-shell.md).</summary>
    [Fact]
    public void Rail_IsEighteenColumnsWide()
    {
        var rail = new Rail(
            [new Destination(DestinationKind.Messages, "Direct messages") { Unread = 12 }],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var lines = RailLines.Of(rail, quota: null, height: 6);

        Assert.All(lines, line => Assert.Equal(RailLines.Width, line.Width));
        Assert.Contains("12", lines[0].Text);
    }

    /// <summary>The quota goes red when it is nearly spent, and is drawn as nothing before anything has asked.</summary>
    [Fact]
    public void Rail_DrawsTheQuotaAtItsFootAndSaysWhenItIsNearlySpent()
    {
        var rail = new Rail([new Destination(DestinationKind.Home, "Home")], new FakeShellHost(), TimeSpan.Zero);

        var plenty = RailLines.Of(rail, new RateLimitQuota(213, 300, null), 8);
        var nearly = RailLines.Of(rail, new RateLimitQuota(5, 300, null), 8);

        Assert.Contains("213/300 left", plenty[^1].Text);
        Assert.Equal(Role.Quota, plenty[^1].Role);
        Assert.Equal(Role.QuotaLow, nearly[^1].Role);
    }

    /// <summary>
    ///     A fetch in flight is said once, on the breadcrumb, beside the content it is about to replace — and never on
    ///     the rail, which holds still (ADR-0014).
    /// </summary>
    [Fact]
    public void Breadcrumb_SaysAFetchIsInFlightAndTheRailDoesNot()
    {
        var still = ChromeLines.Breadcrumb("home", fetching: false, 61);
        var busy = ChromeLines.Breadcrumb("home", fetching: true, 61);

        Assert.DoesNotContain("fetching", still.Text, StringComparison.Ordinal);
        Assert.Contains("fetching…", busy.Text);
        Assert.Contains(busy.Spans, span => span.Role == Role.Loading);
    }

    /// <summary>A confirmation displaces the keys, in the role that says what kind of thing is being asked.</summary>
    [Fact]
    public void Status_DrawsAConfirmationInTheDestructiveRole()
    {
        var asking = ChromeLines.Status(
            [new KeyHint("j/k", "post")],
            notice: null,
            noticeIsError: false,
            new Confirmation("Delete post 110? This cannot be undone."),
            80);

        Assert.Contains(asking.Spans, span => span.Role == Role.Destructive);
        Assert.Contains("cannot be undone", asking.Text);
        Assert.DoesNotContain("j/k", asking.Text, StringComparison.Ordinal);
    }

    /// <summary>A failure is drawn as one; a remark is not.</summary>
    [Fact]
    public void Status_TellsAFailureApartFromARemark()
    {
        var failed = ChromeLines.Status([], "Only your own posts can be deleted.", noticeIsError: true, null, 80);
        var said = ChromeLines.Status([], "Sent.", noticeIsError: false, null, 80);

        Assert.Equal(Role.Error, failed.Role);
        Assert.Equal(Role.Muted, said.Role);
    }

    /// <summary>Nothing to say means the keys, which is what the status row is for the rest of the time.</summary>
    [Fact]
    public void Status_SaysWhatThisScreensKeysAre()
    {
        var keys = ChromeLines.Status(
            [new KeyHint("⏎", "read"), new KeyHint("a", "author")],
            notice: null,
            noticeIsError: false,
            asking: null,
            80);

        Assert.Contains("⏎ read", keys.Text);
        Assert.Contains("a author", keys.Text);
        Assert.Equal(Role.Chrome, keys.Role);
    }

    /// <summary>
    ///     The narrow case the whole shape was chosen for: 61 columns is what an 80-column terminal leaves, and no row
    ///     may run past it (ADR-0014).
    /// </summary>
    [Fact]
    public void Feed_ReadsAtSixtyOneColumns()
    {
        var post = APost.With(
            author: "Somebody With A Very Long Display Name Indeed",
            account: "somebody@an-extremely-long-instance-domain.example",
            content: "Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application "
                     + "model made the whole shell testable — no more static state leaking between test runs.",
            media: [APost.APicture(description: "A screenshot of a terminal showing a great deal of text indeed")]);

        var lines = PostLines.Feed(post, 61, revealed: false, Now);

        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }
}
