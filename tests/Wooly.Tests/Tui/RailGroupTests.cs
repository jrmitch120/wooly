using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every destination belongs to one rail group, by what it is for (GLOSSARY.md, ADR-0021): the timelines you read,
///     the tools you go looking with, what comes to you, and your own account.
/// </summary>
/// <remarks>
///     Which group each fixed destination is in is said once, with the rail's order, by
///     <see cref="RailFetchTests.Rail_ListsTheFixedDestinationsInTheirGroups_WhereThereAreNoTagSets" />, and a tag set's
///     by <see cref="RailFetchTests.Rail_ListsEachTagSetBetweenTheTimelinesAndExplore_InTheOrderWritten" /> (#559).
/// </remarks>
public class RailGroupTests
{
    /// <summary>Every kind there is has a group: a new destination cannot arrive without being placed.</summary>
    [Fact]
    public void Group_IsSaidForEveryKind() =>
        Assert.All(
            Enum.GetValues<DestinationKind>(),
            kind => Assert.True(Enum.IsDefined(new Destination(kind, kind.ToString()).Group)));
}
