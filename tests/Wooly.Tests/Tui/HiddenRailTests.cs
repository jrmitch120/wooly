using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.RailHeights;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>hidden rail</b> (#445): <c>\</c> puts the rail out of sight, so the content panel takes the whole width
///     of the shell, and shows it again. Pressed and read back off a headless terminal, since the promise is about
///     where the cells land.
/// </summary>
public class HiddenRailTests
{
    /// <summary>The key that hides and shows the rail.</summary>
    private static readonly Key Toggle = new('\\');

    /// <summary>
    ///     <c>\</c> hides the rail: the content panel and the breadcrumb on its edge start at the shell's first column,
    ///     and at 80 columns the panel is 80 wide. Pressed again, the rail is as it was.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BackslashHidesTheRailAndShowsItAgain(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured));

        drawn.Press(Toggle);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal(0, ContentStarts(drawn));
        Assert.Equal(80, drawn.Content.Frame.Width);
        Assert.EndsWith("╮", drawn.Rows()[0], StringComparison.Ordinal);

        drawn.Press(Toggle);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(RailLines.Width, ContentStarts(drawn));
        Assert.Equal(80 - RailLines.Width, drawn.Content.Frame.Width);
    }

    /// <summary>
    ///     Hidden means gone, not shrunk: no destination, no unread count — not even <c>40+</c> — no instance and no
    ///     budget is drawn anywhere above the status row, framed or compact, in colour or without.
    /// </summary>
    [Theory]
    [InlineData(Framed, true)]
    [InlineData(Framed, false)]
    [InlineData(Compact, true)]
    [InlineData(Compact, false)]
    [InlineData(Scrolled, true)]
    [InlineData(Scrolled, false)]
    public async Task NothingOfTheRailIsDrawnWhileItIsHidden(int height, bool coloured)
    {
        // An empty Home, so that nothing the content draws names the instance.
        var built = Twice();
        built.Timelines = FakeTimelineReader.Holding();
        using var drawn = await DrawnShell.Of(80, height, Theme(coloured), built);
        var rail = drawn.Shell.Rail;

        rail.Update(rail.Destinations.Single(place => place.Kind == DestinationKind.Notifications) with
        {
            Unread = 40,
            Full = true,
        });
        built.RateLimit.Said("mastodon.social", "token-personal", 213);
        drawn.Redraw();

        Assert.Contains(drawn.Rows(), row => row.Contains("40+", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("71%", StringComparison.Ordinal));

        drawn.Press(Toggle);

        var above = drawn.Rows()[..^1];

        Assert.All(
            ["40+", "%", "mastodon.social", "Timelines", "Notifications", "API", "▶", "█"],
            gone => Assert.DoesNotContain(above, row => row.Contains(gone, StringComparison.Ordinal)));
        Assert.All(above, row => Assert.Equal(80, row.Length));
        Assert.StartsWith("╭ Home", above[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The Inbox is still read while the rail is hidden, and a badge rising rings the bell as it does shown — only
    ///     the count is not drawn.
    /// </summary>
    [Fact]
    public async Task ARisingBadgeStillRingsTheBellWhileTheRailIsHidden()
    {
        var built = new AShell { Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "101")) };
        built.Notifications.Marker = "100";
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Dark, built);

        drawn.Press(Toggle);
        built.Notifications.NowHolding(ANotification.With(id: "102"), ANotification.With(id: "101"));

        for (var second = 0; second < 60; second++)
        {
            built.Clock.Advance(TimeSpan.FromSeconds(1));
            built.Host.Drain();
        }

        drawn.Redraw();

        Assert.Equal(1, built.Host.Bells);
        Assert.Equal(
            2,
            drawn.Shell.Rail.Destinations.Single(place => place.Kind == DestinationKind.Notifications).Unread);
        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
    }

    /// <summary>
    ///     Walking the hidden rail is locked, since a cursor nobody can see would land somewhere nobody chose:
    ///     <c>tab</c>, <c>shift-tab</c>, <c>`</c> and <c>~</c> move neither the cursor nor the selection, start no
    ///     settle window, and say on the status row how to get the rail back.
    /// </summary>
    [Theory]
    [InlineData("tab")]
    [InlineData("shift-tab")]
    [InlineData("`")]
    [InlineData("~")]
    public async Task WalkingTheHiddenRailIsLockedAndSaysHowToShowIt(string key)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(Toggle);
        drawn.Press(key switch
        {
            "tab" => Key.Tab,
            "shift-tab" => Key.Tab.WithShift,
            _ => new Key(key[0]),
        });

        Assert.Equal(0, drawn.Shell.Rail.Cursor);
        Assert.Contains(@"Rail hidden — \ to show", drawn.Rows()[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(ShellTiming.Default.Settle, drawn.Built.Host.Delays);

        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(0, drawn.Shell.Rail.Current);
        Assert.Equal(["Home"], drawn.Shell.Crumbs);

        drawn.Press(Toggle);
        drawn.Press(Key.Tab);

        Assert.Equal(1, drawn.Shell.Rail.Cursor);
    }

    /// <summary>
    ///     On Search while its prompt takes letters walking the hidden rail is as locked, and says how to show it as it
    ///     does anywhere: <c>\</c> shows the rail from the prompt too (#447).
    /// </summary>
    [Fact]
    public async Task OnTheSearchPromptTheLockedWalkSaysHowToShowIt()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(Toggle);
        drawn.Press(new Key('/'));
        drawn.Settle();

        Assert.True(drawn.Shell.Screen.IsTyping);

        var cursor = drawn.Shell.Rail.Cursor;

        drawn.Press(Key.Tab);

        Assert.Contains(@"Rail hidden — \ to show", drawn.Rows()[^1], StringComparison.Ordinal);
        Assert.Equal(cursor, drawn.Shell.Rail.Cursor);
    }

    /// <summary>
    ///     On the one-line prompts — Search's while it takes letters, and the follow list's filter — <c>\</c> hides a
    ///     shown rail and shows a hidden one rather than being typed, so that the keys never leave the rail out of reach
    ///     there (#447). Nothing is added to what was typed either way.
    /// </summary>
    [Theory]
    [InlineData("search")]
    [InlineData("filter")]
    public async Task OnAOneLinePromptBackslashHidesAndShowsTheRail(string where)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.Holding(
                AnAccount.With(address: "ben@hachyderm.io"),
                AnAccount.With(id: "1", address: "ann@hachyderm.io", author: "Ann")),
        };
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, built);

        if (where == "search")
        {
            drawn.Press(new Key('/'));
            drawn.Settle();
        }
        else
        {
            await drawn.Shell.OpenAuthor();
            built.Host.Drain();
            drawn.Press(Key.W);
            built.Host.Drain();
            drawn.Press(Key.F);
        }

        drawn.Press(new Key('a'));

        Assert.True(drawn.Shell.Screen.IsTyping);

        drawn.Press(Toggle);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal(0, drawn.Content.Frame.X);

        drawn.Press(Toggle);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(RailLines.Width, drawn.Content.Frame.X);
        Assert.True(drawn.Shell.Screen.IsTyping);
        Assert.Equal(
            "a",
            drawn.Shell.Screen switch
            {
                SearchScreen search => search.Query,
                FollowsScreen follows => follows.Filter,
                var other => throw new InvalidOperationException($"Not a prompt: {other.Crumb}"),
            });
    }

    /// <summary>
    ///     Going straight to a named destination still works hidden, since the breadcrumb says where it landed:
    ///     <c>/</c> arrives at Search, and a profile chosen on <c>ctrl-p</c> starts again on its Home.
    /// </summary>
    [Fact]
    public async Task SearchAndChoosingAProfileStillArriveWhileTheRailIsHidden()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, Twice());

        drawn.Press(Toggle);
        drawn.Press(new Key('/'));
        drawn.Settle();

        Assert.IsType<SearchScreen>(drawn.Shell.Screen);
        Assert.Equal(["Search"], drawn.Shell.Crumbs);
        Assert.StartsWith("╭ Search", drawn.Rows()[0], StringComparison.Ordinal);

        drawn.Press(Key.Esc);
        drawn.Press(Key.P.WithCtrl);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen).Pick("work");
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal("token-work", drawn.Built.Timelines.Tokens[^1]);
        Assert.Equal(["Home"], drawn.Shell.Crumbs);
        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.StartsWith("╭ Home", drawn.Rows()[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Hidden, the status row no longer offers the walk it has locked: the destination and group hints come off,
    ///     and <c>Rail: \</c> stands where they stood. Shown again, the row is as it was, and without <c>\</c>. On
    ///     Search while its prompt takes letters, where the destination hint is offered alone, <c>Rail: \</c> stands in
    ///     its place too, since <c>\</c> shows the rail from there (#447).
    /// </summary>
    [Fact]
    public async Task WhileTheRailIsHiddenTheStatusRowOffersBackslashInPlaceOfTheWalk()
    {
        using var drawn = await DrawnShell.Of(300, Framed, Themes.Plain);

        var shown = drawn.Rows()[^1];

        Assert.Contains("Destination: tab", shown, StringComparison.Ordinal);
        Assert.Contains("Group: `", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", shown, StringComparison.Ordinal);

        drawn.Press(Toggle);

        var hidden = drawn.Rows()[^1];

        Assert.DoesNotContain("Destination: tab", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("Group: `", hidden, StringComparison.Ordinal);
        Assert.Equal(
            shown.IndexOf("Destination", StringComparison.Ordinal),
            hidden.IndexOf(@"Rail: \", StringComparison.Ordinal));

        drawn.Press(Toggle);

        Assert.Equal(shown, drawn.Rows()[^1]);

        drawn.Press(Toggle);
        drawn.Press(new Key('/'));
        drawn.Settle();

        var searching = drawn.Rows()[^1];

        Assert.DoesNotContain("Destination: tab", searching, StringComparison.Ordinal);
        Assert.Contains(@"Rail: \", searching, StringComparison.Ordinal);
        Assert.Contains("Search: ⏎", searching, StringComparison.Ordinal);

        drawn.Press(Toggle);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
    }

    /// <summary>
    ///     Hidden in the middle of a walk, inside the settle window the walk started: the arrival it was waiting on
    ///     still lands where the cursor had got to.
    /// </summary>
    [Fact]
    public async Task HidingInsideASettleWindowStillLandsThePendingArrival()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(Key.Tab);
        drawn.Press(Toggle);

        Assert.Equal(0, drawn.Shell.Rail.Current);

        drawn.Built.Host.Settle();
        drawn.Redraw();

        Assert.Equal(1, drawn.Shell.Rail.Current);
        Assert.Equal(["Local"], drawn.Shell.Crumbs);
        Assert.StartsWith("╭ Local", drawn.Rows()[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     On compose, the description editor and the public profile form <c>\</c> is typed, as <c>-</c> and <c>+</c>
    ///     are: a backslash in the text, never a rail hidden from under the draft.
    /// </summary>
    [Theory]
    [InlineData("compose")]
    [InlineData("description")]
    [InlineData("public profile")]
    public async Task OnTheScreensWithFieldsBackslashIsTyped(string where)
    {
        using var files = new TemporaryDirectory();
        var me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");
        var profile = APublicProfile.With(displayName: string.Empty, account: me);
        using var drawn = await DrawnShell.Of(80, 30, Themes.Plain, new AShell
        {
            Accounts = FakeAccountRelationships.Holding(me),
            PublicProfile = FakePublicProfileEditor.Holding(profile),
        });

        switch (where)
        {
            case "compose":
                ComposeRows.Open(drawn.Shell, ComposeFor.Post);
                drawn.Redraw();
                break;

            case "description":
                drawn.Shell.Press(ShellKey.C);
                drawn.Shell.Paste(files.WriteFile("cat.png"));
                drawn.Settle();
                drawn.Press(Key.CursorUp);
                drawn.Press(Key.Enter);
                Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
                break;

            default:
                drawn.Shell.Rail.GoTo(DestinationKind.Profile);
                drawn.Settle();
                drawn.Press(new Key('E'));
                drawn.Settle();
                Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);
                break;
        }

        foreach (var letter in "a\\b")
        {
            drawn.Press(new Key(letter));
        }

        Assert.Contains(drawn.Rows(), row => row.Contains("a\\b", StringComparison.Ordinal));
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(RailLines.Width, drawn.Content.Frame.X);
    }

    /// <summary>
    ///     Where there is no rail — nobody acted as, the add screen alone on a launch — <c>\</c> hides nothing, so that
    ///     a rail coming back once a profile is signed in is the one the reader last saw.
    /// </summary>
    [Fact]
    public async Task WithNoRailBackslashHidesNothing()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, new AShell { Profiles = registry }, launch: true);

        Assert.False(drawn.Shell.ShowsRail);

        drawn.Press(Key.Esc);
        drawn.Press(Toggle);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(0, drawn.Content.Frame.X);
    }

    /// <summary>
    ///     With no rail the toggle itself does nothing — the shell's own rule, whatever screen is in front and whatever
    ///     asks: the rail stays shown, nothing is saved, and the content keeps every column.
    /// </summary>
    [Fact]
    public async Task WithNoRailTheToggleItselfDoesNothing()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        var built = new AShell { Profiles = registry };
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built, launch: true);

        Assert.False(drawn.Shell.ShowsRail);

        drawn.Shell.ToggleRail();
        drawn.Redraw();

        Assert.False(drawn.Shell.Do(Verb.ToggleRail, answer: null));
        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Empty(built.ShellPreferences.Rails);
        Assert.Equal(0, drawn.Content.Frame.X);
    }

    /// <summary>
    ///     On a toggle the content is laid out again at its new width — each post wrapping to fewer rows hidden — and
    ///     the post picked stays on the row of the page it was drawn on, both ways, rather than the page keeping its
    ///     row offset into rows that are now another shape.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AToggleKeepsThePickedPostOnItsRow(bool coloured)
    {
        using var drawn = await DrawnShell.Of(80, Framed, Theme(coloured), Long());

        for (var post = 0; post < 7; post++)
        {
            drawn.Press(Key.K);
        }

        var row = PickRow(drawn);
        var shown = drawn.Shell.Screen.Picked?.Id;
        var tall = PickRows(drawn);

        Assert.True(row > 1, "The pick is far enough down that the page has scrolled to it.");

        drawn.Press(Toggle);

        Assert.True(PickRows(drawn) < tall, $"{PickRows(drawn)} rows of {tall}");
        Assert.Equal(row, PickRow(drawn));
        Assert.Equal(shown, drawn.Shell.Screen.Picked?.Id);

        drawn.Press(Toggle);

        Assert.Equal(row, PickRow(drawn));
    }

    /// <summary>
    ///     A picture is laid out again at the content's new width and sent again in the frame the toggle lands — from
    ///     just past the panel's edge and gutter, to the column before the panel's right edge — and back.
    /// </summary>
    [Fact]
    public async Task AToggleLaysAPictureOutAgainAtTheNewWidth()
    {
        using var drawn = await DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(id: "110", media: [APost.APicture("m1", shape: new PictureShape(4000, 400))])),
            },
            // Wider than any content panel, so that the panel's width is what bounds its box.
            pictures: new FakePictures().Holding("m1", 4000, 400),
            drawsPictures: true);

        drawn.DrawAsTheLoopDraws();
        drawn.PressAsTheLoopDraws(Toggle);

        Assert.Equal(1, drawn.SixelsSent());
        Assert.Equal((2, 78), Across(drawn));

        drawn.PressAsTheLoopDraws(Toggle);

        Assert.Equal(1, drawn.SixelsSent());
        Assert.Equal((RailLines.Width + 2, 78), Across(drawn));
    }

    /// <summary>
    ///     With nothing picked — the keymap, which is rows of keys and no things — the page is left where it begins, so
    ///     what was at the top is still at the top.
    /// </summary>
    [Fact]
    public async Task WithNothingPickedAToggleLeavesThePageAtItsTop()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain);

        drawn.Press(new Key('?'));

        var top = drawn.Rows()[1][(RailLines.Width + 1)..^1].Trim();

        drawn.Press(Toggle);

        Assert.Equal(top, drawn.Rows()[1][1..^1].Trim());
    }

    /// <summary>
    ///     Drilled two screens into Home, a toggle leaves the stack and the breadcrumb alone, either way: hiding the
    ///     rail changes nothing about where the reader is.
    /// </summary>
    [Fact]
    public async Task AToggleLeavesTheDrillAlone()
    {
        using var drawn = await DrawnShell.Of(80, Framed, Themes.Plain, Long());

        drawn.Press(Key.Enter);
        drawn.Built.Host.Drain();
        await drawn.Shell.OpenAuthor();
        drawn.Built.Host.Drain();
        drawn.Redraw();

        var screen = drawn.Shell.Screen;
        var crumbs = drawn.Shell.Crumbs;
        var requests = drawn.Built.Requests;

        drawn.Press(Toggle);

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(crumbs, drawn.Shell.Crumbs);
        Assert.Equal(requests, drawn.Built.Requests);
        Assert.Contains(crumbs[^1], drawn.Rows()[0], StringComparison.Ordinal);

        drawn.Press(Toggle);

        Assert.Same(screen, drawn.Shell.Screen);
        Assert.Equal(crumbs, drawn.Shell.Crumbs);
        Assert.Equal(requests, drawn.Built.Requests);
    }

    /// <summary>
    ///     Inside a narrowed shell the hidden rail gives its columns to the content: the panel starts on the shell's
    ///     first column, past the margin, and still ends on the shell's last.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InsideANarrowedShellTheHiddenRailGivesItsWidthToTheContent(bool coloured)
    {
        using var drawn = await DrawnShell.Of(200, Framed, Theme(coloured));

        drawn.Press(new Key('-'));
        drawn.Press(Toggle);

        var top = drawn.Rows()[0];

        Assert.Equal(10, top.IndexOf("╭ Home", StringComparison.Ordinal));
        Assert.Equal(189, top.LastIndexOf('╮'));
        Assert.Equal(180, drawn.Content.Frame.Width);
    }

    /// <summary>The keymap lists <c>\</c> with the frame's keys, everywhere.</summary>
    [Fact]
    public async Task TheKeymapListsBackslashWithTheFrame()
    {
        using var drawn = await DrawnShell.Of(80, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        var rows = drawn.Rows();
        var everywhere = Array.FindIndex(rows, row => row.Contains("Everywhere", StringComparison.Ordinal));
        var key = Array.FindIndex(rows, row => row.Contains("│\\ ", StringComparison.Ordinal));

        Assert.InRange(everywhere, 0, key - 1);
        Assert.Contains("hide or show the rail", rows[key], StringComparison.Ordinal);
    }

    /// <summary>
    ///     And says that <c>\</c> does so from a search or a filter too, since nothing else says a backslash cannot be
    ///     typed into one (#447).
    /// </summary>
    [Fact]
    public async Task TheKeymapSaysBackslashShowsTheRailFromAPromptToo()
    {
        using var drawn = await DrawnShell.Of(160, 40, Themes.Plain);

        drawn.Press(new Key('?'));

        const string said = @"\               hide or show the rail, even from a search or a filter";

        Assert.Contains(drawn.Rows(), row => row.Contains(said, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The first row of the page the pick is drawn on, by the bar in the content panel's first column.
    /// </summary>
    private static int PickRow(DrawnShell drawn)
    {
        var column = ContentStarts(drawn) + 1;

        return Array.FindIndex(drawn.Rows(), row => row[column] == '▌');
    }

    /// <summary>How many rows of the page the pick is drawn on.</summary>
    private static int PickRows(DrawnShell drawn)
    {
        var column = ContentStarts(drawn) + 1;

        return drawn.Rows().Count(row => row[column] == '▌');
    }

    /// <summary>The first and last column the sixels drawn take, as far as each is let be drawn.</summary>
    private static (int First, int Last) Across(DrawnShell drawn)
    {
        var columns = drawn.Application.Driver!.GetOutputBuffer().GetRasterImages()
            .SelectMany(image => (image.Clip?.GetRectangles() ?? [image.DestinationCells])
                .Select(clip => System.Drawing.Rectangle.Intersect(clip, image.DestinationCells)))
            .Where(visible => !visible.IsEmpty)
            .ToList();

        return (columns.Min(visible => visible.Left), columns.Max(visible => visible.Right - 1));
    }

    /// <summary>
    ///     A Home of twelve posts each long enough to wrap to fewer rows once the content panel is wider.
    /// </summary>
    private static AShell Long() => new()
    {
        Timelines = FakeTimelineReader.Holding(
        [
            .. Enumerable.Range(1, 12).Select(post => APost.With(
                id: $"{post}0",
                content: string.Join(' ', Enumerable.Repeat("lorem ipsum dolor sit amet", 10)))),
        ]),
    };

    /// <summary>Two profiles, so that the shown rail's foot names the instance, and the hidden rail none.</summary>
    private static AShell Twice() => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>
    ///     Where the content panel's top-left corner is drawn, which is the column after the rail's last, or the
    ///     shell's first with the rail hidden.
    /// </summary>
    private static int ContentStarts(DrawnShell drawn) => drawn.Rows()[0].IndexOf("╭ Home", StringComparison.Ordinal);

    private static ITheme Theme(bool coloured) => coloured ? Themes.Dark : Themes.Plain;
}
