using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What one person on a follow list says: their byline, and the compact standing beside it — muted, and silent
///     where there is nothing honest to say (#180).
/// </summary>
public class FollowsRowTests
{
    /// <summary>The row is the byline the search screen already draws, so a person reads one way everywhere.</summary>
    [Fact]
    public void Person_SaysWhoTheyAre()
    {
        var row = AccountLines.Person(AnAccount.With(address: "maria@fosstodon.org", author: "Maria"), 61);

        Assert.Equal("Maria @maria@fosstodon.org", Said(row));
    }

    /// <summary>
    ///     And the standing after it, in as few words as a row has room for — muted, the two-span shape the hashtag
    ///     row already uses.
    /// </summary>
    [Theory]
    [InlineData(true, false, false, false, false, "following")]
    [InlineData(false, true, false, false, false, "asked")]
    [InlineData(false, false, true, false, false, "follows you")]
    [InlineData(true, false, true, false, false, "following · follows you")]
    [InlineData(false, false, false, true, false, "blocked")]
    [InlineData(false, false, false, false, true, "muted")]
    public void Person_SaysWhereTheReaderStandsWithThem(
        bool following,
        bool requested,
        bool followedBy,
        bool blocking,
        bool muting,
        string said)
    {
        var standing = AnAccount.Standing(following, requested, followedBy, blocking, muting);

        var row = AccountLines.Person(AnAccount.With(standing: standing), 61);

        Assert.EndsWith($"  {said}", Said(row), StringComparison.Ordinal);
        Assert.Equal(Role.Muted, row.Spans[^1].Role);
    }

    /// <summary>
    ///     An account carrying no standing at all says nothing rather than saying no: the batched call that fills one
    ///     in may not have happened, or may have been refused, and a silent row is the honest one (CONTEXT.md).
    /// </summary>
    [Fact]
    public void Person_SaysNothingWhereTheInstanceWasNeverAsked()
    {
        var row = AccountLines.Person(AnAccount.With(author: "Maria", address: "maria@fosstodon.org"), 61);

        Assert.Equal("Maria @maria@fosstodon.org", Said(row));
    }

    /// <summary>And nothing where the reader stands in no relation to them at all, which is a row with room to spare.</summary>
    [Fact]
    public void Person_SaysNothingWhereThereIsNoTieEitherWay()
    {
        var row = AccountLines.Person(AnAccount.With(author: "Maria", address: "m@f.org", standing: AnAccount.Standing()), 61);

        Assert.Equal("Maria @m@f.org", Said(row));
    }

    /// <summary>
    ///     A row never says what the list it is on already says: every row of your own following list is somebody you
    ///     follow, so the words go and what is left is the direction you cannot infer.
    /// </summary>
    [Fact]
    public void Person_LeavesOutWhatTheListAlreadySays()
    {
        var standing = AnAccount.Standing(following: true, followedBy: true);
        var account = AnAccount.With(standing: standing);

        Assert.EndsWith("  follows you", Said(AccountLines.Person(account, 61, Implied.YouFollowThem)), StringComparison.Ordinal);
        Assert.EndsWith("  following", Said(AccountLines.Person(account, 61, Implied.TheyFollowYou)), StringComparison.Ordinal);
    }

    /// <summary>
    ///     And where that is all there was to say, the gap is the signal — which is the whole of what your own
    ///     followers list is read for: who has not been followed back.
    /// </summary>
    [Fact]
    public void Person_IsSilentWhereTheListSaidTheWholeOfIt()
    {
        var standing = AnAccount.Standing(followedBy: true);

        var row = AccountLines.Person(
            AnAccount.With(author: "Maria", address: "m@f.org", standing: standing),
            61,
            Implied.TheyFollowYou);

        Assert.Equal("Maria @m@f.org", Said(row));
    }

    /// <summary>The standing is never cut off the row, being the shorter half and the half a wide name would eat.</summary>
    [Fact]
    public void Person_KeepsTheStandingWhereTheNameIsLong()
    {
        var account = AnAccount.With(
            author: new string('a', 80),
            address: "verylongaddress@some.instance.example",
            standing: AnAccount.Standing(following: true));

        var row = AccountLines.Person(account, 30);

        Assert.EndsWith("  following", Said(row), StringComparison.Ordinal);
        Assert.True(Said(row).Length <= 30);
    }

    /// <summary>The whole row as one string, which is what a reader sees.</summary>
    private static string Said(Wooly.Tui.Rendering.Line line) => string.Concat(line.Spans.Select(span => span.Text));
}
