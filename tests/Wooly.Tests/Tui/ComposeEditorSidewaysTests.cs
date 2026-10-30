using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A sideways notch over an editor moves nothing. The editor wraps its text to its width, so there is nothing off
///     either side to scroll to, and a trackpad drifting sideways must not carry the text off the field — as it did, a
///     column a notch, in compose's post and the edit public profile form's bio alike.
/// </summary>
public class ComposeEditorSidewaysTests
{
    private const string Written = "sheep in the meadow";

    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>Notches right, with the odd one back left among them, as a drifting finger sends.</summary>
    private static void Drift(DrawnShell drawn, ComposeEditor editor)
    {
        var at = editor.FrameToScreen();

        for (var notch = 0; notch < 5; notch++)
        {
            drawn.WheelSideways(at.X + 2, at.Y);
            drawn.WheelSideways(at.X + 2, at.Y, right: false);
            drawn.WheelSideways(at.X + 2, at.Y);
        }

        drawn.Redraw();
    }

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    [Fact]
    public async Task OverThePost_TheTextStaysWhereItWas()
    {
        using var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, new AShell());

        ComposeRows.Open(drawn.Shell, ComposeFor.Post);
        drawn.Redraw();

        var editor = drawn.Window.ComposeField<ComposeEditor>();

        Type(drawn, Written);
        Drift(drawn, editor);

        Assert.Equal(0, editor.Viewport.X);
        Assert.Contains(Written, drawn.Rows()[editor.FrameToScreen().Y], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverThePublicProfilesBio_TheTextStaysWhereItWas()
    {
        using var drawn = await DrawnShell.Of(80, 30, Themes.Plain, new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: Written, account: Me)),
        });

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        var editor = drawn.Window.ProfileField<ComposeEditor>();

        Drift(drawn, editor);

        Assert.Equal(0, editor.Viewport.X);
        Assert.Contains(Written, drawn.Rows()[editor.FrameToScreen().Y], StringComparison.Ordinal);
    }
}
