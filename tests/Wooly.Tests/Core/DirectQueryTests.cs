using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     What is typed into search when it names one thing rather than asks after something. A tag needs no asking, since
///     any well-formed tag has a timeline; a handle or an address is found only by an exact match among what the search
///     that was asked anyway turned up.
/// </summary>
public class DirectQueryTests
{
    [Theory]
    [InlineData("#cats", "cats")]
    [InlineData("  #cats  ", "cats")]
    [InlineData("#caturday_2026", "caturday_2026")]

    // A tag in another script is as well-formed a tag as one in English, so it is as direct.
    [InlineData("#日本語", "日本語")]
    [InlineData("#γάτες", "γάτες")]
    public void Tag_NamesTheTagAHashtagIsDirectFor(string typed, string tag) =>
        Assert.Equal(tag, DirectQuery.Tag(typed));

    [Theory]
    [InlineData("")]

    // A word with no sign on it is how a reader asks for everything that matches it.
    [InlineData("cats")]

    // A phrase is asked after, not named.
    [InlineData("#cats dogs")]

    // What is left of starting a tag and not finishing it.
    [InlineData("#")]

    // Two hashes is not how a tag is written, though the timeline's rule would strip both.
    [InlineData("##cats")]

    // Not one word, so not a tag the timeline would take.
    [InlineData("#cats/../../instance")]

    // A handle is direct too, but it names an account rather than a tag.
    [InlineData("@alice")]
    public void Tag_IsNothingForWhatIsSearchedFor(string typed) => Assert.Null(DirectQuery.Tag(typed));

    [Theory]
    [InlineData("@alice@hachyderm.io", "alice@hachyderm.io")]
    [InlineData("alice@hachyderm.io", "alice@hachyderm.io")]
    [InlineData("@alice", "alice")]
    [InlineData("  @alice  ", "alice")]
    public void Handle_NamesTheAccountAHandleIsDirectFor(string typed, string handle) =>
        Assert.Equal(handle, DirectQuery.Handle(typed)?.Text);

    [Theory]
    [InlineData("")]

    // A word with no @ in it is how a reader asks for everything that resembles it.
    [InlineData("alice")]

    // A phrase is asked after, not named.
    [InlineData("@alice hello")]

    // One or two parts, never three.
    [InlineData("@a@b@c")]

    // What is left of starting a handle and not finishing it.
    [InlineData("@")]
    [InlineData("alice@")]

    // An address with an @ in its path is an address, not a handle.
    [InlineData("https://hachyderm.io/@alice")]
    [InlineData("hachyderm.io/@alice")]
    [InlineData("#cats")]

    // A tag with an @ in it is a malformed tag, not somebody's handle.
    [InlineData("#a@b")]
    public void Handle_IsNothingForWhatIsNotAHandle(string typed) => Assert.Null(DirectQuery.Handle(typed));

    [Theory]
    [InlineData("https://hachyderm.io/@alice/110", "https://hachyderm.io/@alice/110")]
    [InlineData("http://hachyderm.io/@alice", "http://hachyderm.io/@alice")]
    [InlineData("  https://hachyderm.io/@alice  ", "https://hachyderm.io/@alice")]

    // The elided form an address is so often written in is read as https, as opening a link reads it.
    [InlineData("hachyderm.io/@alice/110", "https://hachyderm.io/@alice/110")]
    [InlineData("www.example.com/notes", "https://www.example.com/notes")]
    public void Address_NamesThePageAnAddressIsDirectFor(string typed, string address) =>
        Assert.Equal(address, DirectQuery.Address(typed)?.AbsoluteUri);

    [Theory]
    [InlineData("")]
    [InlineData("cats")]

    // A word with a dot in it is a word somebody is searching for, not a page: a post or a profile has a path.
    [InlineData("node.js")]
    [InlineData("3.14")]
    [InlineData("hachyderm.io")]

    // An address with words after it is a phrase that mentions one.
    [InlineData("https://hachyderm.io/@alice look at this")]

    // Not a scheme this client opens.
    [InlineData("ftp://hachyderm.io/@alice")]
    [InlineData("javascript:alert(1)")]
    [InlineData("alice@hachyderm.io")]
    [InlineData("@alice")]
    [InlineData("#cats")]
    public void Address_IsNothingForWhatIsNotAnAddress(string typed) => Assert.Null(DirectQuery.Address(typed));

    /// <summary>
    ///     An address is searched for whole, so an elided one is resolved as the page it names rather than searched
    ///     for as words; anything else is searched for as typed.
    /// </summary>
    [Theory]
    [InlineData("hachyderm.io/@alice/110", "https://hachyderm.io/@alice/110")]
    [InlineData("https://hachyderm.io/@alice/110", "https://hachyderm.io/@alice/110")]
    [InlineData("  cats  ", "cats")]
    [InlineData("@alice", "@alice")]
    public void Asking_IsWhatIsSearchedFor(string typed, string asked) =>
        Assert.Equal(asked, DirectQuery.Asking(typed));

    [Theory]
    [InlineData("@alice@hachyderm.io")]
    [InlineData("alice@hachyderm.io")]
    [InlineData("@ALICE@Hachyderm.io")]
    public void Among_OpensTheAccountWhoseAddressAHandleMatches(string typed)
    {
        var alice = AnAccount.With(address: "alice@hachyderm.io", id: "1");
        var found = Found(accounts: [AnAccount.With(address: "alicia@hachyderm.io", id: "2"), alice]);

        var named = DirectQuery.Among(typed, found, "mastodon.social");

        Assert.Equal(alice, Assert.IsType<DirectQuery.Named.OfAccount>(named).Account);
    }

    /// <summary>A bare handle is read against the profile's own instance, as a tie is put on one.</summary>
    [Fact]
    public void Among_ReadsABareHandleAgainstTheProfilesOwnInstance()
    {
        var local = AnAccount.With(address: "alice@mastodon.social", id: "1");
        var found = Found(accounts: [AnAccount.With(address: "alice@hachyderm.io", id: "2"), local]);

        var named = DirectQuery.Among("@alice", found, "mastodon.social");

        Assert.Equal(local, Assert.IsType<DirectQuery.Named.OfAccount>(named).Account);
    }

    /// <summary>A near miss has found somebody else, and is listed rather than opened.</summary>
    [Theory]
    [InlineData("alice@hachyderm.io")]
    [InlineData("@alice")]
    public void Among_IsNothingWhereNoAccountMatchesExactly(string typed)
    {
        var found = Found(accounts: [AnAccount.With(address: "alicia@hachyderm.io")]);

        Assert.Null(DirectQuery.Among(typed, found, "mastodon.social"));
    }

    /// <summary>An address resolves to one thing, and that one thing is what it named.</summary>
    [Fact]
    public void Among_OpensThePostAnAddressResolvedTo()
    {
        var post = APost.With(id: "110");

        var named = DirectQuery.Among("mastodon.social/@jeff/110", Found(posts: [post]), "mastodon.social");

        Assert.Equal(post, Assert.IsType<DirectQuery.Named.OfPost>(named).Post);
    }

    [Fact]
    public void Among_OpensTheAccountAProfilesAddressResolvedTo()
    {
        var alice = AnAccount.With(address: "alice@hachyderm.io");

        var named = DirectQuery.Among("https://hachyderm.io/@alice", Found(accounts: [alice]), "mastodon.social");

        Assert.Equal(alice, Assert.IsType<DirectQuery.Named.OfAccount>(named).Account);
    }

    /// <summary>
    ///     A page that is neither a post nor a profile resolves to nothing, and an answer holding more than one thing
    ///     is not an answer the address resolved to.
    /// </summary>
    [Fact]
    public void Among_IsNothingWhereAnAddressResolvedToNoOneThing()
    {
        const string address = "https://example.com/notes";

        Assert.Null(DirectQuery.Among(address, Found(), "mastodon.social"));
        Assert.Null(DirectQuery.Among(address, Found(posts: [APost.With(id: "1"), APost.With(id: "2")]), "mastodon.social"));
        Assert.Null(DirectQuery.Among(
            address,
            Found(accounts: [AnAccount.With()], posts: [APost.With()]),
            "mastodon.social"));
        Assert.Null(DirectQuery.Among(address, Found(hashtags: [AHashtag.With("notes")]), "mastodon.social"));
    }

    /// <summary>What is not a handle or an address is never opened, whatever it found.</summary>
    [Theory]
    [InlineData("alice")]
    [InlineData("#cats")]
    [InlineData("@alice hello")]
    public void Among_IsNothingForWhatIsSearchedFor(string typed)
    {
        var found = Found(accounts: [AnAccount.With(address: "alice@mastodon.social")]);

        Assert.Null(DirectQuery.Among(typed, found, "mastodon.social"));
    }

    private static SearchResults Found(
        Account[]? accounts = null,
        Hashtag[]? hashtags = null,
        Post[]? posts = null) =>
        SearchResults.Matching(SearchKind.Everything, accounts ?? [], hashtags ?? [], posts ?? []);
}
