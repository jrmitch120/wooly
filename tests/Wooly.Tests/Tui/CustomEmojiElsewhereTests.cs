using Terminal.Gui.Input;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;
using static Wooly.Tests.Tui.CustomEmojiNameTests;
using static Wooly.Tests.Tui.CustomEmojiTests;

namespace Wooly.Tests.Tui;

/// <summary>
///     Custom emoji in the rest of what an instance writes — a post's content warning, a poll's options, an account's
///     bio and its custom fields — read back off the whole shell drawn headless (#526, ADR-0035): drawn as a post's
///     words draw them, two columns wherever the <b>Raster</b> draws and the shortcode, in a role of its own, where
///     nothing does.
/// </summary>
public class CustomEmojiElsewhereTests
{
    private static readonly CustomEmoji Php = new("php", "https://files.mastodon.social/emoji/php.png");

    private static readonly CustomEmoji Arizona = new("US_AZ", "https://files.mastodon.social/emoji/US_AZ.png");

    private static readonly CustomEmoji Cat = new("cat", "https://files.mastodon.social/emoji/cat.png");

    private static readonly Terminal.Gui.Drawing.Color Muted = Themes.Dark.For(Role.Muted).Foreground;

    private static readonly Terminal.Gui.Drawing.Color Warning = Themes.Dark.For(Role.ContentWarning).Foreground;

    private static readonly Terminal.Gui.Drawing.Color Polled = Themes.Dark.For(Role.Poll).Foreground;

    private static readonly Terminal.Gui.Drawing.Color Body = Themes.Dark.For(Role.Body).Foreground;

    /// <summary>
    ///     A content warning draws its custom emoji while it hides the post's words and once they are shown, at the
    ///     same columns on both, the words after it where two columns leave them.
    /// </summary>
    [Fact]
    public async Task AContentWarningDrawsItsCustomEmojiShownOrHidden()
    {
        using var drawn = await Shown(
            Warned("Spoilers :php: ahead", "Shearing today"),
            new FakePictures().HoldingCustomEmoji(Php),
            ARaster.Placeholders());

        // The columns the warning's emoji is drawn in, checked to be right after its words and before the rest.
        int[] WarningsEmoji()
        {
            var row = RowOf(drawn, "⚠ Spoilers ");
            var at = ColumnOf(drawn, row, "⚠ Spoilers ") + "⚠ Spoilers ".Length;

            Assert.Equal([at, at + 1], CustomEmojiColumns(drawn, row, RailLines.Width));
            Assert.Equal(" ahead", Text(drawn, row, at + CustomEmojiBox.Columns, " ahead".Length));

            return [at, at + 1];
        }

        var hidden = WarningsEmoji();

        drawn.Press(Key.X);
        drawn.Redraw();

        Assert.Contains(drawn.Rows(), row => row.Contains("Shearing today", StringComparison.Ordinal));
        Assert.Equal(hidden, WarningsEmoji());
    }

    /// <summary>
    ///     Where nothing draws, a warning's shortcode is its letters, in the custom emoji's role, and the rest of it the
    ///     warning's.
    /// </summary>
    [Fact]
    public async Task AContentWarningsShortcodeIsInItsOwnRoleWhereNothingDraws()
    {
        using var drawn = await Shown(
            Warned("Spoilers :php: ahead", "Shearing today"),
            new FakePictures().HoldingCustomEmoji(Php),
            Raster.None);

        var row = RowOf(drawn, "⚠ Spoilers :php: ahead");

        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":php:"));
        Assert.Equal(Warning, Foreground(drawn, row, "ahead"));
    }

    /// <summary>
    ///     The preview of the reader's own warned post under a notification, which is its warning in place of its
    ///     words, draws the warning's custom emoji too.
    /// </summary>
    [Fact]
    public async Task APreviewsWarningDrawsItsCustomEmoji()
    {
        var mine = APost.With(
            id: "110",
            content: "Shearing :US_AZ: today",
            contentWarning: "Spoilers :php: ahead",
            isMine: true,
            customEmoji: [Php, Arizona]);

        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(kind: Wooly.Core.Notifications.NotificationKind.Favorite, post: mine)),
        };

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        drawn.Shell.Step(ToNotifications);
        built.Host.Settle();
        drawn.Redraw();

        var row = RowOf(drawn, "⚠ Spoilers ");
        var at = ColumnOf(drawn, row, "⚠ Spoilers ") + "⚠ Spoilers ".Length;

        Assert.Equal([at, at + 1], CustomEmojiColumns(drawn, row, at));
        Assert.Equal(" ahead", Text(drawn, row, at + CustomEmojiBox.Columns, " ahead".Length));
    }

    /// <summary>
    ///     Where nothing draws, the preview of the reader's own post under a notification, which is muted, writes the
    ///     post's shortcodes set apart from the muted words around them — the one place a post's words are muted, where
    ///     a muted shortcode would read as one more of them (story 16).
    /// </summary>
    [Fact]
    public async Task APreviewsShortcodeIsSetApartFromItsMutedWordsWhereNothingDraws()
    {
        var mine = APost.With(id: "110", content: "Shearing :php: today", isMine: true, customEmoji: [Php]);

        var built = new AShell
        {
            Notifications = FakeNotificationInbox.Holding(
                ANotification.With(kind: Wooly.Core.Notifications.NotificationKind.Favorite, post: mine)),
        };

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            raster: Raster.None);

        drawn.Shell.Step(ToNotifications);
        built.Host.Settle();
        drawn.Redraw();

        var row = RowOf(drawn, "Shearing :php: today");

        Assert.Equal(Muted, Foreground(drawn, row, "today"));
        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":php:"));
        Assert.NotEqual(Muted, ShortcodeColour);
    }

    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 5;

    /// <summary>
    ///     A warned post's words want none of their custom emoji pictures while the warning hides them — only the
    ///     warning's own, which is drawn — and want them once the reader asks past it.
    /// </summary>
    [Fact]
    public async Task AWarnedPostsHiddenWordsWantNoCustomEmojiUntilShown()
    {
        var pictures = new FakePictures();

        using var drawn = await Shown(
            Warned("Spoilers :php: ahead", "Shearing :US_AZ: today"),
            pictures,
            ARaster.Placeholders());

        bool Wanted(CustomEmoji emoji) =>
            pictures.Frames.Any(frame => frame.Any(wanted => wanted.Drawn == Drawn.CustomEmoji(emoji)));

        Assert.True(Wanted(Php));
        Assert.False(Wanted(Arizona));
        Assert.DoesNotContain(Drawn.CustomEmoji(Arizona).Id, pictures.Asked);

        drawn.Press(Key.X);
        drawn.Redraw();

        Assert.True(Wanted(Arizona));
    }

    /// <summary>
    ///     A poll option draws the custom emoji the poll lists after its bar, share and count, which stand in the
    ///     columns they take on an option written without one — and the words after it where two columns leave them.
    /// </summary>
    [Fact]
    public async Task APollOptionDrawsItsCustomEmojiWithTheBarAndCountWhereTheyWere()
    {
        using var plain = await Shown(
            Polling(APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6)),
            new FakePictures(),
            ARaster.Placeholders());

        using var drawn = await Shown(
            Polling(APost.AnAnswer(":cat: Cats", 4, customEmoji: [Cat]), APost.AnAnswer("Dogs", 6, customEmoji: [Cat])),
            new FakePictures().HoldingCustomEmoji(Cat),
            ARaster.Placeholders());

        var was = RowOf(plain, "(4)");
        var at = ColumnOf(plain, was, "Cats");
        var row = RowOf(drawn, "(4)");

        Assert.Equal(Text(plain, was, 0, at), Text(drawn, row, 0, at));
        Assert.Equal([at, at + 1], CustomEmojiColumns(drawn, row, RailLines.Width));
        Assert.Equal(" Cats", Text(drawn, row, at + CustomEmojiBox.Columns, " Cats".Length));
        Assert.Equal(plain.Rows()[RowOf(plain, "(6)")], drawn.Rows()[RowOf(drawn, "(6)")]);
    }

    /// <summary>
    ///     Where nothing draws, a poll option's shortcode is its letters, in the custom emoji's role, and the rest of it
    ///     the poll's.
    /// </summary>
    [Fact]
    public async Task APollOptionsShortcodeIsInItsOwnRoleWhereNothingDraws()
    {
        using var drawn = await Shown(
            Polling(APost.AnAnswer(":cat: Cats", 4, customEmoji: [Cat]), APost.AnAnswer("Dogs", 6)),
            new FakePictures().HoldingCustomEmoji(Cat),
            Raster.None);

        var row = RowOf(drawn, ":cat: Cats");

        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":cat:"));
        Assert.Equal(Polled, Foreground(drawn, row, "Cats"));
    }

    /// <summary>
    ///     An account's bio and its custom fields draw the custom emoji the account lists on its profile, a field's
    ///     label as well as its value — each the words after it where two columns leave them.
    /// </summary>
    [Fact]
    public async Task ABioAndCustomFieldsDrawTheirCustomEmojiOnTheProfile()
    {
        using var drawn = await Profiled(ARaster.Placeholders());

        var bio = RowOf(drawn, "Shepherd ");
        var after = ColumnOf(drawn, bio, "Shepherd ") + "Shepherd ".Length;

        Assert.Equal([after, after + 1], CustomEmojiColumns(drawn, bio, RailLines.Width));
        Assert.Equal(" in Arizona", Text(drawn, bio, after + 2, " in Arizona".Length));

        var field = RowOf(drawn, " Site: ");
        var label = ColumnOf(drawn, field, " Site: ") - CustomEmojiBox.Columns;
        var value = ColumnOf(drawn, field, "https://maria.example ") + "https://maria.example ".Length;

        Assert.Equal([label, label + 1, value, value + 1], CustomEmojiColumns(drawn, field, RailLines.Width));
    }

    /// <summary>
    ///     Where nothing draws, a bio's and a field's shortcodes are their letters, in the custom emoji's role, and the
    ///     rest of each what it is drawn as — a label's muted around its shortcode rather than over it.
    /// </summary>
    [Fact]
    public async Task ABiosAndAFieldsShortcodesAreInTheirOwnRoleWhereNothingDraws()
    {
        using var drawn = await Profiled(Raster.None);

        var bio = RowOf(drawn, "Shepherd :php: in Arizona");

        Assert.Equal(ShortcodeColour, Foreground(drawn, bio, ":php:"));
        Assert.Equal(Body, Foreground(drawn, bio, "Arizona"));

        var field = RowOf(drawn, ":php: Site: https://maria.example :php:");

        // The label is muted, so its shortcode is set apart from it rather than muted with it (story 16).
        Assert.Equal(Muted, Foreground(drawn, field, " Site:"));
        Assert.Equal(ShortcodeColour, Foreground(drawn, field, ":php:"));
        Assert.NotEqual(Muted, ShortcodeColour);
        Assert.Equal(Themes.Dark.For(Role.Link).Foreground, Foreground(drawn, field, "https://maria.example"));
        // The value's own, the second on the row.
        var value = drawn.Rows()[field].LastIndexOf(":php:", StringComparison.Ordinal);

        Assert.All(
            Enumerable.Range(value, ":php:".Length),
            column => Assert.Equal(ShortcodeColour, drawn.Cell(field, column).Foreground));
    }

    /// <summary>A post warned with <paramref name="warning" />, listing every emoji here for both.</summary>
    private static Post Warned(string warning, string content) => APost.With(
        id: "110",
        content: content,
        contentWarning: warning,
        customEmoji: [Php, Arizona]);

    /// <summary>A post carrying a poll of <paramref name="options" /> out of ten votes.</summary>
    private static Post Polling(params PostPollOption[] options) =>
        APost.With(id: "110", content: "Cats or dogs?", poll: APost.APoll(options: options));

    /// <summary>
    ///     The feed in the dark theme with <paramref name="post" /> first, so that it is the one picked out and
    ///     <c>x</c> acts on it.
    /// </summary>
    private static Task<DrawnShell> Shown(Post post, FakePictures pictures, Raster raster) =>
        DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: pictures,
            drawsPlaceholders: raster.Way == PictureWay.Placeholders,
            raster: raster);

    /// <summary>
    ///     The account screen of somebody whose bio and one custom field, label and value both, write the custom emoji
    ///     their account lists.
    /// </summary>
    private static async Task<DrawnShell> Profiled(Raster raster)
    {
        var maria = AnAccount.With(
            address: "maria@hachyderm.io",
            author: "Maria Ochoa",
            bio: "Shepherd :php: in Arizona",
            fields: [AnAccount.Field(":php: Site", "https://maria.example :php:")],
            customEmoji: [Php]);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: maria.Address, author: maria.Author)),
            Accounts = FakeAccountRelationships.Holding(maria),
        };

        var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Php),
            drawsPlaceholders: raster.Way == PictureWay.Placeholders,
            raster: raster);

        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }
}
