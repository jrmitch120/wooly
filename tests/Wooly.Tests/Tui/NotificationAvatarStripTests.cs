using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <b>Notification group</b>'s avatar strip (#454), layout A7b of #450: its actors' faces at a byline's 4×2 on
///     their own two rows under the names line, indented two columns with two between faces, as many as the width
///     allows up to the sample the instance sent, then a muted <c>+N</c> on the top row for the rest. Read from the
///     shell; drawing the pixels is the picture tests' business.
/// </summary>
public class NotificationAvatarStripTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 5;

    /// <summary>
    ///     The strip stands between the names line and the post, and wants each actor's face in the order the instance
    ///     gave them.
    /// </summary>
    [Fact]
    public async Task AGroupsStripWantsItsActorsFacesInTheInstancesOrder()
    {
        var screen = await Arrived(ANotification.Group(count: 3, actors: ["Carol", "Alice", "Bob"]));

        var lines = screen.Lines(Drawing(61));

        Assert.StartsWith("Carol, Alice and 1 other", lines[0].Text[1..], StringComparison.Ordinal);
        Assert.Equal(
            ["carol@hachyderm.io", "alice@hachyderm.io", "bob@hachyderm.io"],
            Faces(lines[1]));
        Assert.Empty(Faces(lines[2]));
        Assert.Contains("Hello world", lines[3].Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     More actors than there is room for: the strip shows as many as fit, in order, and ends its top row in a muted
    ///     <c>+N</c> counting the rest of the group by the instance's count — not by how many actors it sent.
    /// </summary>
    [Fact]
    public async Task AStripWithMoreActorsThanRoomEndsInAMutedCountOfTheRest()
    {
        var screen = await Arrived(ANotification.Group(
            count: 40,
            actors: ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8"]));

        // 40 columns: the gutter and the indent leave 37, which is room for five faces and a count.
        var top = screen.Lines(Drawing(40))[1];

        Assert.Equal(["a1@hachyderm.io", "a2@hachyderm.io", "a3@hachyderm.io", "a4@hachyderm.io", "a5@hachyderm.io"], Faces(top));
        Assert.Equal(new Span("+35", Role.Muted), top.Spans[^1]);
        Assert.EndsWith("  +35", top.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     However many are left over, the count fits the columns held for it after the last face the width has room
    ///     for — exact while it fits, then in thousands, millions or billions, rounded down so it never says more than
    ///     there are — so a group growing past a thousand never pushes the row past its width.
    /// </summary>
    [Theory]
    [InlineData(999, "+999")]
    [InlineData(9_999, "+9999")]
    [InlineData(12_345, "+12k")]
    [InlineData(999_999, "+999k")]
    [InlineData(1_500_000, "+1M")]
    [InlineData(2_000_000_000, "+2G")]
    public async Task TheCountOfTheRestAlwaysFitsItsColumns(int rest, string drawn)
    {
        var screen = await Arrived(ANotification.Group(
            count: 5 + rest,
            actors: ["A1", "A2", "A3", "A4", "A5", "A6"]));

        // 38 columns: the gutter and the indent leave 35, which is room for five faces and a count of five columns.
        var top = screen.Lines(Drawing(38))[1];

        Assert.Equal(5, Faces(top).Count);
        Assert.Equal(new Span(drawn, Role.Muted), top.Spans[^1]);
        Assert.True(top.Width <= 38, $"{top.Text} is {top.Width} columns");
    }

    /// <summary>A group of one shows its one face, and no count, since nobody is left over.</summary>
    [Fact]
    public async Task AGroupOfOneShowsOneFaceAndNoCount()
    {
        var screen = await Arrived(ANotification.Group(kind: NotificationKind.Follow, actors: ["Alice"], noPost: true));

        var lines = screen.Lines(Drawing(61));

        Assert.Equal(["alice@hachyderm.io"], Faces(lines[1]));
        Assert.DoesNotContain("+", lines[1].Text, StringComparison.Ordinal);
        Assert.Equal(3, lines.Count(line => line.Item == 0));
    }

    /// <summary>
    ///     Where this terminal draws no pictures — or no terminal is in the room — the strip takes no rows: the post
    ///     follows the names line straight away.
    /// </summary>
    [Fact]
    public async Task WithNoPictureSupportTheStripTakesNoRows()
    {
        var screen = await Arrived(ANotification.Group(count: 3, actors: ["Alice", "Bob", "Carol"]));

        foreach (var drawing in new[] { Drawing(61, raster: Raster.None), new Drawing(61, AShell.Now) })
        {
            var lines = screen.Lines(drawing);

            Assert.Contains("Hello world", lines[1].Text, StringComparison.Ordinal);
            Assert.All(lines, line => Assert.Empty(Faces(line)));
        }
    }

    /// <summary>
    ///     Where the faces go is the width's to say, not the group's: a group of two and a group of eight put their
    ///     first two faces in the same columns, and at one width both fit the same number of faces.
    /// </summary>
    [Fact]
    public async Task WhereTheFacesGoDependsOnTheWidthNotOnHowManyActorsThereAre()
    {
        string[] eight = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8"];
        var pictures = new FakePictures();

        foreach (var actor in eight)
        {
            pictures.HoldingAvatarOf($"{actor.ToLowerInvariant()}@hachyderm.io");
        }

        var screen = await Arrived(
            ANotification.Group(key: "two", newestId: "36", count: 2, actors: ["A1", "A2"]),
            ANotification.Group(key: "eight", newestId: "35", count: 8, actors: eight));

        var tops = screen.Lines(Drawing(61, pictures)).Where(line => line.Insets.Count > 0).ToList();

        Assert.Equal(2, tops.Count);
        Assert.Equal(
            tops[0].Insets.Select(inset => inset.Column),
            tops[1].Insets.Take(2).Select(inset => inset.Column));
        Assert.Equal([3, 9], tops[0].Insets.Select(inset => inset.Column));
    }

    /// <summary>A post drawn whole by its actor — a mention — has no strip: its byline already has the face.</summary>
    [Fact]
    public async Task AMentionHasNoStrip()
    {
        var mention = ANotification.Group(
            key: "ungrouped-40",
            kind: NotificationKind.Mention,
            actors: ["Robin"],
            post: APost.With(id: "120", account: "robin@hachyderm.io", author: "Robin"));
        var screen = await Arrived(mention);

        var lines = screen.Lines(Drawing(61));

        Assert.StartsWith("@ mentioned you", lines[0].Text[1..], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.Empty(Faces(line)));
    }

    private static Drawing Drawing(int width, IPictures? pictures = null, Raster? raster = null) =>
        new(width, AShell.Now, pictures ?? new FakePictures(), raster ?? ARaster.Sixel());

    /// <summary>Whose faces a row wants, left to right, by the account each avatar is named for.</summary>
    private static List<string> Faces(Line line) =>
    [
        .. new[] { line.Wants }.Concat(line.AlsoWants)
            .OfType<Drawn>()
            .Select(drawn => drawn.Id["avatar:".Length..].Split(' ')[0]),
    ];

    private static async Task<NotificationsScreen> Arrived(params NotificationGroup[] groups)
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding() };
        shell.Notifications.Groups = groups;
        shell.Notifications.Marker = "0";

        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        return Assert.IsType<NotificationsScreen>(opened.Screen);
    }
}
