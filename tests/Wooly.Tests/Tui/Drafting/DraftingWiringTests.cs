using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     How the drawn shell is wired to drafting (ADR-0029, #427): every verb is asked of drafting first through one arm
///     of the shell's verb route, drafting is told of every screen as it comes off the stack, by every way off, and of
///     the screen uncovered where <c>esc</c> or a walk back leaves one in front.
/// </summary>
public class DraftingWiringTests
{
    [Fact]
    public async Task ADraftVerbIsCarriedOutByDraftingAndUsesThePress()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);

        Assert.True(drawn.Shell.Do(Verb.Compose, answer: null));
        drawn.Redraw();

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Contains("Compose", drawn.Rows()[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVerbThatIsNotADraftsIsCarriedOnWithByTheShell()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);

        drawn.Press(Key.P.WithCtrl);

        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen);
    }

    [Fact]
    public async Task EscTellsDraftingOfTheScreenLeavingAndOfTheOneUncovered()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        var told = Told(drawn);

        drawn.Press(Key.Esc);

        Assert.Equal(["Compose leaving", "Home uncovered"], told);
    }

    [Fact]
    public async Task WalkingBackTellsDraftingOfTheScreenLeavingAndOfTheOneUncovered()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        var told = Told(drawn);

        drawn.Shell.WalkBack(0);

        Assert.Equal(["Compose leaving", "Home uncovered"], told);
    }

    [Fact]
    public async Task ASendThatLandsTellsDraftingOfTheComposeLeaving()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        drawn.Press(new Key('h'));
        drawn.Press(new Key('i'));
        var told = Told(drawn);

        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        Assert.Contains("Compose leaving", told);
        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
    }

    [Fact]
    public async Task ArrivingAtADestinationTellsDraftingOfEveryScreenLeaving()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        var told = Told(drawn);

        drawn.Shell.Arrive(1);
        drawn.Settle();

        Assert.Equal(["Home leaving", "Compose leaving"], told.Take(2));
    }

    [Fact]
    public async Task OpeningTheProfilesOverADraftTellsDraftingOfTheDraftLeaving()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        var told = Told(drawn);

        drawn.Press(Key.P.WithCtrl);

        Assert.Equal(["Compose leaving"], told);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen);
    }

    [Fact]
    public async Task ARefreshTellsDraftingOfTheScreenItReplaces()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        var told = Told(drawn);

        drawn.Press(new Key('g'));
        drawn.Settle();

        Assert.Equal(["Home leaving"], told);
    }

    /// <summary>What drafting is told from here on: each screen leaving, and each uncovered, by its crumb.</summary>
    private static List<string> Told(DrawnShell drawn)
    {
        var told = new List<string>();

        drawn.Shell.Drafting.Leaving += screen => told.Add($"{screen.Crumb} leaving");
        drawn.Shell.Drafting.Uncovering += () => told.Add($"{drawn.Shell.Screen.Crumb} uncovered");

        return told;
    }
}
