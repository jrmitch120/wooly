using Terminal.Gui.Input;
using Wooly.Core;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;
using static Wooly.Tests.Tui.CustomEmojiTests;

namespace Wooly.Tests.Tui;

/// <summary>
///     Custom emoji in display names, read back off the whole shell drawn headless (#524, ADR-0035): wherever a name is
///     drawn, the same as in a post's words — two columns and one row wherever the <b>Raster</b> draws, placeholder
///     cells once landed on Kitty's placeholders, and the shortcode as written, in a role of its own, where nothing
///     draws.
/// </summary>
public class CustomEmojiNameTests
{
    private static readonly CustomEmoji Verified = new("verified", "https://files.mastodon.social/emoji/verified.png");

    private static readonly CustomEmoji Arizona = new("US_AZ", "https://files.mastodon.social/emoji/US_AZ.png");

    /// <summary>
    ///     A feed's byline draws its author's custom emoji as two placeholder cells each, beside the avatar's, right
    ///     after the name's words — each emoji's picture sent to the terminal once, and the avatar's as well.
    /// </summary>
    [Fact]
    public async Task AFeedBylineDrawsTheNamesCustomEmojiAsPlaceholderCells()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await Shown(
            APost.With(
                id: "220",
                account: "jeff@mastodon.social",
                author: "Jeff :verified: :US_AZ:",
                avatarUrl: "https://files.mastodon.social/avatars/jeff.png",
                authorCustomEmoji: [Verified, Arizona]),
            new FakePictures()
                .HoldingAvatarOf("jeff@mastodon.social")
                .HoldingCustomEmoji(Verified)
                .HoldingCustomEmoji(Arizona),
            ARaster.Placeholders(),
            terminal);

        var row = RowOf(drawn, "Jeff ");
        var at = ColumnOf(drawn, row, "Jeff ") + "Jeff ".Length;

        Assert.Equal([at, at + 1, at + 3, at + 4], CustomEmojiColumns(drawn, row, at));
        Assert.Equal(
            [(2, 1), (2, 1), (Avatar.BylineColumns(ARaster.Placeholders()), Avatar.BylineRows)],
            terminal.Transmitted.Select(sent => (sent.Columns, sent.Rows)).Order());
    }

    /// <summary>
    ///     On the ways that draw through a box, sixel and Kitty's, a byline's custom emoji are pictures two columns by
    ///     one right after the name's words, drawn as well as the avatar beside them rather than in place of it.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Sixel)]
    [InlineData(PictureWay.Kitty)]
    public async Task AFeedBylineDrawsTheNamesCustomEmojiThroughABoxBesideTheAvatar(PictureWay way)
    {
        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", account: "maria@hachyderm.io", author: "Maria Ochoa"),
                    APost.With(
                        id: "220",
                        author: "Jeff :verified:",
                        avatarUrl: "https://files.mastodon.social/avatars/jeff.png",
                        authorCustomEmoji: [Verified])),
            },
            pictures: new FakePictures().HoldingAvatarOf("jeff@mastodon.social").HoldingCustomEmoji(Verified),
            drawsPictures: way == PictureWay.Sixel,
            answersKitty: way == PictureWay.Kitty);

        var row = RowOf(drawn, "Jeff ");
        var at = drawn.Rows()[row].IndexOf("Jeff ", StringComparison.Ordinal);
        var pictures = drawn.Application.Driver!.GetOutputBuffer().GetRasterImages()
                            .Select(image => image.DestinationCells)
                            .ToList();

        Assert.Contains(
            new System.Drawing.Rectangle(at + "Jeff ".Length, row, CustomEmojiBox.Columns, CustomEmojiBox.Rows),
            pictures);
        Assert.Contains(
            pictures,
            cells => cells.Y == row
                && cells.Size == new System.Drawing.Size(Avatar.BylineColumns(ARaster.Sixel()), Avatar.BylineRows));
    }

    /// <summary>
    ///     Where nothing draws, a byline's custom emoji are their shortcodes as written, in the custom emoji's role
    ///     beside the name's words — and a <c>:word:</c> the account does not list is part of the name.
    /// </summary>
    [Fact]
    public async Task AFeedBylineWritesTheNamesShortcodesInTheirOwnRoleWhereNothingDraws()
    {
        using var drawn = await Shown(
            APost.With(id: "220", author: "Jeff :verified: :wave:", authorCustomEmoji: [Verified]),
            new FakePictures().HoldingCustomEmoji(Verified),
            Raster.None);

        var row = RowOf(drawn, "Jeff :verified: :wave:");

        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":verified:"));
        Assert.Equal(Name, Foreground(drawn, row, "Jeff"));
        Assert.Equal(Name, Foreground(drawn, row, ":wave:"));
    }

    /// <summary>
    ///     A name too long for its byline is clipped with its custom emoji held whole — each one dropped with the rest of
    ///     what is cut rather than drawn in half, wherever the cut falls across it — and the audience and the age after
    ///     it stand where every other byline's do.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ANameClippedToItsBylineDropsACustomEmojiWhole(int lead)
    {
        using var drawn = await Shown(
            APost.With(
                id: "220",
                author: new string('x', lead) + string.Concat(Enumerable.Repeat(":verified: ", 40)),
                authorCustomEmoji: [Verified]),
            new FakePictures().HoldingCustomEmoji(Verified),
            ARaster.Placeholders());

        var row = RowOf(drawn, "…");
        var cells = Placeholders(drawn).Where(cell => cell.Row == row).OrderBy(cell => cell.Column).ToList();

        Assert.NotEmpty(cells);
        Assert.All(
            cells.Chunk(2),
            pair => Assert.Equal(
                [KittyPlaceholder.Of(0, 0), KittyPlaceholder.Of(0, 1)],
                pair.Select(cell => cell.Grapheme)));
        Assert.All(cells.Chunk(2), pair => Assert.Equal(pair[0].Column + 1, pair[1].Column));
        Assert.Equal(
            ColumnOf(drawn, RowOf(drawn, "Maria Ochoa"), "○ now"),
            ColumnOf(drawn, row, "○ now"));
    }

    /// <summary>
    ///     A boost names its booster with their custom emoji drawn, on the feed's "boosted" line and the post screen's
    ///     "boosted by" alike — and the post screen's full byline draws the boosted author's.
    /// </summary>
    [Fact]
    public async Task TheBoostedLineAndThePostScreensBylineDrawTheNamesCustomEmoji()
    {
        var boost = APost.With(
            id: "220",
            account: "maria@hachyderm.io",
            author: "Maria :verified:",
            authorCustomEmoji: [Verified],
            content: "",
            boosted: APost.With(id: "99", author: "Jeff :US_AZ:", authorCustomEmoji: [Arizona]));

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(boost) },
            pictures: new FakePictures().HoldingCustomEmoji(Verified).HoldingCustomEmoji(Arizona),
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        var feed = RowOf(drawn, "Maria ");

        Assert.Equal(
            [ColumnOf(drawn, feed, "Maria ") + 6, ColumnOf(drawn, feed, "Maria ") + 7],
            CustomEmojiColumns(drawn, feed, RailLines.Width));
        Assert.Equal(ColumnOf(drawn, feed, "Maria ") + 8, ColumnOf(drawn, feed, " boosted"));

        drawn.Press(Key.Enter);
        drawn.Settle();

        var by = RowOf(drawn, "boosted by Maria ");
        var name = RowOf(drawn, "Jeff ", after: 0);

        Assert.Equal(
            [ColumnOf(drawn, by, "Maria ") + 6, ColumnOf(drawn, by, "Maria ") + 7],
            CustomEmojiColumns(drawn, by, RailLines.Width));
        Assert.Equal(
            [ColumnOf(drawn, name, "Jeff ") + 5, ColumnOf(drawn, name, "Jeff ") + 6],
            CustomEmojiColumns(drawn, name, RailLines.Width));
    }

    /// <summary>
    ///     Where nothing draws, the booster's shortcode on the feed's "boosted" line is set apart from the muted words
    ///     around it, as it is from a byline's — the line being muted, a muted shortcode would be lost in it (story 16).
    /// </summary>
    [Fact]
    public async Task TheBoostedLinesShortcodeIsSetApartFromItsMutedWordsWhereNothingDraws()
    {
        var boost = APost.With(
            id: "220",
            account: "maria@hachyderm.io",
            author: "Maria :verified:",
            authorCustomEmoji: [Verified],
            content: "",
            boosted: APost.With(id: "99", author: "Jeff Mitchell"));

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "110"), boost) },
            pictures: new FakePictures().HoldingCustomEmoji(Verified),
            raster: Raster.None);

        var row = RowOf(drawn, "Maria :verified: boosted");

        Assert.Equal(Muted, Foreground(drawn, row, "boosted"));
        Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":verified:"));
        Assert.NotEqual(Muted, ShortcodeColour);
    }

    private static readonly Terminal.Gui.Drawing.Color Muted = Themes.Dark.For(Role.Muted).Foreground;

    private static readonly Terminal.Gui.Drawing.Color Name = Themes.Dark.For(Role.BylineName).Foreground;

    /// <summary>
    ///     A notification group's names line draws each actor's custom emoji from that actor's own list, the words
    ///     after them where two columns leave them — and where nothing draws, the same names with the shortcodes in
    ///     their own role.
    /// </summary>
    [Fact]
    public async Task ANotificationsActorNamesDrawTheirCustomEmoji()
    {
        var group = ANotification.Group() with
        {
            Actors =
            [
                AnAccount.With(address: "alice@hachyderm.io", author: "Alice :verified:", customEmoji: [Verified]),
                AnAccount.With(address: "bob@hachyderm.io", author: "Bob :verified:", id: "43"),
            ],
        };

        using (var drawn = await Notified(group, ARaster.Placeholders()))
        {
            var row = RowOf(drawn, "Alice ", after: 0);
            var at = ColumnOf(drawn, row, "Alice ") + "Alice ".Length;

            Assert.Equal([at, at + 1], CustomEmojiColumns(drawn, row, RailLines.Width));
            Assert.Equal(at + 2, ColumnOf(drawn, row, " and Bob :verified:"));
        }

        using (var drawn = await Notified(group, Raster.None))
        {
            var row = RowOf(drawn, "Alice :verified: and Bob :verified:");

            Assert.Equal(ShortcodeColour, Foreground(drawn, row, ":verified:"));
            Assert.Equal(Name, Foreground(drawn, row, "Bob :verified:"));
        }
    }

    /// <summary>
    ///     Every Account block draws its name's custom emoji — the account screen's header, and each account a follow
    ///     list and Discover's suggestions list — beside the avatar's box, right after the name's words.
    /// </summary>
    [Theory]
    [InlineData("account")]
    [InlineData("follows")]
    [InlineData("discover")]
    public async Task AnAccountBlockDrawsTheNamesCustomEmoji(string where)
    {
        var alice = AnAccount.With(
            address: "alice@hachyderm.io",
            author: "Alice :verified:",
            customEmoji: [Verified]);

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: alice.Address, author: alice.Author)),
            Accounts = FakeAccountRelationships.Holding(
                where == "account" ? alice : AnAccount.With(address: "maria@hachyderm.io", author: "Maria Ochoa"),
                alice),
            Suggestions = FakeFollowSuggestions.Offering(ASuggestion.With(alice)),
        };

        using var drawn = await DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Verified),
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        switch (where)
        {
            case "account":
                await drawn.Shell.OpenAuthor();
                break;

            case "follows":
                await drawn.Shell.OpenAuthor();
                built.Host.Drain();
                drawn.Press(Key.W);
                break;

            case "discover":
                drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Discover));
                break;
        }

        built.Host.Drain();
        drawn.Redraw();

        var row = RowOf(drawn, "Alice ", after: 0);
        var at = ColumnOf(drawn, row, "Alice ") + "Alice ".Length;

        Assert.Equal([at, at + 1], CustomEmojiColumns(drawn, row, at));
    }

    /// <summary>
    ///     The breadcrumb, which can hold one line of plain text and no picture, names a post's author and then the
    ///     account opened off it by their name with the shortcodes it lists taken out, on a terminal that draws them as
    ///     well — and by the handle where the name is nothing but shortcodes, so that a crumb always names somebody
    ///     (#525).
    /// </summary>
    [Theory]
    [InlineData(":verified: Jeff :US_AZ:", "Jeff")]
    [InlineData(":verified: :US_AZ:", "@jeff@mastodon.social")]
    public async Task TheCrumbNamesThePostsAuthorAndTheirAccountWithTheShortcodesTakenOut(string author, string named)
    {
        var jeff = AnAccount.With(address: "jeff@mastodon.social", author: author, customEmoji: [Verified, Arizona]);
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "220", account: jeff.Address, author: author, authorCustomEmoji: [Verified, Arizona])),
            Accounts = FakeAccountRelationships.Holding(jeff),
        };

        using var drawn = await DrawnShell.Of(
            120,
            Tall,
            Themes.Plain,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Verified).HoldingCustomEmoji(Arizona),
            drawsPlaceholders: true,
            raster: ARaster.Placeholders());

        drawn.Press(Key.Enter);
        drawn.Settle();
        await drawn.Shell.OpenAuthor();
        built.Host.Drain();
        drawn.Redraw();

        var crumbs = drawn.Rows()[0];

        Assert.Contains($"Post by {named} › {named}", crumbs, StringComparison.Ordinal);
        Assert.DoesNotContain(":", crumbs.Replace(named, string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>The notifications screen holding <paramref name="group" />, drawn in the dark theme and wide enough for two names.</summary>
    private static async Task<DrawnShell> Notified(NotificationGroup group, Raster raster)
    {
        var built = new AShell { Notifications = FakeNotificationInbox.Holding() };
        built.Notifications.Groups = [group];

        var drawn = await DrawnShell.Of(
            120,
            Tall,
            Themes.Dark,
            built,
            pictures: new FakePictures().HoldingCustomEmoji(Verified),
            drawsPlaceholders: raster.Way == PictureWay.Placeholders,
            raster: raster);

        drawn.Shell.Step(ToNotifications);
        built.Host.Settle();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>Where the rail's notifications destination is, counting from Home.</summary>
    private const int ToNotifications = 5;

    /// <summary>The columns from <paramref name="from" /> on that a row draws placeholder cells in.</summary>
    internal static int[] CustomEmojiColumns(DrawnShell drawn, int row, int from) =>
    [
        .. Placeholders(drawn).Where(cell => cell.Row == row && cell.Column >= from)
                              .Select(cell => cell.Column)
                              .Order(),
    ];

    /// <summary>
    ///     The column <paramref name="text" /> starts at on a row, read cell by cell — so that a placeholder cell before
    ///     it, which is one column however many characters it is written in, counts as the one column it is.
    /// </summary>
    internal static int ColumnOf(DrawnShell drawn, int row, string text)
    {
        var contents = drawn.Application.Driver!.Contents!;
        var columns = contents.GetLength(1);

        for (var column = RailLines.Width; column + text.Length <= columns; column++)
        {
            if (Text(drawn, row, column, text.Length) == text)
            {
                return column;
            }
        }

        Assert.Fail($"Row {row} does not read {text}:\n{string.Join('\n', drawn.Rows())}");

        return -1;
    }

    /// <summary>
    ///     The feed, tall, in the dark theme so that roles can be told apart by colour, with <paramref name="post" />
    ///     second, after one by somebody else — so that it is not the one picked out.
    /// </summary>
    private static Task<DrawnShell> Shown(
        Post post,
        FakePictures pictures,
        Raster raster,
        FakeTerminalImages? terminal = null) =>
        DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", account: "maria@hachyderm.io", author: "Maria Ochoa"),
                    post),
            },
            pictures: pictures,
            kittyImages: terminal,
            drawsPlaceholders: raster.Way == PictureWay.Placeholders,
            raster: raster);
}
