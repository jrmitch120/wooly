using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The prompt <c>/</c> opens, what it asks the instance for, and what opening one of the three kinds of result
///     does. What is typed is a fact about the shell rather than something only a terminal knows (ADR-0015's reason,
///     inherited), which is what makes all of this assertable without one.
/// </summary>
public class ShellSearchTests
{
    /// <summary><c>/</c> is a frame key: it goes to search from wherever you are, and arrives at a prompt taking letters.</summary>
    [Fact]
    public async Task Search_OpensAPromptThatIsTakingWhatIsTyped()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();
        opened.Search();

        shell.Host.Drain();

        var search = Assert.IsType<SearchScreen>(opened.Screen);
        Assert.True(search.IsTyping);
        Assert.True(opened.Screen.IsTyping);
        Assert.Equal(DestinationKind.Search, opened.Rail.Showing.Kind);
        Assert.Empty(shell.Search.Searches);
    }

    [Fact]
    public async Task Find_AsksTheInstanceForWhatWasTyped()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Search();

        shell.Host.Drain();

        foreach (var letter in "cats")
        {
            opened.Type(letter);
        }

        opened.Type('x');
        opened.Backspace();

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var asked = Assert.Single(shell.Search.Searches);
        Assert.Equal("cats", asked.Query.Text);

        // Everything, because this screen has no way to ask for less and no reason to guess which kind was meant.
        Assert.Equal(SearchKind.Everything, asked.Query.Kind);

        var search = Assert.IsType<SearchScreen>(opened.Screen);
        Assert.False(search.IsTyping);
        Assert.Equal("search cats", opened.Breadcrumb);
    }

    /// <summary>
    ///     A blank query is turned down in the same words the command turns one down with, so the two front ends cannot
    ///     come to say different things about the same empty value.
    /// </summary>
    [Fact]
    public async Task Find_TurnsDownAQueryWithNothingInIt()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Search();

        shell.Host.Drain();

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Empty(shell.Search.Searches);
        Assert.Equal(SearchQuery.Rejection, opened.Notice);
        Assert.True(opened.NoticeIsError);
        Assert.True(opened.Screen.IsTyping);
    }

    /// <summary>An account a search turned up opens the same screen <c>a</c> opens from a post.</summary>
    [Fact]
    public async Task Press_OpensTheAccountASearchFound()
    {
        var shell = new AShell
        {
            Search = FakeInstanceSearch.Finding(accounts: [AnAccount.With(address: "alice@hachyderm.io")]),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "alice@hachyderm.io")),
        };

        var opened = await shell.Opened();

        await Found(shell, opened, "alice");
        shell.Host.Drain();

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("alice@hachyderm.io", account.Account.Address);
        Assert.Equal("alice@hachyderm.io", Assert.Single(shell.Accounts.Reads).Account.Text);
    }

    /// <summary>
    ///     A hashtag opens as a timeline on the stack rather than as the rail's own hashtag entry — which tag the rail
    ///     keeps a place for is a setting the reader wrote down, and a search result is not them changing their mind.
    /// </summary>
    [Fact]
    public async Task Press_OpensAHashtagsTimelineWithoutTouchingTheRailsOwn()
    {
        var shell = new AShell { Search = FakeInstanceSearch.Finding(accounts: [], hashtags: [AHashtag.With("cats")]) };
        var opened = await shell.Opened();

        await Found(shell, opened, "cats");
        shell.Host.Drain();

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal("cats", shell.Timelines.Reads[^1].Timeline.Hashtag);
        Assert.Equal("search cats › #cats", opened.Breadcrumb);
        Assert.Equal("Hashtag", opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Hashtag).Label);
    }

    /// <summary>A post a search found is read exactly as one on a timeline is.</summary>
    [Fact]
    public async Task Press_OpensAPostASearchFound()
    {
        var shell = new AShell
        {
            Search = FakeInstanceSearch.Finding(accounts: [], hashtags: [], posts: [APost.With(id: "110")]),
            Engagement = FakePostEngagement.Answered(APost.With(id: "110"), APost.With(id: "111")),
        };

        var opened = await shell.Opened();

        await Found(shell, opened, "sheep");
        shell.Host.Drain();

        Assert.Equal("110", opened.Screen.Picked?.Id);

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal("110", Assert.IsType<PostScreen>(opened.Screen).Post.Id);
    }

    /// <summary>The selection walks the three kinds as one list, in the order they are drawn.</summary>
    [Fact]
    public async Task Move_WalksAccountsThenHashtagsThenPosts()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await Found(shell, opened, "cats");
        shell.Host.Drain();

        var search = Assert.IsType<SearchScreen>(opened.Screen);

        Assert.NotNull(search.PickedAccount);

        opened.Move(1);

        Assert.NotNull(search.PickedHashtag);

        opened.Move(1);

        Assert.NotNull(search.Picked);

        opened.Move(1);

        // Stops at the last one rather than wrapping, the same way every other list here does.
        Assert.NotNull(search.Picked);
    }

    /// <summary>
    ///     <c>/</c> on the search screen starts a fresh prompt. The one place the key is most likely to be pressed is
    ///     the one place it must not do nothing.
    /// </summary>
    [Fact]
    public async Task Search_StartsAFreshPromptFromTheSearchScreenItself()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await Found(shell, opened, "cats");

        opened.Search();

        shell.Host.Drain();

        var again = Assert.IsType<SearchScreen>(opened.Screen);
        Assert.True(again.IsTyping);
        Assert.Empty(again.Query);
        Assert.Equal(0, again.Count);
    }

    [Fact]
    public async Task Find_SaysSoWhereNothingWasFound()
    {
        var shell = new AShell { Search = FakeInstanceSearch.FindingNothing() };
        var opened = await shell.Opened();

        await Found(shell, opened, "nobody");
        shell.Host.Drain();

        var drawn = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Nothing found for nobody", StringComparison.Ordinal));
    }

    /// <summary>
    ///     While the prompt is taking letters, the status row says only the keys that are not letters — every other one
    ///     would be typed rather than acted on.
    /// </summary>
    [Fact]
    public async Task Keys_SayOnlyWhatIsNotATypedLetterWhileTheQueryIsBeingWritten()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Search();

        shell.Host.Drain();

        Assert.Equal(["⏎", "tab"], opened.Keys.Select(key => key.Key));

        await Found(shell, opened, "cats");
        shell.Host.Drain();

        Assert.Contains(opened.Keys, key => key.Key == "/");
    }

    /// <summary>A search screen, having searched for <paramref name="text" />.</summary>
    private static async Task Found(AShell built, Shell shell, string text)
    {
        shell.Search();

        built.Host.Drain();

        foreach (var letter in text)
        {
            shell.Type(letter);
        }

        await shell.Find();

        built.Host.Drain();
    }
}
