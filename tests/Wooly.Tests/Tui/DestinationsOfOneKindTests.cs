using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Two destinations of one kind on the rail, each told apart by being itself rather than by its kind (#510): what
///     the rail goes to and what the cache holds are each its own — two tag sets, the one kind the rail holds more than
///     one of (#511), driven through a shell over fakes as the reader would drive them.
/// </summary>
public class DestinationsOfOneKindTests
{
    private static readonly TagSet Cats = TagSet.Of("cats", ["cats", "kittens"]);

    private static readonly TagSet Dogs = TagSet.Of("dogs", ["dogs", "puppies"]);

    /// <summary>Arriving at the second of two of a kind lands on it, and not on the first.</summary>
    [Fact]
    public async Task Arrive_LandsOnTheDestinationArrivedAtRatherThanTheFirstOfItsKind()
    {
        var (shell, opened) = await Opened();

        opened.Arrive(Index(opened, "dogs"));
        shell.Host.Drain();

        Assert.Equal(Index(opened, "dogs"), opened.Rail.Current);
        Assert.Equal("dogs", opened.Breadcrumb);
        Assert.Equal(Timeline.AnyOf(Dogs), shell.Timelines.Reads[^1].Timeline);
    }

    /// <summary>A destination is the same destination whatever its badge says, so going to it finds it anyway.</summary>
    [Fact]
    public async Task GoTo_FindsADestinationWhateverItsBadgeSays()
    {
        var (_, opened) = await Opened();
        var dogs = opened.Rail.Destinations[Index(opened, "dogs")];

        opened.Rail.Update(dogs with { Unread = 3 });
        opened.Rail.GoTo(dogs);

        Assert.Equal(Index(opened, "dogs"), opened.Rail.Current);
        Assert.Equal(3, opened.Rail.Showing.Unread);
    }

    /// <summary>Changing one of two of a kind changes it, and leaves the other as it was.</summary>
    [Fact]
    public async Task Update_ChangesOnlyTheDestinationItIsAbout()
    {
        var (_, opened) = await Opened();

        opened.Rail.Update(opened.Rail.Destinations[Index(opened, "dogs")] with { Unread = 3 });

        Assert.Equal(0, opened.Rail.Destinations[Index(opened, "cats")].Unread);
        Assert.Equal(3, opened.Rail.Destinations[Index(opened, "dogs")].Unread);
    }

    /// <summary>
    ///     Each keeps its own cache entry: arriving at the second reads it rather than drawing what the first held, and
    ///     coming back to the first inside the cache window draws what it held and asks for nothing.
    /// </summary>
    [Fact]
    public async Task Arrive_KeepsACacheEntryForEachDestinationOfOneKind()
    {
        var (shell, opened) = await Opened();

        opened.Arrive(Index(opened, "cats"));
        shell.Host.Drain();
        opened.Arrive(Index(opened, "dogs"));
        shell.Host.Drain();

        Assert.Equal(["cats", "dogs"], SetsRead(shell));
        Assert.Equal("dogs-post", Assert.Single(Assert.IsType<FeedScreen>(opened.Screen).Posts).Id);

        opened.Arrive(Index(opened, "cats"));
        shell.Host.Drain();

        Assert.Equal(["cats", "dogs"], SetsRead(shell));
        Assert.Equal("cats-post", Assert.Single(Assert.IsType<FeedScreen>(opened.Screen).Posts).Id);
    }

    /// <summary>Asking one of two of a kind again forgets what it held, and leaves the other's held.</summary>
    [Fact]
    public async Task Refresh_ForgetsOnlyTheDestinationAskedAgain()
    {
        var (shell, opened) = await Opened();

        opened.Arrive(Index(opened, "cats"));
        shell.Host.Drain();
        opened.Arrive(Index(opened, "dogs"));
        shell.Host.Drain();
        await opened.Refresh();
        shell.Host.Drain();
        opened.Arrive(Index(opened, "cats"));
        shell.Host.Drain();

        Assert.Equal(["cats", "dogs", "dogs"], SetsRead(shell));
        Assert.Equal("cats", opened.Breadcrumb);
    }

    /// <summary>
    ///     A read in flight for one of two of a kind is its own: the other's ending does not end it, and neither
    ///     holds back a re-read of the other.
    /// </summary>
    /// <remarks>
    ///     Asked of the reads in flight themselves rather than through the shell: what they hold back is an Inbox
    ///     count, and a tag set has none, so two sets' reads in flight are told apart nowhere the shell shows.
    /// </remarks>
    [Fact]
    public void ListReads_CountsTheReadsInFlightOfEachDestinationOfOneKindApart()
    {
        var cats = new Destination(DestinationKind.TagSet, "cats", Timeline.AnyOf(Cats));
        var dogs = new Destination(DestinationKind.TagSet, "dogs", Timeline.AnyOf(Dogs));
        var lists = new ListReads();

        lists.Began(cats);
        lists.Began(dogs);
        lists.Ended(cats);

        Assert.False(lists.Reading(cats));
        Assert.True(lists.Reading(dogs));
        Assert.False(lists.HoldsBack(cats));
    }

    /// <summary>A shell whose config file writes the two sets, each answered with a post named for it.</summary>
    private static async Task<(AShell Shell, Wooly.Tui.Shell.Shell Opened)> Opened()
    {
        var shell = new AShell
        {
            TagSets = [Cats, Dogs],
            Timelines = FakeTimelineReader.Answering(timeline =>
                Fetch<Post>.Complete([APost.With(id: $"{timeline.Set?.Name ?? "home"}-post")])),
        };

        return (shell, await shell.Opened());
    }

    /// <summary>Where on the rail the set called <paramref name="name" /> stands.</summary>
    private static int Index(Wooly.Tui.Shell.Shell shell, string name)
    {
        var at = shell.Rail.Destinations.ToList()
            .FindIndex(destination => destination.Kind == DestinationKind.TagSet && destination.Label == name);

        Assert.True(at >= 0, $"No set called {name} on the rail.");

        return at;
    }

    /// <summary>The sets read so far, by name, in the order they were read.</summary>
    private static List<string> SetsRead(AShell shell) =>
    [
        .. shell.Timelines.Reads.Where(read => read.Timeline.Set is not null).Select(read => read.Timeline.Set!.Name),
    ];
}
