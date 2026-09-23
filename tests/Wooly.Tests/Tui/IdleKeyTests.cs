using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A key that cannot act on what is picked out is not on the status row and not in its count (#220,
///     <c>docs/tui-shell.md</c>): <c>p</c>, <c>e</c> and <c>d</c> on a post that is not the reader's own, and <c>x</c>
///     on a post with nothing hidden or already asked past here.
/// </summary>
/// <remarks>
///     Asked of every screen that can pick a post, rather than of the feed alone, since the rule is
///     <see cref="Screen.Keys" />'s and a screen that escaped it would be a row that lies.
/// </remarks>
public class IdleKeyTests
{
    private static readonly string[] Yours = ["p:pin", "e:edit", "d:delete"];

    private const string Revealing = "x:show warning";

    /// <summary>Every screen that can have a post picked out, which is where these keys could be announced at all.</summary>
    public static TheoryData<string> Screens => new(
        "feed",
        "post",
        "post-reply",
        "account-post",
        "notifications-mention",
        "search-post",
        "conversation");

    /// <summary>
    ///     By letter, since a screen may say something of its own by one of them — the inbox's <c>d</c> dismisses — and
    ///     that is <see cref="PostKeys.Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" />'s rule rather than this one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void Keys_OnThePostOfTheReadersOwn_SayPinEditAndDelete(string state) =>
        Assert.Subset(
            On(state, APost.With(isMine: true)).Keys.Select(key => key.Key).ToHashSet(StringComparer.Ordinal),
            new HashSet<string>(["p", "e", "d"], StringComparer.Ordinal));

    /// <summary>And a screen's own key by one of those letters stays on somebody else's post, being no post key.</summary>
    [Fact]
    public void Keys_OnSomebodyElsesPost_LeaveTheInboxsDismissAlone() =>
        Assert.Contains("d:dismiss", Said(On("notifications-mention", APost.With())));

    [Theory]
    [MemberData(nameof(Screens))]
    public void Keys_OnSomebodyElsesPost_SayNoneOfPinEditOrDelete(string state)
    {
        var said = Said(On(state, APost.With(isMine: false)));

        foreach (var key in Yours)
        {
            Assert.DoesNotContain(key, said);
        }
    }

    /// <summary>
    ///     A boost is asked about by the post inside it, which is what the three act on: somebody else boosting the
    ///     reader's post leaves the three on, and the reader boosting somebody else's takes them off.
    /// </summary>
    [Fact]
    public void Keys_OnABoost_AskWhoseThePostInsideItIs()
    {
        var mineBoosted = APost.With(id: "2", boosted: APost.With(id: "1", isMine: true));
        var theirsBoostedByMe = APost.With(id: "2", isMine: true, boosted: APost.With(id: "1"));

        SaysAllOf(Yours, On("feed", mineBoosted));
        Assert.DoesNotContain("p:pin", Said(On("feed", theirsBoostedByMe)));
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void Keys_OnAPostHidingNothing_DoNotSayShowWarning(string state) =>
        Assert.DoesNotContain(Revealing, Said(On(state, APost.With())));

    [Theory]
    [MemberData(nameof(Screens))]
    public void Keys_OnAWarnedPost_SayShowWarning(string state) =>
        Assert.Contains(Revealing, Said(On(state, APost.With(contentWarning: "spoilers"))));

    /// <summary>A post flagged sensitive with something under it is warned too, with no text written over it (#113).</summary>
    [Fact]
    public void Keys_OnAPostWhoseMediaIsFlagged_SayShowWarning() =>
        Assert.Contains(
            Revealing,
            Said(On("feed", APost.With(sensitive: true, media: [APost.APicture()]))));

    /// <summary>
    ///     A reveal is one-way, so <c>x</c> leaves the row the moment it has acted — and, being one of the keys the row
    ///     had no room for, the count it was in drops by one.
    /// </summary>
    [Fact]
    public void Keys_AfterAReveal_NoLongerSayShowWarning_AndTheCountDropsByOne()
    {
        var screen = On("feed", APost.With(contentWarning: "spoilers", isMine: true));
        var before = Over(screen);

        Assert.True(screen.Reveal());

        Assert.DoesNotContain(Revealing, Said(screen));
        Assert.Equal(before - 1, Over(screen));
    }

    /// <summary>
    ///     And a key dropped is out of the count rather than hidden in it: somebody else's post, hiding nothing, has
    ///     four fewer keys to count than the reader's own warned one.
    /// </summary>
    [Fact]
    public void TheCount_LeavesOutEveryKeyThatCannotAct() =>
        Assert.Equal(
            Over(On("feed", APost.With(contentWarning: "spoilers", isMine: true))) - 4,
            Over(On("feed", APost.With())));

    /// <summary>
    ///     <c>?</c> on somebody else's post lists the keys the row ranked and nothing more — read through the keymap
    ///     screen itself, so that it is its rule being asserted rather than a second copy of it.
    /// </summary>
    [Fact]
    public void Help_OnSomebodyElsesPost_ListsWhatTheRowDrewFrom()
    {
        var screen = On("feed", APost.With());
        var listed = new HelpScreen(screen).Lines(new Drawing(80, AShell.Now))
            .Select(line => line.Text)
            .SkipWhile(text => !text.StartsWith("On ", StringComparison.Ordinal))
            .Skip(2)
            .TakeWhile(text => text.Length > 0)
            .ToList();

        Assert.Equal(screen.Keys.Select(key => $"{Glyphs.Padded(key.Key, 16)}{key.Does}"), listed);

        foreach (var key in Yours.Append(Revealing))
        {
            Assert.DoesNotContain(key[(key.IndexOf(':') + 1)..], listed.Select(text => text[16..]));
        }
    }

    /// <summary>
    ///     The two drops already there hold beside this one rather than being stood in for by it: an empty screen says
    ///     no post key, and a screen with things on it that are not posts says none either — whoever those things are.
    /// </summary>
    [Fact]
    public void Keys_TheEarlierDropsStillHoldBesideThisOne()
    {
        var empty = Said(new FeedScreen(new Destination(DestinationKind.Home, "Home"), [], "Nothing here yet."));
        var follow = Said(new NotificationsScreen([ANotification.Follow()]));

        foreach (var said in new[] { empty, follow })
        {
            foreach (var key in Yours.Append(Revealing).Append("⏎:read").Append("b:boost"))
            {
                Assert.DoesNotContain(key, said);
            }

            Assert.Contains("c:compose", said);
        }

        Assert.Contains("d:dismiss", follow);
    }

    /// <summary>A post built without saying whose it is, is not the reader's own.</summary>
    [Fact]
    public void APost_BuiltWithoutSayingWhoseItIs_IsNotTheReadersOwn() =>
        Assert.False(new Post
        {
            Id = "1",
            Account = "jeff@mastodon.social",
            Author = "Jeff",
            PostedAt = AShell.Now,
            Content = "Hello",
            Visibility = PostVisibility.Public,
            Boosts = 0,
            Favorites = 0,
            Replies = 0,
            Marks = PostMarks.None,
        }.IsMine);

    private static void SaysAllOf(IEnumerable<string> keys, Screen screen)
    {
        var said = Said(screen);

        foreach (var key in keys)
        {
            Assert.Contains(key, said);
        }
    }

    private static IReadOnlyList<string> Said(Screen screen) => [.. screen.Keys.Select(key => key.ToString())];

    /// <summary>What the row's <c>…+N</c> counts at 80 columns.</summary>
    private static int Over(Screen screen)
    {
        var mark = ChromeLines.Status(screen.Keys, notice: null, noticeIsError: false, asking: null, width: 80).Text
            .Split(" · ")
            .Single(said => said.StartsWith("…+", StringComparison.Ordinal));

        return int.Parse(mark[2..], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The screen <paramref name="state" /> names, with <paramref name="post" /> picked out on it.</summary>
    private static Screen On(string state, Post post)
    {
        switch (state)
        {
            case "feed":
                return new FeedScreen(new Destination(DestinationKind.Home, "Home"), [post]);

            case "post":
                return new PostScreen(post, new PostThread([], []));

            case "post-reply":
                var thread = new PostScreen(APost.With(id: "1"), new PostThread([], [post with { Id = "220" }]));

                thread.Move(1);

                return thread;

            case "account-post":
                var account = new AccountScreen(AnAccount.With(), [post], pinned: []);

                account.Move(1);

                return account;

            case "notifications-mention":
                return new NotificationsScreen([ANotification.With(post: post)]);

            case "search-post":
                var search = new SearchScreen();

                search.Found("maria", new Wooly.Core.Search.SearchResults { Posts = [post] });

                return search;

            case "conversation":
                return new ConversationScreen(
                    AConversation.Thread(AConversation.With(), post with { Visibility = PostVisibility.Direct }));

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "No screen by that name.");
        }
    }
}
