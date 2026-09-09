using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Account block: what one person looks like wherever they are merely listed — four rows beside their avatar,
///     the fourth carrying the joined month, the flags and whatever the screen drawing it has to add (#198).
/// </summary>
/// <remarks>
///     Held at line level with no terminal in the room, the way the header block's own rows are: what a screen adds to
///     row 4 is the screen's, and that it lands there muted and <c>·</c>-joined is this module's.
/// </remarks>
public class AccountBlockTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The block's rows, drawn as a listing screen with no terminal draws them.</summary>
    private static IReadOnlyList<Line> Block(
        Account account,
        string said = "",
        int width = 61,
        IPictures? pictures = null) =>
        AccountLines.Block(account, new Drawing(width, Now, pictures), said);

    /// <summary>
    ///     Who they are, on four rows in that order: the name, the handle, how much of a presence they have, and the
    ///     month they arrived — which is the account screen's own opening, drawn here so a person reads one way
    ///     everywhere.
    /// </summary>
    [Fact]
    public void Block_IsTheNameTheHandleThePresenceAndTheMonthTheyJoined()
    {
        var lines = Block(AnAccount.With(
            address: "maria@fosstodon.org",
            author: "Maria",
            posts: 18462,
            following: 5,
            followers: 509,
            joined: new DateOnly(2023, 7, 14)));

        Assert.Equal(
            ["Maria", "@maria@fosstodon.org", "18,462 posts · 5 following · 509 followers", "Joined Jul 2023"],
            lines.Select(line => line.Text));
    }

    /// <summary>Row 4 is muted throughout, being a list of small facts rather than anything the reader acts on.</summary>
    [Fact]
    public void Block_MutesTheFactsRow() =>
        Assert.All(Block(AnAccount.With()) [3].Spans, span => Assert.Equal(Role.Muted, span.Role));

    /// <summary>What a screen has to say about somebody joins that row rather than starting a fifth.</summary>
    [Fact]
    public void Block_PutsWhatTheScreenSaysOnTheFactsRow() =>
        Assert.Equal(
            "Joined Jan 2020 · following · follows you",
            Block(AnAccount.With(), "following · follows you")[3].Text);

    /// <summary>And the flags stand between the two, so what is true of the person comes before what is true of the pair.</summary>
    [Fact]
    public void Block_PutsTheFlagsBeforeWhatTheScreenSays() =>
        Assert.Equal(
            "Joined Jan 2020 · ⚙ bot · ⚿ locked · following",
            Block(AnAccount.With(isBot: true, isLocked: true), "following")[3].Text);

    /// <summary>
    ///     A screen with nothing to say leaves the row as it stands, rather than a separator hanging off the end of
    ///     it — which is every row of a search result and of a follow request today.
    /// </summary>
    [Fact]
    public void Block_LeavesTheFactsRowAloneWhereTheScreenSaysNothing() =>
        Assert.Equal("Joined Jan 2020 · ⚿ locked", Block(AnAccount.With(isLocked: true))[3].Text);

    /// <summary>
    ///     Where the terminal cannot paint, the avatar's columns are reclaimed rather than held open — what #62
    ///     settled on a byline, at the header's own size.
    /// </summary>
    [Fact]
    public void Block_ReclaimsTheAvatarsColumnsWhereNoPictureIsComing()
    {
        var lines = Block(AnAccount.With(), pictures: FakePictures.DrawingNothing());

        Assert.Equal("Alice", lines[0].Text);
        Assert.DoesNotContain(lines, line => line.Has(Role.Media));
    }

    /// <summary>
    ///     And where it can, the header's own 8×4 box stands beside the four rows — the same box, not a third size:
    ///     a size named at a call site is a size that can drift from the one beside it.
    /// </summary>
    [Fact]
    public void Block_SetsTheFourRowsBesideTheHeadersOwnEightByFourAvatar()
    {
        var lines = Block(AnAccount.With(), pictures: FakePictures.With().HoldingAvatarOf("alice@hachyderm.io", 96, 96));

        var inset = Assert.Single(lines[0].Insets);

        Assert.Equal(4, lines.Count);
        Assert.Equal(8, inset.Columns);
        Assert.Equal(4, inset.Rows);
        Assert.All(lines, line => Assert.StartsWith("          ", line.Text));
        Assert.All(lines.Skip(1), line => Assert.Empty(line.Insets));
    }

    /// <summary>No row runs past the width, however long a name, a handle or what the screen has to say is.</summary>
    [Fact]
    public void Block_ReadsAtSixtyOneColumns()
    {
        var lines = Block(
            AnAccount.With(
                address: "somebody@an-extremely-long-instance-domain.example",
                author: "Somebody With A Very Long Display Name Indeed, Truly",
                isBot: true,
                isLocked: true),
            "following · follows you · blocked · muted",
            pictures: FakePictures.With().HoldingAvatarOf("somebody@an-extremely-long-instance-domain.example"));

        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }

    /// <summary>
    ///     And what the screen said is never the half that goes: the facts are what a reader still recognises
    ///     clipped, since a joined month cut short is still a joined month and <c>follo</c> is not a standing (#180).
    /// </summary>
    [Fact]
    public void Block_KeepsWhatTheScreenSaidWhereTheRowIsNarrow()
    {
        var lines = Block(
            AnAccount.With(isBot: true, isLocked: true),
            "following · follows you",
            pictures: FakePictures.With().HoldingAvatarOf("alice@hachyderm.io"));

        Assert.EndsWith("following · follows you", lines[3].Text, StringComparison.Ordinal);
        Assert.True(lines[3].Width <= 61);
    }

    /// <summary>
    ///     And where that leaves the facts no room at all, the row is what the screen said and nothing else — rather
    ///     than a separator with nothing in front of it.
    /// </summary>
    [Fact]
    public void Block_DropsTheFactsWhereWhatTheScreenSaidFillsTheRow()
    {
        const string Said = "following · follows you · blocked · muted";

        Assert.Equal(Said, Block(AnAccount.With(), Said, width: Said.Length)[3].Text);
    }

    /// <summary>
    ///     Where the reader stands with them, in as few words as a row has room for — what a follow list puts on row
    ///     4, and the same five facts the account screen says as a sentence.
    /// </summary>
    [Theory]
    [InlineData(true, false, false, false, false, "following")]
    [InlineData(false, true, false, false, false, "asked")]
    [InlineData(false, false, true, false, false, "follows you")]
    [InlineData(true, false, true, false, false, "following · follows you")]
    [InlineData(false, false, false, true, false, "blocked")]
    [InlineData(false, false, false, false, true, "muted")]
    public void Compact_SaysWhereTheReaderStandsWithThem(
        bool following,
        bool requested,
        bool followedBy,
        bool blocking,
        bool muting,
        string said) =>
        Assert.Equal(
            said,
            AccountLines.Compact(AnAccount.Standing(following, requested, followedBy, blocking, muting)));

    /// <summary>
    ///     An account carrying no standing at all says nothing rather than saying no: the batched call that fills one
    ///     in may not have happened, or may have been refused, and a silent row is the honest one (CONTEXT.md).
    /// </summary>
    [Fact]
    public void Compact_SaysNothingWhereTheInstanceWasNeverAsked() => Assert.Equal(string.Empty, AccountLines.Compact(null));

    /// <summary>And nothing where the reader stands in no relation to them at all.</summary>
    [Fact]
    public void Compact_SaysNothingWhereThereIsNoTieEitherWay() =>
        Assert.Equal(string.Empty, AccountLines.Compact(AnAccount.Standing()));

    /// <summary>
    ///     A row never says what the list it is on already says: every row of your own following list is somebody you
    ///     follow, so the words go and what is left is the direction you cannot infer.
    /// </summary>
    [Fact]
    public void Compact_LeavesOutWhatTheListAlreadySays()
    {
        var standing = AnAccount.Standing(following: true, followedBy: true);

        Assert.Equal("follows you", AccountLines.Compact(standing, Implied.YouFollowThem));
        Assert.Equal("following", AccountLines.Compact(standing, Implied.TheyFollowYou));
    }

    /// <summary>
    ///     And where that is all there was to say, the gap is the signal — which is the whole of what your own
    ///     followers list is read for: who has not been followed back.
    /// </summary>
    [Fact]
    public void Compact_IsSilentWhereTheListSaidTheWholeOfIt() =>
        Assert.Equal(string.Empty, AccountLines.Compact(AnAccount.Standing(followedBy: true), Implied.TheyFollowYou));

    /// <summary>
    ///     A follow still waiting to be let in is not the claim a following list makes of everyone on it, so it
    ///     survives where a follow in place would be dropped.
    /// </summary>
    [Fact]
    public void Compact_KeepsAWaitingFollowThatTheListDidNotClaim() =>
        Assert.Equal(
            "asked",
            AccountLines.Compact(AnAccount.Standing(followRequested: true), Implied.YouFollowThem));
}
