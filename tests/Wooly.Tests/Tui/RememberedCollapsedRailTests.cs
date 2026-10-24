using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>collapsed rail</b> remembered (#442): each toggle saves which rail shows, and a launch opens on what was
///     saved, as the <b>Shell width</b> is remembered. Pressed and read back off a headless terminal, with what was
///     saved read off the port the shell saves it through.
/// </summary>
public class RememberedCollapsedRailTests
{
    /// <summary>The key that collapses and expands the rail.</summary>
    private static readonly Key Toggle = new('\\');

    [Fact]
    public async Task EachToggleSavesTheRailItLeftShowing()
    {
        var built = new AShell();
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Press(Toggle);

        Assert.Equal([RailState.Hidden], built.ShellPreferences.Rails);

        drawn.Press(Toggle);
        drawn.Press(Toggle);

        Assert.Equal([RailState.Hidden, RailState.Shown, RailState.Hidden], built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     A launch opens on the collapsed rail where that is what was saved, the content panel starting where the
    ///     collapsed rail ends — and saves nothing, as opening on it is not a toggle. <c>\</c> then expands it.
    /// </summary>
    [Fact]
    public async Task ALaunchOpensOnTheSavedCollapsedRail()
    {
        var built = new AShell { HiddenRail = true };
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built, launch: true);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal(7, ContentStarts(drawn));
        Assert.Empty(built.ShellPreferences.Rails);

        drawn.Press(Toggle);

        Assert.Equal(RailLines.Width, ContentStarts(drawn));
        Assert.Equal([RailState.Shown], built.ShellPreferences.Rails);
    }

    /// <summary>Where nothing was saved, as on a fresh install, a launch opens on the expanded rail.</summary>
    [Fact]
    public async Task ALaunchWithNothingSavedOpensOnTheExpandedRail()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, new AShell(), launch: true);

        Assert.Equal(RailState.Shown, drawn.Shell.RailState);
        Assert.Equal(RailLines.Width, ContentStarts(drawn));
    }

    /// <summary>
    ///     A config file edited by hand, while the TUI runs, into something that no longer reads cannot be saved to.
    ///     The status row says why, as it does for a width, and the rail stays collapsed for this session, since it was
    ///     seen.
    /// </summary>
    [Fact]
    public async Task AToggleThatCannotBeSavedSaysWhy_AndStillHoldsForTheSession()
    {
        var built = new AShell();
        built.ShellPreferences.Unreadable = "'bell' must be true or false.";
        using var drawn = await DrawnShell.Of(80, 24, Themes.Dark, built);

        drawn.Press(Toggle);

        Assert.Equal(RailState.Hidden, drawn.Shell.RailState);
        Assert.Equal(7, ContentStarts(drawn));
        Assert.Contains("'bell' must be true or false.", drawn.Shell.Notice, StringComparison.Ordinal);
        Assert.True(drawn.Shell.NoticeIsError);
        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Where there is no rail — nobody acted as, the add screen alone on a launch — <c>\</c> collapses nothing, so
    ///     it saves nothing either: a setting the reader cannot see is not changed behind their back.
    /// </summary>
    [Fact]
    public async Task WithNoRailBackslashSavesNothing()
    {
        var registry = FakeProfileRegistry.Holding(
            "work",
            FakeProfileRegistry.Profile("work", "mastodon.social", "jeff@mastodon.social"));
        registry.Tokenless.Add("work");

        var built = new AShell { Profiles = registry };
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built, launch: true);

        Assert.False(drawn.Shell.ShowsRail);

        drawn.Press(Key.Esc);
        drawn.Press(Toggle);

        Assert.Empty(built.ShellPreferences.Rails);
    }

    /// <summary>
    ///     Where the content panel's top-left corner is drawn, which is the column after the rail's last.
    /// </summary>
    private static int ContentStarts(DrawnShell drawn) => drawn.Rows()[0].IndexOf("╭ Home", StringComparison.Ordinal);
}
