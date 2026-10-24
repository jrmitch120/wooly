using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     The mouse on a <b>Notification group</b> reaches everyone the keyboard does (#456): a click on a face in the
///     avatar strip opens that one actor's account, a click on the names line opens the group's actors — the actors
///     screen on a group of several, the actor on a group of one — a double click on the post opens it, and a click
///     anywhere else picks the row and does nothing more. Sent at cells of a headless terminal, as rail clicks are.
/// </summary>
public class ShellNotificationGroupClickTests
{
    /// <summary>
    ///     A click on an actor's face opens that actor's account — the second face the second actor, by the strip's
    ///     layout (A7b of #450): indented two columns under the names, a face four wide and two apart.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ClickingAFaceOpensThatActor(int row)
    {
        var (built, drawn) = await On(Several(), Other());

        var names = RowOf(drawn, "Alice, Bob and 1 other");

        drawn.Click(FaceColumn(drawn, names, "Alice", face: 1), names + 1 + row);
        drawn.Settle();

        Assert.IsType<AccountScreen>(drawn.Shell.Screen);
        Assert.Equal("bob@hachyderm.io", Assert.Single(built.Accounts.Reads).Account.Text);
        Assert.Empty(built.Notifications.ActorReads);
    }

    /// <summary>A face on a group further down picks that group, and opens its own actor rather than the first group's.</summary>
    [Fact]
    public async Task ClickingAFaceOnAnotherGroupOpensItsActor()
    {
        var (built, drawn) = await On(Several(), Other());

        var names = RowOf(drawn, "Dan ★ favorited");

        drawn.Click(FaceColumn(drawn, names, "Dan", face: 0), names + 1);
        drawn.Settle();

        Assert.Equal("dan@hachyderm.io", Assert.Single(built.Accounts.Reads).Account.Text);
    }

    /// <summary>
    ///     A click on the names line of a group of several opens the screen listing everyone in it, read by the group's
    ///     key — what <c>a</c> does on it.
    /// </summary>
    [Fact]
    public async Task ClickingTheNamesOfAGroupOfSeveralOpensItsActors()
    {
        var (built, drawn) = await On(Several(), Other());

        drawn.Click(OverContent, RowOf(drawn, "Alice, Bob and 1 other"));
        drawn.Settle();

        Assert.IsType<ActorsScreen>(drawn.Shell.Screen);
        Assert.Equal("several", Assert.Single(built.Notifications.ActorReads).GroupKey);
        Assert.Empty(built.Accounts.Reads);
    }

    /// <summary>On a group of one the names line opens its one actor, as <c>a</c> does there.</summary>
    [Fact]
    public async Task ClickingTheNamesOfAGroupOfOneOpensItsActor()
    {
        var (built, drawn) = await On(Several(), Other());

        drawn.Click(OverContent, RowOf(drawn, "Dan ★ favorited"));
        drawn.Settle();

        Assert.IsType<AccountScreen>(drawn.Shell.Screen);
        Assert.Equal("dan@hachyderm.io", Assert.Single(built.Accounts.Reads).Account.Text);
        Assert.Empty(built.Notifications.ActorReads);
    }

    /// <summary>A double click on the preview of the reader's own post opens the post, as on any row.</summary>
    [Fact]
    public async Task DoubleClickingThePreviewOpensThePost()
    {
        var (built, drawn) = await On(Several(), Other());

        drawn.DoubleClick(OverContent, RowOf(drawn, "Second sheep"));
        drawn.Settle();

        Assert.Equal("220", Assert.IsType<PostScreen>(drawn.Shell.Screen).Post.Id);
        Assert.Empty(built.Accounts.Reads);
        Assert.Empty(built.Notifications.ActorReads);
    }

    /// <summary>
    ///     A click anywhere else on a row — the preview, the gap between two faces, the <c>+N</c>, the age at the end of
    ///     the names line — picks the group and does nothing more.
    /// </summary>
    [Theory]
    [InlineData("preview")]
    [InlineData("gap")]
    [InlineData("count")]
    [InlineData("age")]
    public async Task ClickingAnywhereElseOnARowOnlyPicksIt(string where)
    {
        var (built, drawn) = await On(Other(), Several());

        var names = RowOf(drawn, "Alice, Bob and 1 other");
        var line = drawn.Rows()[names + 1];
        var (column, row) = where switch
        {
            "preview" => (OverContent, RowOf(drawn, "Hello world")),
            "gap" => (FaceColumn(drawn, names, "Alice", face: 1) + 3, names + 1),
            "count" => (line.IndexOf("+1", StringComparison.Ordinal), names + 1),
            _ => (drawn.Rows()[names].LastIndexOf(Elapsed.Since(Several().NewestAt, AShell.Now), StringComparison.Ordinal), names),
        };

        Assert.True(column > 0, $"Nothing to click for {where}:\n{string.Join('\n', drawn.Rows())}");

        var screen = drawn.Shell.Screen;

        drawn.Click(column, row);
        drawn.Settle();

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal("several", Assert.IsType<NotificationsScreen>(screen).PickedGroup?.Key);
        Assert.Empty(built.Accounts.Reads);
        Assert.Empty(built.Notifications.ActorReads);
    }

    /// <summary>A group of three, two in the instance's sample, about the reader's post that reads "Hello world".</summary>
    private static NotificationGroup Several() =>
        ANotification.Group(key: "several", newestId: "36", count: 3, actors: ["Alice", "Bob"]);

    /// <summary>A group of one about another of the reader's posts.</summary>
    private static NotificationGroup Other() =>
        ANotification.Group(
            key: "other",
            newestId: "35",
            actors: ["Dan"],
            post: APost.With(id: "220", account: "jeff@mastodon.social", author: "Jeff", content: "Second sheep", isMine: true));

    /// <summary>
    ///     The terminal column a face's middle is drawn in, in the strip under the names line on <paramref name="names" />, whose names start with <paramref name="first" />:
    ///     the strip stands two columns in from where the names start, a face four wide and two apart.
    /// </summary>
    private static int FaceColumn(DrawnShell drawn, int names, string first, int face)
    {
        var start = drawn.Rows()[names].IndexOf(first, RailLines.Width, StringComparison.Ordinal);

        return start + 2 + face * 6 + 1;
    }

    /// <summary>The shell drawn tall, able to draw pictures, on the notifications screen holding <paramref name="groups" />.</summary>
    private static async Task<(AShell Built, DrawnShell Drawn)> On(params NotificationGroup[] groups)
    {
        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "alice@hachyderm.io")),
        };

        built.Notifications.Groups = groups;
        built.Notifications.Marker = "0";

        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built, drawsPictures: true);

        drawn.Shell.Rail.GoTo(DestinationKind.Notifications);
        built.Host.Drain();
        drawn.Redraw();

        return (built, drawn);
    }
}
