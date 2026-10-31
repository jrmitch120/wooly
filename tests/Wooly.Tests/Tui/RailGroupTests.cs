using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every destination belongs to one rail group, by what it is for (GLOSSARY.md, ADR-0021): the timelines you read,
///     the tools you go looking with, what comes to you, and your own account.
/// </summary>
public class RailGroupTests
{
    /// <summary>Each kind with the group ADR-0021 and ADR-0033 put it in.</summary>
    public static TheoryData<DestinationKind, RailGroup> Groups => new()
    {
        { DestinationKind.Home, RailGroup.Timelines },
        { DestinationKind.Local, RailGroup.Timelines },
        { DestinationKind.Federated, RailGroup.Timelines },
        { DestinationKind.TagSet, RailGroup.Hashtags },
        { DestinationKind.Discover, RailGroup.Explore },
        { DestinationKind.Search, RailGroup.Explore },
        { DestinationKind.Notifications, RailGroup.Inbox },
        { DestinationKind.Messages, RailGroup.Inbox },
        { DestinationKind.Requests, RailGroup.Inbox },
        { DestinationKind.Profile, RailGroup.You },
    };

    /// <summary>The five groups hold exactly the destinations ADR-0021 and ADR-0033 list.</summary>
    [Theory]
    [MemberData(nameof(Groups))]
    public void Group_IsTheOneADR0021PutsTheDestinationIn(DestinationKind kind, RailGroup group) =>
        Assert.Equal(group, new Destination(kind, kind.ToString()).Group);

    /// <summary>Every kind there is has a group: an eleventh destination cannot arrive without being placed.</summary>
    [Fact]
    public void Group_IsSaidForEveryKind() =>
        Assert.Equal(Enum.GetValues<DestinationKind>().Length, Groups.Count);
}
