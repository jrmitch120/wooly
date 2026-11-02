using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     The one way the media path holds what it prepares ahead — a sixel's cuts and a picture taken to its box (#554):
///     each held as its hold policy says, prepared elsewhere, waited on by a frame that asks for one being prepared,
///     and settled whatever happens to it.
/// </summary>
public class PreparedAheadTests
{
    /// <summary>
    ///     A cut let go of while it is being encoded, and prepared again before the first is done, is the second's to
    ///     settle: the first finishing takes nothing off the list and holds nothing, so a frame asking for it waits on
    ///     the second rather than being handed what was let go of. As the cuts of a sixel are held.
    /// </summary>
    [Fact]
    public void APreparedValueLetGoOfAndPreparedAgainIsTheSecondsToSettle()
    {
        var elsewhere = new List<Action>();
        var made = 0;
        var prepared = HeldAsCuts(elsewhere.Add, () => $"{++made}");

        prepared.Prepare("cut", 0);
        prepared.LetGoOf(key => key == "cut");
        prepared.Prepare("cut", 0);
        elsewhere[0]();

        // Still listed, so it is not prepared a third time.
        prepared.Prepare("cut", 0);
        Assert.Equal(2, elsewhere.Count);

        elsewhere[1]();

        Assert.Equal("2", prepared.Of("cut", 0));
        Assert.Equal(2, made);
    }

    /// <summary>The same where the first fails: its failure takes nothing off the list either.</summary>
    [Fact]
    public void APreparedValueLetGoOfThatFailsLeavesTheOnePreparedAgainListed()
    {
        var elsewhere = new List<Action>();
        var made = 0;
        var prepared = HeldAsCuts(
            elsewhere.Add,
            () => ++made == 1 ? throw new InvalidOperationException("No encoder today.") : $"{made}");

        prepared.Prepare("cut", 0);
        prepared.LetGoOf(key => key == "cut");
        prepared.Prepare("cut", 0);
        elsewhere[0]();

        prepared.Prepare("cut", 0);
        Assert.Equal(2, elsewhere.Count);

        elsewhere[1]();

        Assert.Equal("2", prepared.Of("cut", 0));
    }

    /// <summary>
    ///     An out-of-memory is settled like any failure, so that a frame waiting on it is not left waiting for ever —
    ///     and thrown on rather than swallowed, and not tried again on the frame.
    /// </summary>
    [Fact]
    public async Task AnOutOfMemoryIsSettledAndThrownOn()
    {
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        Task? preparing = null;
        var prepared = HeldAsCuts(
            work => preparing = Task.Run(work),
            () =>
            {
                started.Set();
                release.Wait();

                throw new OutOfMemoryException();
            });

        prepared.Prepare("cut", 0);
        started.Wait(TestContext.Current.CancellationToken);

        var asked = Task.Run(() => prepared.Of("cut", 0), TestContext.Current.CancellationToken);

        release.Set();

        await Assert.ThrowsAsync<OutOfMemoryException>(() => preparing!);
        await Assert.ThrowsAsync<OutOfMemoryException>(() => asked);
    }

    /// <summary>
    ///     Held least recently used first, as a sixel's cuts are: past its room the one used longest ago goes, and one
    ///     asked for again is the last to.
    /// </summary>
    [Fact]
    public void AValueUsedAgainIsHeldLongerThanOneUsedLongerAgo()
    {
        var made = new List<string>();
        var prepared = new PreparedAhead<string, int, string>(
            (key, _) =>
            {
                made.Add(key);

                return key;
            },
            work => work(),
            new LeastRecentlyUsed<string>(most: 2));

        prepared.Of("a", 0);
        prepared.Of("b", 0);
        prepared.Of("a", 0);
        prepared.Of("c", 0);
        prepared.Of("a", 0);
        prepared.Of("b", 0);

        Assert.Equal(["a", "b", "c", "b"], made);
    }

    /// <summary>A key counted apart is held in room of its own, and lets go of none of the others (ADR-0035).</summary>
    [Fact]
    public void AValueCountedApartLetsGoOfNoneOfTheOthers()
    {
        var made = new List<string>();
        var prepared = new PreparedAhead<string, int, string>(
            (key, _) =>
            {
                made.Add(key);

                return key;
            },
            work => work(),
            new LeastRecentlyUsed<string>(most: 1, apart: key => key.StartsWith(':'), mostApart: 2));

        prepared.Of("a", 0);
        prepared.Of(":e1:", 0);
        prepared.Of(":e2:", 0);
        prepared.Of(":e3:", 0);
        prepared.Of("a", 0);
        prepared.Of(":e2:", 0);

        Assert.Equal(["a", ":e1:", ":e2:", ":e3:"], made);
    }

    /// <summary>Held as a sixel's cuts are, the work being <paramref name="make" />.</summary>
    private static PreparedAhead<string, int, string> HeldAsCuts(Action<Action> elsewhere, Func<string> make) =>
        new(
            (_, _) => make(),
            elsewhere,
            new LeastRecentlyUsed<string>(SixelPictures.MostHeld, _ => false, SixelPictures.MostCustomEmojiHeld));
}
