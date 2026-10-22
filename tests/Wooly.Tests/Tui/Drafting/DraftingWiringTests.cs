using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     How the drawn shell is wired to drafting (ADR-0029, #427, #420): every verb is asked of drafting first through one
///     arm of the shell's verb route, the window carries <c>ctrl-s</c> to it, and drafting is told of every screen as it
///     comes off the stack, by every way off, and of the screen uncovered where <c>esc</c> or a walk back leaves one in
///     front — which it shows by letting a compose's uploads go, and by saying a waiting send again.
/// </summary>
public class DraftingWiringTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

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
    public async Task CtrlSIsCarriedByTheWindowToDraftingWhichSends()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Press(new Key('c'));
        drawn.Press(new Key('h'));
        drawn.Press(new Key('i'));

        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        Assert.Equal("hi", Assert.Single(drawn.Built.Author.Published).Draft.Text);
        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal("Sent.", drawn.Shell.Notice);
    }

    [Fact]
    public async Task EscOffAComposeLetsItsUploadsGo()
    {
        using var drawn = await Attached();

        drawn.Press(Key.Esc);
        drawn.Press(Key.Y);

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
        Assert.True(drawn.Built.Author.Attaching.Single().CalledOff);
    }

    [Fact]
    public async Task WalkingBackOffAComposeLetsItsUploadsGo()
    {
        using var drawn = await Attached();

        drawn.Shell.WalkBack(0);
        drawn.Press(Key.Y);

        Assert.IsNotType<ComposeScreen>(drawn.Shell.Screen);
        Assert.True(drawn.Built.Author.Attaching.Single().CalledOff);
    }

    [Fact]
    public async Task ArrivingAtADestinationLetsAComposesUploadsGo()
    {
        using var drawn = await Attached();

        drawn.Shell.Arrive(1);
        drawn.Press(Key.Y);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.True(drawn.Built.Author.Attaching.Single().CalledOff);
    }

    [Fact]
    public async Task SwitchingProfileLetsEveryUploadGo()
    {
        using var drawn = await DrawnShell.Of(
            80,
            24,
            Themes.Plain,
            new AShell
            {
                Profiles = FakeProfileRegistry.Holding(
                    "personal",
                    FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                    FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
            });

        drawn.Press(new Key('c'));
        Assert.True(drawn.Shell.Paste(_files.WriteFile("one.png")));
        Assert.True(drawn.Shell.Paste(_files.WriteFile("two.png")));

        drawn.Press(Key.P.WithCtrl);
        drawn.Press(Key.Y);
        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen).Pick("work");
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal("hachyderm.io", drawn.Shell.Instance);
        Assert.Equal(2, drawn.Built.Author.Attaching.Count);
        Assert.All(drawn.Built.Author.Attaching, sending => Assert.True(sending.CalledOff));
    }

    [Fact]
    public async Task LeavingTheDescriptionEditorSendsWhatItHolds()
    {
        using var drawn = await Attached();
        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();
        OpenTheDescriptionEditor(drawn);

        drawn.Shell.Drafting.Write("A cat");
        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal("A cat", Assert.Single(drawn.Built.Author.Descriptions).Description);
    }

    [Fact]
    public async Task EscBackOntoAComposeWithASendWaitingSaysItAgain()
    {
        using var drawn = await Attached();
        Assert.IsType<ComposeScreen>(drawn.Shell.Screen).Rewrite("Watch this");
        drawn.Press(Key.S.WithCtrl);
        OpenTheDescriptionEditor(drawn);

        Assert.Null(drawn.Shell.Notice);

        drawn.Press(Key.Esc);

        Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        Assert.Equal("Will send once 1 attachment finishes — esc to stop.", drawn.Shell.Notice);
    }

    [Fact]
    public async Task EscWithASendWaitingCallsItOffThroughDrafting()
    {
        using var drawn = await Attached();
        var compose = Assert.IsType<ComposeScreen>(drawn.Shell.Screen);
        compose.Rewrite("Watch this");
        drawn.Press(Key.S.WithCtrl);

        drawn.Press(Key.Esc);

        Assert.Same(compose, drawn.Shell.Screen);
        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("Not sent — the draft is as it was.", drawn.Shell.Notice);

        drawn.Built.Author.Attaching.Single().Ready();
        drawn.Settle();

        Assert.Empty(drawn.Built.Author.Published);
    }

    /// <summary>
    ///     A post sent, its compose left and something else opened before the instance answers: the answer lands without
    ///     taking off what the reader went on to, and says it was sent (#423).
    /// </summary>
    [Fact]
    public async Task ASendLandingAfterItsComposeHasLeftLeavesWhatTheReaderOpenedInFront()
    {
        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain);
        drawn.Built.Author.HoldingPublishes = true;
        drawn.Press(new Key('c'));
        drawn.Press(new Key('h'));
        drawn.Press(new Key('i'));
        drawn.Press(Key.S.WithCtrl);

        drawn.Press(Key.Esc);
        drawn.Press(Key.Y);
        drawn.Press(Key.P.WithCtrl);
        drawn.Redraw();
        var opened = drawn.Rows()[0];

        drawn.Built.Author.HeldPublishes.Single().Land();
        drawn.Settle();
        drawn.Redraw();

        Assert.IsType<ProfilesScreen>(drawn.Shell.Screen);
        Assert.Equal(opened, drawn.Rows()[0]);
        Assert.Equal("Sent.", drawn.Shell.Notice);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>A drawn shell with a compose open, cat.png dropped on it and still going up.</summary>
    private async Task<DrawnShell> Attached()
    {
        var drawn = await DrawnShell.Of(80, 24, Themes.Plain);

        drawn.Press(new Key('c'));
        drawn.Redraw();

        Assert.True(drawn.Shell.Paste(_files.WriteFile("cat.png")));

        drawn.Redraw();

        return drawn;
    }

    /// <summary>Walks up onto the only row and opens the description editor on it, as the keys do.</summary>
    private static void OpenTheDescriptionEditor(DrawnShell drawn)
    {
        drawn.Redraw();
        drawn.Press(Key.CursorUp);
        drawn.Press(Key.Enter);

        Assert.IsType<DescriptionScreen>(drawn.Shell.Screen);
    }
}
