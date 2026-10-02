using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Wooly.Core.Configuration;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Line = Wooly.Tui.Rendering.Line;

namespace Wooly.Tests.Tui;

/// <summary>
///     The selected thing's band (#269): every row of the thing picked out sits on <see cref="Role.Band" />, not only
///     the <c>▌</c> in its gutter. Which rows those are is decided where the rows are built, so it is asserted on the
///     rows; what the view makes of the mark is asserted on a frame drawn headless.
/// </summary>
public class BandTests
{
    /// <summary>The screens ADR-0021 names, with how many things there are on each to pick.</summary>
    public static TheoryData<string, int> Screens => new()
    {
        { "feed", 3 },
        { "post", 3 },
        { "notifications", 3 },
        { "conversation", 3 },

        // The header block is the first thing, and the two posts under it the rest (#179).
        { "account", 3 },
    };

    /// <summary>
    ///     After picking the <c>at</c>th thing, the rows marked as picked are exactly the rows that say they are part
    ///     of it — every one of them, and not the rule or the heading beside it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void EveryRowOfThePickedThingIsMarkedAndNoOther(string kind, int count)
    {
        var screen = Of(kind);

        for (var at = 0; at < count; at++)
        {
            screen.Pick(at);

            var lines = screen.Lines(new Drawing(61, AShell.Now));

            var named = lines.Where(line => line.Item == at).ToList();

            Assert.NotEmpty(named);
            Assert.Equal(named, lines.Where(line => line.Picked).ToList());
        }
    }

    /// <summary>A marked row is banded across the whole of the view, past the end of what is written on it.</summary>
    [Fact]
    public async Task AMarkedRowIsDrawnOnTheBandAcrossItsWidth()
    {
        var band = Themes.Dark.For(Role.Band).Background;

        var cells = await Drawn(Themes.Dark, Line.Of("picked", Role.Body) with { Picked = true });

        Assert.All(Row(cells, 0), cell => Assert.Equal(band, cell.Background));
    }

    /// <summary>And a row nobody picked is left on the page.</summary>
    [Fact]
    public async Task AnUnmarkedRowStaysOnThePage()
    {
        var page = Themes.Dark.For(Role.Body).Background;

        var cells = await Drawn(Themes.Dark, Line.Of("not picked", Role.Body));

        Assert.All(Row(cells, 0), cell => Assert.Equal(page, cell.Background));
    }

    /// <summary>
    ///     A span with a background of its own keeps it on a banded row: a picked reference a theme has given a
    ///     background says something the band does not.
    /// </summary>
    [Fact]
    public async Task ASpanWithABackgroundOfItsOwnKeepsIt()
    {
        var theme = Themes.Chosen(
            new WoolyConfig
            {
                Theme = "dark",
                Themes = new Dictionary<string, ThemeConfig>
                {
                    ["dark"] = new()
                    {
                        Roles = new Dictionary<string, ThemeRole>
                        {
                            ["reference-picked"] = new("#ffffff", "#ff0000"),
                        },
                    },
                },
            },
            "/somewhere/config.toml");

        var cells = await Drawn(
            theme,
            Line.Of(new Span("ab", Role.Body), new Span("cd", Role.ReferencePicked)) with { Picked = true });

        var row = Row(cells, 0);

        Assert.Equal(theme.For(Role.Band).Background, row[0].Background);
        Assert.Equal(theme.For(Role.ReferencePicked).Background, row[2].Background);
        Assert.NotEqual(row[0].Background, row[2].Background);
    }

    /// <summary>Under the no-colour theme the mark changes nothing at all: the <c>▌</c> carries it alone.</summary>
    [Fact]
    public async Task WithoutColourTheMarkChangesNothing()
    {
        var marked = await Drawn(Themes.Plain, Line.Of("picked", Role.Body) with { Picked = true });
        var unmarked = await Drawn(Themes.Plain, Line.Of("picked", Role.Body));

        Assert.Equal(Row(unmarked, 0), Row(marked, 0));
    }

    /// <summary>
    ///     On a banded row a role sitting on the page is drawn on the band instead, in its own foreground — the theme's
    ///     answer rather than one the view puts together (ADR-0014).
    /// </summary>
    [Fact]
    public void ARoleOnThePageIsBandedInItsOwnForeground()
    {
        var banded = Themes.Dark.Banded(Role.Body);

        Assert.Equal(Themes.Dark.For(Role.Body).Foreground, banded.Foreground);
        Assert.Equal(Themes.Dark.For(Role.Band).Background, banded.Background);
    }

    /// <summary>
    ///     A role with a background of its own keeps it, even one the same colour as the page: whether it has one is
    ///     the theme's to know, not something to be guessed by comparing colours.
    /// </summary>
    [Fact]
    public void ARoleWithABackgroundOfItsOwnKeepsItEvenWhereItIsThePagesColour()
    {
        var theme = Themes.Chosen(
            new WoolyConfig
            {
                Theme = "midnight",
                Themes = new Dictionary<string, ThemeConfig>
                {
                    ["midnight"] = new()
                    {
                        Background = "#12111a",
                        Roles = new Dictionary<string, ThemeRole>
                        {
                            ["reference-picked"] = new("#ffffff", "#12111a"),
                        },
                    },
                },
            },
            "/somewhere/config.toml");

        Assert.Equal(theme.For(Role.ReferencePicked), theme.Banded(Role.ReferencePicked));
    }

    /// <summary>Without colour there is no band to draw on, so a banded role is answered as it always is.</summary>
    [Fact]
    public void WithoutColourABandedRoleIsAnsweredAsItAlwaysIs()
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            Assert.Equal(Themes.Plain.For(role), Themes.Plain.Banded(role));
        }
    }

    /// <summary>One of each screen, each with three things on it to pick out.</summary>
    private static Screen Of(string kind)
    {
        Post[] posts = [APost.With(id: "1"), APost.With(id: "2"), APost.With(id: "3")];

        return kind switch
        {
            "feed" => new FeedScreen(
                new Destination(DestinationKind.Home, "Home", Wooly.Core.Timelines.Timeline.Home),
                posts),
            "post" => new PostScreen(posts[0], new PostThread([], [posts[1], posts[2]])),
            "notifications" => new NotificationsScreen(
                [ANotification.With(id: "1"), ANotification.Follow(id: "2"), ANotification.With(id: "3")]),
            "conversation" => new ConversationScreen(AConversation.Thread(
                AConversation.With(),
                AConversation.DirectPost("1"),
                AConversation.DirectPost("2"),
                AConversation.DirectPost("3"))),
            _ => new AccountScreen(AnAccount.With(), [posts[0], posts[1]], pinned: []),
        };
    }

    /// <summary>One row's cells, as attributes, left to right.</summary>
    private static Attribute[] Row(Cell[,] cells, int row) =>
        [.. Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Attribute!.Value)];

    /// <summary><paramref name="line" /> drawn once, alone, by the view that paints everything, at 20×1.</summary>
    private static async Task<Cell[,]> Drawn(ITheme theme, Line line)
    {
        await Task.Yield();

        using var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(20, 1);

        using var window = new Runnable { BorderStyle = LineStyle.None };
        window.Add(new PaintedView(theme, (_, _) => [line]) { Width = 20, Height = 1 });

        application.Begin(window);
        application.LayoutAndDraw(true);

        return (Cell[,])application.Driver.Contents!.Clone();
    }
}
