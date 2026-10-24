using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The reader's own post under a notification is a reminder of which post it was, not something to read again: a
///     short muted preview. Anyone else's post is what the notification is for, so it is drawn whole (#452).
/// </summary>
public class NotificationPreviewTests
{
    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 6;

    /// <summary>Long enough to wrap past two rows at any width these tests draw at.</summary>
    private const string Wordy =
        "Spent the weekend teaching Wooly to group notifications, which turned out to need far more thought about "
        + "who did what to whom than I had expected, and then a second weekend to draw it, and a third to test it.";

    private static readonly DateTimeOffset Now = AShell.Now;

    private static Post Mine(string? contentWarning = null) => APost.With(
        id: "110",
        content: Wordy,
        contentWarning: contentWarning,
        isMine: true,
        media: [APost.APicture()],
        linkPreview: APost.ALinkPreview());

    /// <summary>
    ///     The rows drawn under the row saying what happened (the first row on screen), up to the rule that ends the
    ///     notification, leaving out blank ones.
    /// </summary>
    private static IReadOnlyList<Line> Under(Wooly.Tui.Shell.Shell shell) =>
    [
        .. shell.Screen.Lines(new Drawing(61, Now))
                .Skip(1)
                .TakeWhile(line => !line.Text.TrimStart('▌', ' ').StartsWith('─'))
                .Where(line => line.Text.Trim('▌', ' ').Length > 0),
    ];

    /// <summary>The spans on a row that put anything on screen, past the mark picking the row out.</summary>
    private static IEnumerable<Span> Inked(Line line) =>
        line.Spans.Where(span => span.Text.Trim('▌', ' ').Length > 0);

    private static async Task<(AShell Shell, Wooly.Tui.Shell.Shell Opened)> On(params Notification[] notifications)
    {
        var shell = new AShell { Notifications = FakeNotificationInbox.Holding(notifications) };
        var opened = await shell.Opened();

        opened.Step(ToNotifications);
        shell.Host.Settle();

        Assert.IsType<NotificationsScreen>(opened.Screen);

        return (shell, opened);
    }

    [Theory]
    [InlineData("favorite")]
    [InlineData("boost")]
    public async Task OwnPost_IsAMutedPreviewOfAtMostTwoRowsWithNoMediaOrLinkPreview(string kind)
    {
        var (_, opened) = await On(
            ANotification.With(
                kind: kind == "boost" ? NotificationKind.Boost : NotificationKind.Favorite,
                account: "alice@hachyderm.io",
                post: Mine()));

        var under = Under(opened);

        Assert.Equal(2, under.Count);
        Assert.All(under, line => Assert.All(Inked(line), span => Assert.Equal(Role.Muted, span.Role)));
        Assert.StartsWith("Spent the weekend", under[0].Text.Trim('▌', ' '), StringComparison.Ordinal);
        Assert.EndsWith("…", under[1].Text.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain(under, line => line.Text.Contains("Sheep, at length", StringComparison.Ordinal));
        Assert.DoesNotContain(under, line => line.Text.Contains("▒▒▒▒", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A preview never says what a content warning was put up to keep back: it shows the warning instead.
    /// </summary>
    [Fact]
    public async Task OwnPost_UnderAContentWarning_PreviewsTheWarningAndNeverTheText()
    {
        var (_, opened) = await On(
            ANotification.With(kind: NotificationKind.Favorite, post: Mine(contentWarning: "Spoilers for the finale")));

        var under = Under(opened);

        var said = Assert.Single(under);
        Assert.Contains("Spoilers for the finale", said.Text, StringComparison.Ordinal);
        Assert.All(Inked(said), span => Assert.Equal(Role.Muted, span.Role));
        Assert.DoesNotContain(under, line => line.Text.Contains("Spent the weekend", StringComparison.Ordinal));
    }

    /// <summary>A short post is previewed as it is, with no cut marked where nothing was cut.</summary>
    [Fact]
    public async Task OwnPost_ThatFitsInARow_IsPreviewedWhole()
    {
        var (_, opened) = await On(
            ANotification.With(kind: NotificationKind.Favorite, post: APost.With(content: "Morning, all", isMine: true)));

        var said = Assert.Single(Under(opened));
        Assert.Equal("Morning, all", said.Text.Trim('▌', ' '));
    }

    /// <summary>A mention is somebody speaking to the reader, which is what the row is for: it is drawn whole.</summary>
    [Fact]
    public async Task Mention_IsDrawnInFull()
    {
        var theirs = APost.With(
            account: "alice@hachyderm.io",
            author: "Alice",
            content: Wordy,
            linkPreview: APost.ALinkPreview());

        var (_, opened) = await On(ANotification.With(kind: NotificationKind.Mention, post: theirs));

        var under = Under(opened);

        Assert.Contains(under, line => line.Text.Contains("@alice@hachyderm.io", StringComparison.Ordinal));
        Assert.Contains(
            under,
            line => line.Has(Role.Body) && line.Text.Contains("a third to test it.", StringComparison.Ordinal));
        Assert.Contains(under, line => line.Text.Contains("Sheep, at length", StringComparison.Ordinal));
        Assert.True(under.Count > 2);
    }

    /// <summary>
    ///     The rule follows who wrote the post, not the kind: a kind this client has no word for, about the reader's own
    ///     post, is previewed too — and a favorite of somebody else's post (which no instance sends, but the rule does
    ///     not ask) is drawn whole.
    /// </summary>
    [Fact]
    public async Task TheRuleFollowsThePostsAuthorNotTheKind()
    {
        var (_, opened) = await On(
            ANotification.With(id: "34", kind: NotificationKind.Reported("quoted_update"), post: Mine()),
            ANotification.With(
                id: "35",
                kind: NotificationKind.Favorite,
                post: APost.With(id: "111", account: "alice@hachyderm.io", content: Wordy)));

        var drawn = AShell.Drawn(opened.Screen);

        // Both posts open on the same words; only the one drawn whole gets as far as the end of them.
        Assert.Equal(2, drawn.Count(line => line.Contains("Spent the weekend", StringComparison.Ordinal)));
        Assert.Single(drawn, line => line.Contains("a third to test it.", StringComparison.Ordinal));
    }

    /// <summary>The preview is never a dead end: <c>⏎</c> opens the post it stands for in full.</summary>
    [Fact]
    public async Task Enter_OnAPreview_OpensThePostInFull()
    {
        var (shell, opened) = await On(ANotification.With(kind: NotificationKind.Favorite, post: Mine()));

        shell.Engagement = FakePostEngagement.Answering(Mine());
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var post = Assert.IsType<PostScreen>(opened.Screen);
        Assert.Equal("110", post.Post.Id);
        Assert.Contains(AShell.Drawn(post), line => line.Contains("a third to test it.", StringComparison.Ordinal));
    }

    /// <summary>The post keys act on the post under a preview as they do on any post.</summary>
    [Theory]
    [InlineData(PostMark.Favorite)]
    [InlineData(PostMark.Boost)]
    public async Task Mark_ActsOnThePostUnderAPreview(PostMark mark)
    {
        var (shell, opened) = await On(ANotification.With(kind: NotificationKind.Favorite, post: Mine()));

        await opened.Mark(mark);

        var marked = Assert.Single(shell.Engagement.Marks);
        Assert.Equal("110", marked.PostId);
        Assert.Equal(mark, marked.Mark);
    }

    [Fact]
    public async Task Reply_AnswersThePostUnderAPreview()
    {
        var (_, opened) = await On(ANotification.With(kind: NotificationKind.Favorite, post: Mine()));

        var compose = ComposeRows.Open(opened, ComposeFor.Reply);

        Assert.Equal("110", compose.About?.Id);
    }

    /// <summary>
    ///     The arrows walk the references a reader can see. A preview draws none of them, so there is nothing to walk
    ///     to and no hint offering it; ⏎ opens the post instead, whose references are all there.
    /// </summary>
    [Fact]
    public void WalkReference_FindsNothingInAPreview()
    {
        var mine = APost.With(content: "Grouping, as #wooly does it: https://example.com/groups", isMine: true);
        var screen = new NotificationsScreen(
            [ANotification.Alone(ANotification.With(kind: NotificationKind.Favorite, post: mine))]);

        Assert.Empty(screen.References);
        Assert.False(screen.WalkReference(1));
    }
}
