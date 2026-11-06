using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The <b>Bio</b> on the edit public profile form painted as it is typed (#573), as compose's post body is (#572):
///     its hashtags, mentions and addresses drawn in their roles, since a bio's references are walked once someone reads
///     it (ADR-0019), and everything else in the body role. Driven through the drawn form, as
///     <see cref="PublicProfileViewTests" /> is.
/// </summary>
public class PublicProfileReferencePaintTests
{
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>The shell in the dark theme, the form opened with <c>E</c> on <b>You</b> and walked down to an empty bio.</summary>
    private static async Task<DrawnShell> InTheBio()
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(displayName: "Jeff", bio: "", fields: [], account: Me)),
        };

        var drawn = await DrawnShell.Of(80, 30, Themes.Dark, built);

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        while (Form(drawn).Typing.Part != PublicProfilePart.Bio)
        {
            drawn.Press(Key.CursorDown);
        }

        return drawn;
    }

    private static PublicProfileScreen Form(DrawnShell drawn) => Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

    private static ComposeEditor Bio(DrawnShell drawn) => drawn.Window.ProfileField<ComposeEditor>();

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    /// <summary>A hashtag, a mention and an address typed into the bio are drawn in their roles, and the rest is the body.</summary>
    [Fact]
    public async Task AHashtagAMentionAndAnAddressInTheBio_AreDrawnInTheirRoles()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi #rustlang @maria@hachyderm.io https://a.io/x ok");
        drawn.Redraw();

        Assert.Equal("hi #rustlang @maria@hachyderm.io https://a.io/x ok", Form(drawn).Bio);

        AssertDrawn(
            drawn,
            ("hi ", Role.Body),
            ("#rustlang", Role.Hashtag),
            (" ", Role.Body),
            ("@maria@hachyderm.io", Role.Mention),
            (" ", Role.Body),
            ("https://a.io/x", Role.Link),
            (" ok", Role.Body));
    }

    /// <summary>Selected text in the bio stays the selected-text role, over a reference as over anything else.</summary>
    [Fact]
    public async Task ASelectionOverAReferenceInTheBio_StaysTheSelection()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi #rustlang");
        Bio(drawn).SelectAll();
        drawn.Redraw();

        AssertDrawn(drawn, ("hi #rustlang", Role.SelectedText));
    }

    /// <summary>The cells of the bio's first row, from its first column, are each run of text in its role, in turn.</summary>
    private static void AssertDrawn(DrawnShell drawn, params (string Text, Role Role)[] runs) =>
        DrawnRuns.AssertDrawn(drawn.Application.Driver!.Contents!, Bio(drawn), 0, Themes.Dark, runs);
}
