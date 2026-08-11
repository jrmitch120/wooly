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
    ///     Walks every role in the contract and asserts some view actually emits it — the test that would have caught
    ///     <see cref="Role.Poll" /> sitting dead in the contract, themed and documented with nothing ever drawing it,
    ///     before this ticket wired a poll's block bars to it (#80).
    /// </summary>
    /// <remarks>
    ///     Nothing is exempt: <see cref="Role.ReferencePicked" /> was the contract's one other dead role until #83
    ///     wired the brackets a picked reference is drawn in, and the exemption came off with it.
    /// </remarks>
    [Fact]
    public void EveryRoleInTheContractIsEmittedBySomeView()
    {
        var seen = new HashSet<Role>();

        void Collect(IEnumerable<Line> lines)
        {
            foreach (var line in lines)
            {
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

        Collect(PostLines.Feed(mine, 61, revealed, Now, FakePictures.With()));
        Collect(PostLines.Feed(
            APost.With(media: [APost.APicture(description: null)]),
            61,
            default,
            Now,
            FakePictures.With()));
        Collect(PostLines.Whole(mine, 61, revealed, Now));

        // With the first reference in the text picked out, which is the only thing that draws the brackets (#83).
        Collect(PostLines.Feed(mine, 61, revealed with { Reference = BodyText.References(mine.Content)[0] }, Now));

        var feedScreen = new FeedScreen(
            new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
            [APost.With(id: "1"), APost.With(id: "2")]);
        Collect(feedScreen.Lines(61, Now));

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
        Collect(RailLines.Of(rail, new RateLimitQuota(5, 300, null), 10));

        Collect([ChromeLines.Breadcrumb("home", fetching: true, 61)]);
        Collect([
            ChromeLines.Status(
                [],
                notice: null,
                noticeIsError: false,
                new Confirmation("Delete post 110? This cannot be undone."),
                80),
        ]);
        Collect([ChromeLines.Status([], "Only your own posts can be deleted.", noticeIsError: true, null, 80)]);
        Collect([ChromeLines.Status([new KeyHint("⏎", "read")], null, noticeIsError: false, asking: null, 80)]);

        Assert.Empty(Enum.GetValues<Role>().Except(seen));
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
    public void RoleName_IsTheNameTheContractUses(Role role, string expected)
    {
        Assert.Equal(expected, RoleName.Of(role));
        Assert.Equal(role, RoleName.For(expected));
    }

    /// <summary>ADR-0014's own example: a boost that is mine is drawn as mine rather than as anybody's.</summary>
    [Fact]
    public void Feed_DrawsAMarkOfTheReadersOwnInItsOwnRole()
    {
        var anybodys = PostLines.Feed(APost.With(), 61, default, Now);
        var mine = PostLines.Feed(
            APost.With(marks: APost.Marked(boosted: true, favorited: true)),
            61,
            default,
            Now);

        Assert.Contains(anybodys, line => line.Has(Role.Boost));
        Assert.Contains(anybodys, line => line.Has(Role.Favorite));
        Assert.DoesNotContain(anybodys, line => line.Has(Role.BoostMine));

        Assert.Contains(mine, line => line.Has(Role.BoostMine));
        Assert.Contains(mine, line => line.Has(Role.FavoriteMine));
        Assert.DoesNotContain(mine, line => line.Has(Role.Boost));
    }

    /// <summary>
    ///     A boost or favorite that is mine carries a glyph a colour cannot draw on top of — the closed circle arrow
    ///     and the filled star — rather than only the role, which a terminal reporting no colour draws identically to
    ///     anybody else's.
    /// </summary>
    [Fact]
    public void Feed_DrawsMineAsAClosedArrowAndAFilledStar()
    {
        var anybodys = PostLines.Feed(APost.With(), 61, default, Now);
        var mine = PostLines.Feed(
            APost.With(marks: APost.Marked(boosted: true, favorited: true)),
            61,
            default,
            Now);

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
        var lines = PostLines.Feed(APost.With(author: "Maria Ochoa", account: "maria@fosstodon.org"), 61, default, Now);
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

        var feed = PostLines.Feed(APost.With(content: Said), 61, default, Now);
        var thread = new ConversationScreen(
            AConversation.Thread(AConversation.With(), APost.With(content: Said, visibility: PostVisibility.Direct)))
            .Lines(61, Now);

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

        var spans = editor.Lines(61, Now).SelectMany(line => line.Spans).ToList();

        Assert.DoesNotContain(spans, span => span.Role is Role.Mention or Role.Hashtag or Role.Link);
        Assert.Contains(spans, span => span.Role == Role.Body);
    }

    /// <summary>A warning is a warning whether or not there is colour to draw it in, so it carries its own glyph.</summary>
    [Fact]
    public void Feed_DrawsAContentWarningInItsOwnRoleAndWithItsOwnGlyph()
    {
        var lines = PostLines.Feed(APost.With(contentWarning: "spoilers"), 61, default, Now);
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
        var lines = PostLines.Feed(APost.With(visibility: visibility), 61, default, Now);

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
            61,
            default,
            Now,
            FakePictures.With());

        var undescribed = PostLines.Feed(
            APost.With(media: [APost.APicture(description: null)]),
            61,
            default,
            Now,
            FakePictures.With());

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

        var picked = screen.Lines(61, Now).Where(line => line.Has(Role.Selection)).ToList();

        Assert.NotEmpty(picked);
        Assert.All(picked, line => Assert.StartsWith("▌", line.Text, StringComparison.Ordinal));

        screen.Move(1);

        // Exactly one post is picked out at a time, so moving does not leave the last one lit.
        var after = screen.Lines(61, Now).Where(line => line.Has(Role.Selection)).ToList();
        Assert.NotEmpty(after);
        Assert.NotEqual(picked.Count + after.Count, screen.Lines(61, Now).Count);
    }

    /// <summary>The rail's one mark column, and the unread count taking its own role.</summary>
    [Fact]
    public void Rail_DrawsItsOneMarkColumnAndItsUnreadCounts()
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

        // The cursor has moved and the selection has not, so the hollow and filled marks are on different rows.
        Assert.StartsWith("▷ ", lines[0].Text, StringComparison.Ordinal);
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

    /// <summary>
    ///     The status row is one row, and a list longer than it is cut off at the right — so a screen's own keys have
    ///     to be on the part that survives. The keys #29 adds are exactly the ones that would otherwise be lost behind
    ///     ten keys a reader has already met on every timeline.
    /// </summary>
    [Fact]
    public void Status_KeepsAScreensOwnKeysOnTheRowAtEightyColumns()
    {
        var account = new AccountScreen(
            AnAccount.With(standing: AnAccount.Standing(following: true)),
            [APost.With()]);

        var inbox = new NotificationsScreen([ANotification.With()]);

        var onAnAccount = ChromeLines.Status(account.Keys, null, noticeIsError: false, asking: null, 80).Text;
        var onTheInbox = ChromeLines.Status(inbox.Keys, null, noticeIsError: false, asking: null, 80).Text;

        Assert.Contains("F:unfollow", onAnAccount);
        Assert.Contains("M:mute", onAnAccount);
        Assert.Contains("B:block", onAnAccount);

        Assert.Contains("d:dismiss", onTheInbox);
        Assert.Contains("D:clear all", onTheInbox);

        // And the two movements #51 split apart are both said, since neither key does what the other one does — the
        // shared one behind the screen's own keys, which is the order the cut at the right exists for.
        Assert.Contains("j/k:post", onAnAccount);
        Assert.Contains("↓/↑:row", onAnAccount);

        Assert.True(
            onTheInbox.IndexOf("D:clear all", StringComparison.Ordinal)
            < onTheInbox.IndexOf("↓/↑:row", StringComparison.Ordinal));

        // And the row is still one row, which is what makes the cut necessary in the first place.
        Assert.True(onAnAccount.Length <= 80);
        Assert.True(onTheInbox.Length <= 80);
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

        Assert.Contains("⏎:read", keys.Text);
        Assert.Contains("a:author", keys.Text);

        // The key stays Chrome and the explanation takes Muted — the split #66 draws them in — rather than one role
        // for the whole row.
        Assert.Contains(keys.Spans, span => span is { Role: Role.Chrome, Text: "⏎" });
        Assert.Contains(keys.Spans, span => span is { Role: Role.Muted, Text: ":read" });
    }

    /// <summary>
    ///     A notification says who did what before it says anything else, and the name takes the same role it takes on
    ///     a post — the byline of a mention and the byline of the post under it are the same thing said twice.
    /// </summary>
    [Fact]
    public void Notifications_DrawWhoDidWhatInTheBylineRole()
    {
        var screen = new NotificationsScreen([ANotification.With(author: "Alice"), ANotification.Follow()]);
        var lines = screen.Lines(61, Now);

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

        Assert.Contains(screen.Lines(61, Now), line => line.Text.Contains("poll", StringComparison.Ordinal));
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

        var lines = screen.Lines(61, Now);

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

        Assert.Contains(screen.Lines(61, Now), line => line.Text.Contains("Nothing left", StringComparison.Ordinal));
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

        var prompt = screen.Lines(61, Now)[0];

        Assert.Contains("Search: c", prompt.Text);
        Assert.Contains(prompt.Spans, span => span is { Role: Role.Selection, Text: "▌" });

        screen.Found("c", SearchResults.Matching(SearchKind.Everything, [], [], []));

        Assert.DoesNotContain(screen.Lines(61, Now)[0].Spans, span => span.Role == Role.Selection);
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
            new AccountScreen(account, [wordy]),
            new DirectMessagesScreen([talkative, AConversation.Emptied(id: "9")]),
            new ConversationScreen(AConversation.Thread(talkative, wordy)),
        ];

        foreach (var screen in screens)
        {
            Assert.All(
                screen.Lines(61, Now),
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

        var lines = PostLines.Feed(post, 61, default, Now);

        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }
}
