using System.Text.RegularExpressions;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Search;
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
public partial class RoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     The roles in the contract that no view draws yet, each with the issue that will. The second assertion
    ///     in <see cref="EveryRoleInTheContractIsEmittedBySomeView" /> keeps this honest: a role drawn and still listed
    ///     here is an exemption somebody forgot to take off.
    /// </summary>
    private static readonly Role[] NotYetDrawn = [];

    /// <summary>
    ///     Walks every role in the contract and asserts some view actually emits it — the test that would have caught
    ///     <see cref="Role.Poll" /> sitting dead in the contract, themed and documented with nothing ever drawing it,
    ///     before this ticket wired a poll's block bars to it (#80).
    /// </summary>
    /// <remarks>
    ///     <see cref="Role.ReferencePicked" /> was the contract's one other dead role until #83 wired the brackets a
    ///     picked reference is drawn in, and the exemption came off with it. The roles ADR-0021 added are named before
    ///     anything draws them (#266), and each comes off <see cref="NotYetDrawn" /> with the issue that draws it.
    /// </remarks>
    [Fact]
    public void EveryRoleInTheContractIsEmittedBySomeView()
    {
        var seen = new HashSet<Role>();

        void Collect(IEnumerable<Line> lines)
        {
            foreach (var line in lines)
            {
                // The band is a row's rather than a span's: the view draws a picked row on it (#269).
                if (line.Picked)
                {
                    seen.Add(Role.Band);
                }

                foreach (var span in line.Spans)
                {
                    seen.Add(span.Role);
                }
            }
        }

        var mine = APost.With(
            marks: APost.Marked(boosted: true, favorited: true, pinned: true),
            contentWarning: "spoilers",
            content: "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet",
            media: [APost.APicture()],
            poll: APost.APoll(multipleChoice: true, voters: 4));

        // Revealed, so the content warning's own row and the text behind it — carrying the hashtag, mention and
        // address roles — are both drawn; an unrevealed warning stands in front of the text instead.
        var revealed = new Reading(Revealed: true);

        Collect(PostLines.Feed(mine, new Drawing(61, Now, FakePictures.With()), revealed));
        Collect(PostLines.Feed(
            APost.With(media: [APost.APicture(description: null)]),
            new Drawing(61, Now, FakePictures.With()),
            default
        ));
        Collect(PostLines.Whole(mine, new Drawing(61, Now), revealed));

        // With the first reference in the text picked out, which is the only thing that draws the brackets (#83).
        Collect(PostLines.Feed(
            mine,
            new Drawing(61, Now),
            revealed with { Reference = BodyText.References(mine.Content)[0] }
        ));

        var feedScreen = new FeedScreen(
            new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
            [APost.With(id: "1"), APost.With(id: "2")]);
        Collect(feedScreen.Lines(new Drawing(61, Now)));

        var host = new FakeShellHost();
        var rail = new Rail(
            [
                new Destination(DestinationKind.Home, "Home"),
                new Destination(DestinationKind.Notifications, "Notifications") { Unread = 4 },
            ],
            host,
            TimeSpan.FromMilliseconds(250));
        rail.Step(1);
        Collect(RailLines.Of(rail, new RateLimitQuota(213, 300, null), 10));
        Collect(RailLines.Of(rail, new RateLimitQuota(5, 300, null), 10, coloured: true));

        Collect([ChromeLines.Breadcrumb(["Home"], frame: 1, 62)]);

        // Waiting for the browser to come back from a sign-in, which is what draws `loading` since the breadcrumb's
        // spinner took a role of its own.
        var signingIn = new AddProfileScreen();
        var authorizer = FakeBrowserAuthorizer.Holding();
        signingIn.Sent(authorizer, authorizer.AuthorizationUrl, opened: true);
        Collect(signingIn.Lines(new Drawing(61, Now)));
        Collect([
            ChromeLines.Status(
                [],
                notice: null,
                noticeIsError: false,
                new Confirmation("Delete this post?", Pressing.Nothing),
                80),
        ]);
        Collect([ChromeLines.Status([], "Only your own posts can be deleted.", noticeIsError: true, null, 80)]);
        Collect([ChromeLines.Status([new KeyHint("⏎", "read")], null, noticeIsError: false, asking: null, 80)]);

        Assert.Empty(Enum.GetValues<Role>().Except(seen).Except(NotYetDrawn));
        Assert.Empty(NotYetDrawn.Intersect(seen));
    }

    /// <summary>Every role in the contract has a name, and the built-in theme has an answer for it.</summary>
    [Fact]
    public void Theme_AnswersEveryRoleInTheContract()
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RoleName.Of(role)));

            // Would throw if the built-in theme had forgotten one, which is what a role table exists to prevent.
            Themes.Dark.For(role);
            Themes.Light.For(role);
            Themes.Plain.For(role);
        }
    }

    /// <summary>
    ///     The vocabulary is written down three times — the enum, the names a config file uses, and the table in
    ///     <c>docs/tui-shell.md</c> — and the third is the one nothing could enforce until this test. A role added to
    ///     the code and not to the table is a role nobody can find out how to theme; a row in the table with no role
    ///     behind it is a key somebody will write and be told does not exist.
    /// </summary>
    [Fact]
    public void TheRoleTableInTheContractIsTheRolesThereAre()
    {
        var documented = DocumentedRoles();
        var named = Enum.GetValues<Role>().Select(RoleName.Of).Order().ToList();

        Assert.Equal(named, documented);
    }

    /// <summary>
    ///     The role names in the table in <c>docs/tui-shell.md</c>, taken from the first column. Two of its rows name
    ///     no role — a drawn picture's own pixels, and the rail's cursor — and say so in words rather than in
    ///     backticks, which is what leaves them out of this.
    /// </summary>
    private static IReadOnlyList<string> DocumentedRoles()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot.Path, "docs", "tui-shell.md"))
                        .SkipWhile(line => line != "## Roles")
                        .Skip(1)
                        .SkipWhile(line => !line.StartsWith('|'))
                        .TakeWhile(line => line.StartsWith('|'))
                        .ToList();

        Assert.True(lines.Count > 10, $"Found only {lines.Count} rows of the role table in docs/tui-shell.md.");

        return lines.Select(line => line.Split('|')[1])
                    .SelectMany(cell => Quoted().Matches(cell).Select(match => match.Groups[1].Value))
                    .Order()
                    .ToList();
    }

    /// <summary>A name in backticks, which is how the table writes a role and how it writes nothing else.</summary>
    [GeneratedRegex("`([a-z-]+)`")]
    private static partial Regex Quoted();

    /// <summary>A name written in C# is not the key somebody has in their config file, so the two are kept apart.</summary>
    [Theory]
    [InlineData(Role.BylineHandle, "byline-handle")]
    [InlineData(Role.BoostMine, "boost-mine")]
    [InlineData(Role.RailUnread, "rail-unread")]
    [InlineData(Role.QuotaLow, "quota-low")]
    [InlineData(Role.PanelBorder, "panel-border")]
    [InlineData(Role.PanelBorderActive, "panel-border-active")]
    [InlineData(Role.PanelTitle, "panel-title")]
    [InlineData(Role.Band, "band")]
    [InlineData(Role.RailCursor, "rail-cursor")]
    [InlineData(Role.Gauge, "gauge")]
    [InlineData(Role.GaugeEmpty, "gauge-empty")]
    [InlineData(Role.Replies, "replies")]
    [InlineData(Role.Spinner, "spinner")]
    public void RoleName_IsTheNameTheContractUses(Role role, string expected)
    {
        Assert.Equal(expected, RoleName.Of(role));
        Assert.Equal(role, RoleName.For(expected));
    }

    /// <summary>ADR-0014's own example: a boost that is mine is drawn as mine rather than as anybody's.</summary>
    [Fact]
    public void Feed_DrawsAMarkOfTheReadersOwnInItsOwnRole()
    {
        var anybodys = PostLines.Feed(APost.With(), new Drawing(61, Now), default);
        var mine = PostLines.Feed(
            APost.With(marks: APost.Marked(boosted: true, favorited: true)),
            new Drawing(61, Now),
            default);

        Assert.Contains(anybodys, line => line.Has(Role.Boost));
        Assert.Contains(anybodys, line => line.Has(Role.Favorite));
        Assert.DoesNotContain(anybodys, line => line.Has(Role.BoostMine));

        Assert.Contains(mine, line => line.Has(Role.BoostMine));
        Assert.Contains(mine, line => line.Has(Role.FavoriteMine));
        Assert.DoesNotContain(mine, line => line.Has(Role.Boost));
    }

    /// <summary>
    ///     A reply count is drawn in a role of its own rather than in <see cref="Role.Muted" />, so it reads as a count
    ///     beside the boosts and favorites it sits with — on every screen that draws one (#266).
    /// </summary>
    [Fact]
    public void EveryScreenShowingAPostDrawsItsReplyCountInItsOwnRole()
    {
        var post = APost.With(id: "1");

        IReadOnlyList<Line>[] screens =
        [
            new FeedScreen(
                new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
                [post]).Lines(new Drawing(61, Now)),
            new PostScreen(post, new PostThread([], [])).Lines(new Drawing(61, Now)),
            new ConversationScreen(AConversation.Thread(posts: post)).Lines(new Drawing(61, Now)),
        ];

        foreach (var lines in screens)
        {
            var replies = Assert.Single(lines.SelectMany(line => line.Spans), span => span.Text.StartsWith('↩'));

            Assert.Equal(Role.Replies, replies.Role);
        }
    }

    /// <summary>
    ///     A boost or favorite that is mine carries a glyph a colour cannot draw on top of — the closed circle arrow
    ///     and the filled star — rather than only the role, which a terminal reporting no colour draws identically to
    ///     anybody else's.
    /// </summary>
    [Fact]
    public void Feed_DrawsMineAsAClosedArrowAndAFilledStar()
    {
        var anybodys = PostLines.Feed(APost.With(), new Drawing(61, Now), default);
        var mine = PostLines.Feed(
            APost.With(marks: APost.Marked(boosted: true, favorited: true)),
            new Drawing(61, Now),
            default);

        Assert.Contains(anybodys, line => line.Text.Contains("↺ 3", StringComparison.Ordinal));
        Assert.Contains(anybodys, line => line.Text.Contains("☆ 5", StringComparison.Ordinal));

        Assert.Contains(mine, line => line.Text.Contains("⥀ 3", StringComparison.Ordinal));
        Assert.Contains(mine, line => line.Text.Contains("★ 5", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A byline is two different things one above the other since #77, and they are two roles rather than one —
    ///     the name with the audience beside it, and the handle on the row under it.
    /// </summary>
    [Fact]
    public void Feed_DrawsANameAndAHandleInTheirOwnRoles()
    {
        var lines = PostLines.Feed(
            APost.With(author: "Maria Ochoa", account: "maria@fosstodon.org"),
            new Drawing(61, Now),
            default);
        var name = lines.First(line => line.Has(Role.BylineName));
        var handle = lines.First(line => line.Has(Role.BylineHandle));

        Assert.Contains(name.Spans, span => span is { Role: Role.BylineName, Text: "Maria Ochoa" });
        Assert.Contains(name.Spans, span => span.Role == Role.Audience);
        Assert.Contains(handle.Spans, span => span.Role == Role.BylineHandle && span.Text.Contains('@'));
    }

    /// <summary>
    ///     The three roles inside a post's text (#46). Asserted on the feed and again in a conversation, because the
    ///     whole point of putting the split at the one place a body is wrapped is that every screen showing a post
    ///     gets it — the feed, the post screen, a thread and the notification list alike.
    /// </summary>
    [Fact]
    public void EveryScreenShowingAPostDrawsItsTagsMentionsAndAddresses()
    {
        const string Said = "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet";

        var feed = PostLines.Feed(APost.With(content: Said), new Drawing(61, Now), default);
        var thread = new ConversationScreen(
            AConversation.Thread(AConversation.With(), APost.With(content: Said, visibility: PostVisibility.Direct)))
            .Lines(new Drawing(61, Now));

        foreach (var lines in (IReadOnlyList<Line>[])[feed, thread])
        {
            var spans = lines.SelectMany(line => line.Spans).ToList();

            Assert.Contains(spans, span => span is { Role: Role.Mention, Text: "@maria@fosstodon.org" });
            Assert.Contains(spans, span => span is { Role: Role.Link, Text: "https://example.com/notes" });
            Assert.Contains(spans, span => span is { Role: Role.Hashtag, Text: "#dotnet" });
        }
    }

    /// <summary>
    ///     The editor stays plain, deliberately: a mention lights and goes out again as it is typed, and this client
    ///     has not resolved what somebody is halfway through writing, so a role there would assert something it has
    ///     not checked (#46).
    /// </summary>
    [Fact]
    public void Compose_DrawsWhatIsBeingTypedAsPlainText()
    {
        var editor = new ComposeScreen(ComposeFor.Post) { Text = "Thanks @maria@fosstodon.org #dotnet" };

        var spans = editor.Lines(new Drawing(61, Now)).SelectMany(line => line.Spans).ToList();

        Assert.DoesNotContain(spans, span => span.Role is Role.Mention or Role.Hashtag or Role.Link);
        Assert.Contains(spans, span => span.Role == Role.Body);
    }

    /// <summary>A warning is a warning whether or not there is colour to draw it in, so it carries its own glyph.</summary>
    [Fact]
    public void Feed_DrawsAContentWarningInItsOwnRoleAndWithItsOwnGlyph()
    {
        var lines = PostLines.Feed(APost.With(contentWarning: "spoilers"), new Drawing(61, Now), default);
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
        var lines = PostLines.Feed(APost.With(visibility: visibility), new Drawing(61, Now), default);

        Assert.Contains(lines, line => line.Text.Contains(glyph, StringComparison.Ordinal));
        Assert.Equal(glyph, PostLines.Audience(visibility));
    }

    /// <summary>An attachment nobody described says so, in the muted role, rather than pretending to a description.</summary>
    [Fact]
    public void Feed_DrawsAnAttachmentAndSaysWhereItHasNoDescription()
    {
        // On a terminal that draws, so the mark is the picture's rather than a link's.
        var described = PostLines.Feed(
            APost.With(media: [APost.APicture(description: "A cartoon sheep")]),
            new Drawing(61, Now, FakePictures.With()),
            default);

        var undescribed = PostLines.Feed(
            APost.With(media: [APost.APicture(description: null)]),
            new Drawing(61, Now, FakePictures.With()),
            default);

        var first = described.First(line => line.Text.Contains("▒▒▒▒", StringComparison.Ordinal));
        Assert.True(first.Has(Role.Media));
        Assert.Contains("A cartoon sheep", first.Text);

        var second = undescribed.First(line => line.Text.Contains("▒▒▒▒", StringComparison.Ordinal));
        Assert.Contains("a picture, undescribed", second.Text);
        Assert.Contains(second.Spans, span => span.Role == Role.Muted);
    }

    /// <summary>The selected row is told apart by a gutter mark as well as by its role.</summary>
    [Fact]
    public void Feed_MarksTheSelectedRowInTheGutter()
    {
        var screen = new FeedScreen(
            new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
            [APost.With(id: "1"), APost.With(id: "2")]);

        var picked = screen.Lines(new Drawing(61, Now)).Where(line => line.Has(Role.Selection)).ToList();

        Assert.NotEmpty(picked);
        Assert.All(picked, line => Assert.StartsWith("▌", line.Text, StringComparison.Ordinal));

        screen.Move(1);

        // Exactly one post is picked out at a time, so moving does not leave the last one lit.
        var after = screen.Lines(new Drawing(61, Now)).Where(line => line.Has(Role.Selection)).ToList();
        Assert.NotEmpty(after);
        Assert.NotEqual(picked.Count + after.Count, screen.Lines(new Drawing(61, Now)).Count);
    }

    /// <summary>
    ///     The rail's marks: without colour the one mark column, the selection keeping <c>rail-current</c>; in colour
    ///     the cursor's row on <c>rail-cursor</c> instead of a mark — and the unread count taking its own role in both.
    /// </summary>
    [Fact]
    public void Rail_DrawsTheCursorAndTheSelectionAndItsUnreadCounts()
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

        var plain = RailLines.Of(rail, quota: null, height: 30);
        var coloured = RailLines.Of(rail, quota: null, height: 30, coloured: true);

        // The cursor has moved and the selection has not, so the hollow and filled marks are on different rows.
        Assert.Equal("│▷ Home            │", plain[1].Text);
        Assert.Equal("│▶ Local           │", plain[2].Text);
        Assert.Equal(Role.RailCurrent, plain[1].Spans[1].Role);
        Assert.Equal(Role.Rail, plain[2].Spans[1].Role);

        Assert.Equal("│Home              │", coloured[1].Text);
        Assert.Equal(Role.RailCurrent, coloured[1].Spans[1].Role);
        Assert.Equal(Role.RailCursor, coloured[2].Spans[1].Role);

        var counted = plain.First(line => line.Text.Contains("Notification", StringComparison.Ordinal));
        Assert.Contains(counted.Spans, span => span is { Role: Role.RailUnread, Text: "4" });
    }

    /// <summary>The rail is 20 columns however long a destination is called, framed or not (docs/tui-shell.md).</summary>
    [Theory]
    [InlineData(6)]
    [InlineData(30)]
    public void Rail_IsTwentyColumnsWide(int height)
    {
        var rail = new Rail(
            [new Destination(DestinationKind.Messages, "Direct messages") { Unread = 12 }],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var lines = RailLines.Of(rail, quota: null, height: height);

        Assert.All(lines, line => Assert.Equal(RailLines.Width, line.Width));
        Assert.Contains(lines, line => line.Text.Contains("12", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The budget is a gauge at the rail's foot, its filled cells and percentage drawn as nearly spent when they
    ///     are — and nothing before anything has asked.
    /// </summary>
    [Fact]
    public void Rail_DrawsTheBudgetAsAGaugeAtItsFootAndSaysWhenItIsNearlySpent()
    {
        var rail = new Rail([new Destination(DestinationKind.Home, "Home")], new FakeShellHost(), TimeSpan.Zero);

        var plenty = RailLines.Of(rail, new RateLimitQuota(213, 300, null), 8);
        var nearly = RailLines.Of(rail, new RateLimitQuota(5, 300, null), 8);

        Assert.Contains("71%", plenty[^2].Text, StringComparison.Ordinal);
        Assert.True(plenty[^2].Has(Role.Gauge));
        Assert.True(plenty[^2].Has(Role.GaugeEmpty));
        Assert.True(nearly[^2].Has(Role.QuotaLow));
        Assert.False(nearly[^2].Has(Role.Gauge));
    }

    /// <summary>
    ///     A fetch in flight is said once, on the breadcrumb, beside the content it is about to replace — and never on
    ///     the rail, which holds still (ADR-0014).
    /// </summary>
    [Fact]
    public void Breadcrumb_SaysAFetchIsInFlightAndTheRailDoesNot()
    {
        var still = ChromeLines.Breadcrumb(["Home"], frame: 0, 62);
        var busy = ChromeLines.Breadcrumb(["Home"], frame: 1, 62);

        Assert.DoesNotContain("·", still.Text, StringComparison.Ordinal);
        Assert.Contains("Home ·", busy.Text, StringComparison.Ordinal);
        Assert.Contains(busy.Spans, span => span.Role == Role.Spinner);
    }

    /// <summary>
    ///     The spinner is a role of its own so that a theme can colour it apart from anything else, and both built-ins
    ///     draw it as they draw an unread count: the two things on screen that say something has changed.
    /// </summary>
    [Fact]
    public void Spinner_IsDrawnInTheUnreadCountsColourByEveryBuiltIn()
    {
        Assert.Equal(Themes.Dark.For(Role.RailUnread), Themes.Dark.For(Role.Spinner));
        Assert.Equal(Themes.Light.For(Role.RailUnread), Themes.Light.For(Role.Spinner));
    }

    /// <summary>A confirmation displaces the keys, in the role that says what kind of thing is being asked.</summary>
    [Fact]
    public void Status_DrawsAConfirmationInTheDestructiveRole()
    {
        var asking = ChromeLines.Status(
            [new KeyHint("j/k", "post")],
            notice: null,
            noticeIsError: false,
            new Confirmation("Delete this post?", Pressing.Nothing),
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

    /// <summary>
    ///     The status row is one row, drawn from the front of the rank — so a screen's own keys have to be at the front
    ///     (#218). The keys #29 adds are exactly the ones that would otherwise be lost behind ten keys a reader has
    ///     already met on every timeline.
    /// </summary>
    [Fact]
    public void Status_KeepsAScreensOwnKeysOnTheRowAtEightyColumns()
    {
        var account = new AccountScreen(
            AnAccount.With(standing: AnAccount.Standing(following: true)),
            [APost.With()],
            pinned: []);

        var inbox = new NotificationsScreen([ANotification.With()]);

        var onAnAccount = ChromeLines.Status(account.Keys, null, noticeIsError: false, asking: null, 80).Text;
        var onTheInbox = ChromeLines.Status(inbox.Keys, null, noticeIsError: false, asking: null, 80).Text;

        Assert.Contains("Unfollow: F", onAnAccount);
        Assert.Contains("Mute: M", onAnAccount);
        Assert.Contains("Block: B", onAnAccount);

        Assert.Contains("Dismiss: d", onTheInbox);
        Assert.Contains("Clear all: D", onTheInbox);

        // And the two movements #51 split apart are both announced, since neither key does what the other one does —
        // the shared one in the tail of the rank, behind the screen's own keys and the marks alike (#218), so that it
        // is the one the row gives up for them.
        Assert.Contains("Post: j/k", onAnAccount);
        Assert.Contains(PostKeys.Scrolling, account.Keys);
        Assert.DoesNotContain("Row: ↓/↑", onAnAccount);

        // And the row is still one row, saying how many it had no room for and where to find them.
        Assert.True(onAnAccount.Length <= 80);
        Assert.True(onTheInbox.Length <= 80);
        Assert.EndsWith(" | Keys: ?", onAnAccount, StringComparison.Ordinal);
        Assert.Contains("…+", onTheInbox, StringComparison.Ordinal);
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

        Assert.Contains("Read: ⏎", keys.Text);
        Assert.Contains("Author: a", keys.Text);

        // The key takes Key and the explanation Muted — the split #66 draws them in, in the role #221 gave a key —
        // rather than one role for the whole row.
        Assert.Contains(keys.Spans, span => span is { Role: Role.Key, Text: "⏎" });
        Assert.Contains(keys.Spans, span => span is { Role: Role.Muted, Text: "Read: " });
    }

    /// <summary>
    ///     A notification says who did what before it says anything else, and the name takes the same role it takes on
    ///     a post — the byline of a mention and the byline of the post under it are the same thing said twice.
    /// </summary>
    [Fact]
    public void Notifications_DrawWhoDidWhatInTheBylineRole()
    {
        var screen = new NotificationsScreen([ANotification.With(author: "Alice"), ANotification.Follow()]);
        var lines = screen.Lines(new Drawing(61, Now));

        var said = lines.First(line => line.Has(Role.BylineName));

        Assert.Contains("Alice", said.Text);
        Assert.Contains("mentioned you", said.Text);
        Assert.Contains(lines, line => line.Text.Contains("followed you", StringComparison.Ordinal));

        // The row picked out is told apart by a mark as well as by a role, exactly as a feed's is.
        Assert.Contains(lines.Where(line => line.Has(Role.Selection)), line => line.Text.StartsWith('▌'));
    }

    /// <summary>A kind this client has never heard of is drawn under the instance's own word for it (ADR-0010).</summary>
    [Fact]
    public void Notifications_DrawAKindThisClientHasNoWordForUnderTheInstancesOwn()
    {
        var screen = new NotificationsScreen([ANotification.With(kind: NotificationKind.Reported("poll"))]);

        Assert.Contains(
            screen.Lines(new Drawing(61, Now)),
            line => line.Text.Contains("poll", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>docs/tui-shell.md</c>'s own example of what role selection is for: an unread conversation's badge takes
    ///     <c>rail-unread</c>, and one nobody has anything new in takes no badge at all rather than a dimmer one.
    /// </summary>
    [Fact]
    public void Messages_DrawAnUnreadConversationsBadgeInItsOwnRole()
    {
        var screen = new DirectMessagesScreen([
            AConversation.With(id: "7", with: ["alice@hachyderm.io"]),
            AConversation.With(id: "8", with: ["ben@hachyderm.io"], unread: false),
        ]);

        var lines = screen.Lines(new Drawing(61, Now));

        var unread = lines.First(line => line.Text.Contains("alice", StringComparison.Ordinal));
        var read = lines.First(line => line.Text.Contains("ben", StringComparison.Ordinal));

        Assert.Contains(unread.Spans, span => span is { Role: Role.RailUnread, Text: "unread" });
        Assert.DoesNotContain(read.Spans, span => span.Role == Role.RailUnread);

        // Who it is with is a handle wherever it is drawn, which is the same role a byline gives it.
        Assert.Contains(unread.Spans, span => span.Role == Role.BylineHandle);

        // And the row picked out is told apart by a mark as well as by a role, exactly as a feed's is.
        Assert.Contains(lines.Where(line => line.Has(Role.Selection)), line => line.Text.StartsWith('▌'));
    }

    /// <summary>
    ///     A conversation whose posts have all been taken down says so. The alternative is a heading with nothing
    ///     under it, which reads as a screen that went wrong rather than a conversation that was emptied.
    /// </summary>
    [Fact]
    public void Messages_SayWhereAConversationHasNothingLeftInIt()
    {
        var screen = new DirectMessagesScreen([AConversation.Emptied()]);

        Assert.Contains(
            screen.Lines(new Drawing(61, Now)),
            line => line.Text.Contains("Nothing left", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The search prompt says where the next letter lands with a mark rather than a colour, so a terminal with
    ///     none still shows where the typing is going.
    /// </summary>
    [Fact]
    public void Search_DrawsACaretWhileThePromptIsTakingLetters()
    {
        var screen = new SearchScreen();

        screen.Type('c');

        var prompt = screen.Lines(new Drawing(61, Now))[0];

        Assert.Contains("Search: c", prompt.Text);
        Assert.Contains(prompt.Spans, span => span is { Role: Role.Selection, Text: "▌" });

        screen.Found("c", SearchResults.Matching(SearchKind.Everything, [], [], []));

        Assert.DoesNotContain(screen.Lines(new Drawing(61, Now))[0].Spans, span => span.Role == Role.Selection);
    }

    /// <summary>
    ///     The same rule, of every screen this ticket brought: 61 columns is what an 80-column terminal leaves, and a
    ///     screen that ran past it would be one the shape was not chosen for (ADR-0014, docs/tui-shell.md).
    /// </summary>
    [Fact]
    public void EveryScreenReadsAtSixtyOneColumns()
    {
        var wordy = APost.With(
            author: "Somebody With A Very Long Display Name Indeed",
            account: "somebody@an-extremely-long-instance-domain.example",
            content: "Finally shipped the terminal client rewrite, which took rather longer than anybody expected.");

        var account = AnAccount.With(
            address: "somebody@an-extremely-long-instance-domain.example",
            author: "Somebody With A Very Long Display Name Indeed",
            followers: 1_203_004,
            following: 187_452,
            posts: 4_210_889);

        var search = new SearchScreen();
        search.Found(
            "a query somebody typed that is itself far longer than the prompt has room for",
            SearchResults.Matching(
                SearchKind.Everything,
                [account],
                [AHashtag.With("an-extremely-long-hashtag-somebody-really-did-use", 1_204_887, 90_112)],
                [wordy]));

        var talkative = AConversation.With(
            with: ["somebody@an-extremely-long-instance-domain.example", "another@an-equally-long-domain.example"],
            latest: wordy);

        Screen[] screens =
        [
            new NotificationsScreen([ANotification.With(author: "Somebody With A Very Long Display Name", post: wordy)]),
            new FollowRequestsScreen([account]),
            search,
            new AccountScreen(account, [wordy], pinned: []),
            new DirectMessagesScreen([talkative, AConversation.Emptied(id: "9")]),
            new ConversationScreen(AConversation.Thread(talkative, wordy)),
        ];

        foreach (var screen in screens)
        {
            Assert.All(
                screen.Lines(new Drawing(61, Now)),
                line => Assert.True(line.Width <= 61, $"{screen.Crumb}: '{line.Text}' is {line.Width} columns"));
        }
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

        var lines = PostLines.Feed(post, new Drawing(61, Now), default);

        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }
}
