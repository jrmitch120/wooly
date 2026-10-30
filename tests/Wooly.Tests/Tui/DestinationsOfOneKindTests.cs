using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Two destinations of one kind on the rail, each told apart by being itself rather than by its kind (#510): what
///     the rail goes to, what the cache holds and which reads are in flight are each its own. Two hashtag destinations
///     stand in for what will be two tag sets (#509).
/// </summary>
public class DestinationsOfOneKindTests
{
    private static readonly Destination Home = new(DestinationKind.Home, "Home", Timeline.Home);

    private static readonly Destination Cats = new(DestinationKind.Hashtag, "#cats", Timeline.Tag("cats"));

    private static readonly Destination Dogs = new(DestinationKind.Hashtag, "#dogs", Timeline.Tag("dogs"));

    /// <summary>Going to the second of two of a kind lands on it, and not on the first.</summary>
    [Fact]
    public void GoTo_LandsOnTheDestinationGoneToRatherThanTheFirstOfItsKind()
    {
        var rail = new Rail([Home, Cats, Dogs], new FakeShellHost(), TimeSpan.Zero);
        var selected = new List<Destination>();
        rail.Selected += selected.Add;

        rail.GoTo(Dogs);

        Assert.Equal(2, rail.Cursor);
        Assert.Equal(2, rail.Current);
        Assert.Equal("#dogs", Assert.Single(selected).Label);
    }

    /// <summary>A destination is the same destination whatever its badge says, so going to it finds it anyway.</summary>
    [Fact]
    public void GoTo_FindsADestinationWhateverItsBadgeSays()
    {
        var rail = new Rail([Home, Cats, Dogs], new FakeShellHost(), TimeSpan.Zero);

        rail.Update(Dogs with { Unread = 3 });
        rail.GoTo(Dogs);

        Assert.Equal(2, rail.Current);
        Assert.Equal(3, rail.Showing.Unread);
    }

    /// <summary>Changing one of two of a kind changes it, and leaves the other as it was.</summary>
    [Fact]
    public void Update_ChangesOnlyTheDestinationItIsAbout()
    {
        var rail = new Rail([Home, Cats, Dogs], new FakeShellHost(), TimeSpan.Zero);

        rail.Update(Dogs with { Unread = 3 });

        Assert.Equal(0, rail.Destinations[1].Unread);
        Assert.Equal(3, rail.Destinations[2].Unread);
    }

    /// <summary>
    ///     Each keeps its own cache entry: arriving at the second reads it rather than drawing what the first held, and
    ///     coming back to the first inside the cache window draws what it held and asks for nothing.
    /// </summary>
    [Fact]
    public void At_KeepsACacheEntryForEachDestinationOfOneKind()
    {
        var arriving = new Arriving();

        arriving.At(Cats);
        arriving.At(Dogs);

        Assert.Equal(["cats", "dogs"], arriving.Fakes.Timelines.Reads.Select(read => read.Timeline.Hashtag));
        Assert.Equal("dogs-post", Assert.Single(arriving.Showing.Posts).Id);

        arriving.At(Cats);

        Assert.Equal(2, arriving.Fakes.Timelines.Reads.Count);
        Assert.Equal("cats-post", Assert.Single(arriving.Showing.Posts).Id);
    }

    /// <summary>Asking one of two of a kind again forgets what it held, and leaves the other's held.</summary>
    [Fact]
    public void Again_ForgetsOnlyTheDestinationAskedAgain()
    {
        var arriving = new Arriving();

        arriving.At(Cats);
        arriving.At(Dogs);
        arriving.Again(Dogs);
        arriving.At(Cats);

        Assert.Equal(["cats", "dogs", "dogs"], arriving.Fakes.Timelines.Reads.Select(read => read.Timeline.Hashtag));
    }

    /// <summary>A list read in flight is said to be the destination's it is reading, not its kind's.</summary>
    [Fact]
    public void At_SaysWhichDestinationOfAKindItsListReadIsFor()
    {
        var arriving = new Arriving();
        var reading = new List<Destination>();
        var read = new List<Destination>();
        arriving.It.ListReading += reading.Add;
        arriving.It.ListRead += read.Add;

        arriving.At(Cats);
        arriving.At(Dogs);

        Assert.Equal(["#cats", "#dogs"], reading.Select(destination => destination.Label));
        Assert.Equal(["#cats", "#dogs"], read.Select(destination => destination.Label));
    }

    /// <summary>
    ///     A read in flight for one of two of a kind is its own: the other's ending does not end it, and neither
    ///     holds back a re-read of the other.
    /// </summary>
    [Fact]
    public void ListReads_CountsTheReadsInFlightOfEachDestinationOfOneKindApart()
    {
        var lists = new ListReads();

        lists.Began(Cats);
        lists.Began(Dogs);
        lists.Ended(Cats);

        Assert.False(lists.Reading(Cats));
        Assert.True(lists.Reading(Dogs));
        Assert.False(lists.HoldsBack(Cats));
    }

    /// <summary>An arrival over the shell builder's fakes, with no shell: what it puts up, in order.</summary>
    private sealed class Arriving
    {
        private readonly List<Screen> _up = [];

        public Arriving()
        {
            Fakes.Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete([APost.With(id: $"{timeline.Hashtag}-post")]));

            var rail = new Rail([Home, Cats, Dogs], Fakes.Host, TimeSpan.Zero);
            var enquiry = new Enquiry(
                Fakes.Host,
                Fakes.Clock,
                Fakes.Timing.CountdownStep,
                Fakes.Timing.MarkStep,
                () => _up.Count == 0 ? new FeedScreen(Home, []) : _up[^1]);

            It = new Arrival(
                Fakes.Profile,
                Fakes.Ports,
                enquiry,
                new SubjectCache(Fakes.Clock, Fakes.Timing.CacheFor),
                rail,
                new SessionMarkers());

            It.Arrives += _up.Add;
            It.Refreshes += _up.Add;
        }

        public AShell Fakes { get; } = new();

        public Arrival It { get; }

        /// <summary>The feed screen in front.</summary>
        public FeedScreen Showing => Assert.IsType<FeedScreen>(_up[^1]);

        public void At(Destination destination)
        {
            _ = It.At(destination);
            Fakes.Host.Drain();
        }

        public void Again(Destination destination)
        {
            _ = It.Again(new Subject.Destination(destination));
            Fakes.Host.Drain();
        }
    }
}
