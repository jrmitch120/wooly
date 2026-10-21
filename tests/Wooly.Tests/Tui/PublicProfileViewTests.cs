using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The edit public profile form on a terminal (#406): its fields are real fields laid over the rows the screen keeps
///     for them, typed into, walked with the arrows and clicked, and it reads with no colour.
/// </summary>
public class PublicProfileViewTests
{
    private const int Wide = 80;

    private const int Tall = 30;

    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>The shell on a terminal with no colour, the form opened with <c>E</c> on <b>You</b>.</summary>
    private static async Task<DrawnShell> Editing(PublicProfile? read = null, int tall = Tall)
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(displayName: "Jeff", account: Me)),
        };

        var drawn = await DrawnShell.Of(Wide, tall, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

        return drawn;
    }

    private static PublicProfileScreen Form(DrawnShell drawn) => Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    /// <summary>
    ///     With no colour at all, the form still says what each row is and which way each toggle is, and the fields laid
    ///     over the rows show what was read — the bio at the foot between hairlines, as compose's post is, with its count
    ///     under it; and no bar anywhere, the caret saying where the typing is.
    /// </summary>
    [Fact]
    public async Task TheForm_ReadsWithNoColour()
    {
        using var drawn = await Editing(APublicProfile.With(
            displayName: "Jeff",
            bio: "Writes Wooly.",
            fields: [new PublicProfileField("Site", "https://wooly.example")],
            isBot: true,
            account: Me));

        var rows = drawn.Rows();

        Assert.Contains(rows, row => row.Contains("│    Name  Jeff ", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("│  Writes Wooly. ", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("│  ────", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("13 / 500  │", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains('▌'));
        Assert.Contains(rows, row => row.Contains("  Fields  Site ", StringComparison.Ordinal) && row.Contains("https://wooly.example", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("□ Locked · approve followers by hand", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("■ Bot · posts automatically", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The form opens with the typing in the display name, filled from what was read: what is typed goes on the end
    ///     of it, letters the shell would otherwise take as keys included.
    /// </summary>
    [Fact]
    public async Task Typing_GoesIntoTheDisplayName()
    {
        using var drawn = await Editing();

        Type(drawn, " ?Eb");

        Assert.Equal("Jeff ?Eb", Form(drawn).DisplayName);
        Assert.Contains(drawn.Rows(), row => row.Contains("Name  Jeff ?Eb", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>↓</c> walks the typing from the display name over the avatar and the header and on into the custom fields,
    ///     where what is typed goes into the field walked to, and past the toggles into the bio at the foot.
    /// </summary>
    [Fact]
    public async Task TheArrows_WalkTheTypingFromFieldToField()
    {
        using var drawn = await Editing(APublicProfile.With(bio: "One line.", fields: [], account: Me));

        drawn.Press(Key.CursorDown);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Avatar), Form(drawn).Typing);

        drawn.Press(Key.CursorDown);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Header), Form(drawn).Typing);

        drawn.Press(Key.CursorDown);
        Type(drawn, "Home");
        drawn.Press(Key.CursorDown);
        Type(drawn, "Kansas");

        Assert.Equal("Home", Form(drawn).LabelOf(0));
        Assert.Equal("Kansas", Form(drawn).ValueOf(0));

        while (Form(drawn).Typing.Part != PublicProfilePart.Bio)
        {
            drawn.Press(Key.CursorDown);
        }

        Assert.True(drawn.Window.ProfileField<ComposeEditor>().HasFocus);

        for (var press = 0; press < 20; press++)
        {
            drawn.Press(Key.CursorUp);
        }

        Assert.Equal(new PublicProfileStop(PublicProfilePart.DisplayName), Form(drawn).Typing);
    }

    /// <summary>A click on a toggle flips it and takes the walk there, and its box says so with no colour.</summary>
    [Fact]
    public async Task AClickOnAToggle_FlipsIt()
    {
        using var drawn = await Editing(APublicProfile.With(isLocked: false, account: Me));
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, drawn => drawn.Contains("□ Locked", StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        var column = rows[row].IndexOf("□ Locked", StringComparison.Ordinal);

        drawn.Click(column, row);

        Assert.True(Form(drawn).IsLocked);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Locked), Form(drawn).Typing);
        Assert.Contains("■ Locked", drawn.Rows()[row], StringComparison.Ordinal);

        drawn.Press(Key.Space);

        Assert.False(Form(drawn).IsLocked);
    }

    /// <summary>
    ///     On a terminal too short for the whole form, the field the walk is on is always on screen: walked down to the
    ///     last toggle it is there to be seen, lit, and flipped, the bio below it is there to be typed in, and walked
    ///     back up the display name is there again (review of #403).
    /// </summary>
    [Fact]
    public async Task OnAShortTerminal_TheWalkedFieldIsAlwaysOnScreen()
    {
        using var drawn = await Editing(APublicProfile.With(isDiscoverable: false, account: Me), tall: 14);

        while (Form(drawn).Typing.Part != PublicProfilePart.Discoverable)
        {
            drawn.Press(Key.CursorDown);
        }

        drawn.Redraw();

        Assert.Contains(drawn.Rows(), row => row.Contains("          □ Discoverable", StringComparison.Ordinal));

        drawn.Press(Key.Space);
        drawn.Redraw();

        Assert.True(Form(drawn).IsDiscoverable);
        Assert.Contains(drawn.Rows(), row => row.Contains("          ■ Discoverable", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);
        drawn.Press(new Key('?'));
        drawn.Redraw();

        Assert.Equal("?Writes Wooly.", Form(drawn).Bio);
        Assert.Contains(drawn.Rows(), row => row.Contains("│  ?Writes Wooly.", StringComparison.Ordinal));

        while (Form(drawn).Typing.Part != PublicProfilePart.DisplayName)
        {
            drawn.Press(Key.CursorUp);
        }

        drawn.Redraw();

        Assert.Contains(drawn.Rows(), row => row.Contains("│    Name  Jeff", StringComparison.Ordinal));

        drawn.Press(new Key('!'));

        Assert.Equal("Jeff!", Form(drawn).DisplayName);
    }

    /// <summary><c>ctrl-s</c> from inside a field saves what was typed there.</summary>
    [Fact]
    public async Task CtrlS_InAField_Saves()
    {
        using var drawn = await Editing();

        Type(drawn, " M");
        drawn.Press(Key.S.WithCtrl);
        drawn.Settle();

        Assert.Equal(
            [new PublicProfileChange { DisplayName = "Jeff M" }],
            drawn.Built.PublicProfile.Saves);
        Assert.IsType<AccountScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     <c>esc</c> from inside a touched field asks before the form is thrown away, and the field takes no letter
    ///     while the question is open: <c>n</c> answers it and keeps the form.
    /// </summary>
    [Fact]
    public async Task Esc_InATouchedField_Asks()
    {
        using var drawn = await Editing();

        Type(drawn, "!");
        drawn.Press(Key.Esc);

        Assert.Equal("Discard these changes?", drawn.Shell.Asking?.Ask);

        drawn.Press(new Key('n'));

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("Jeff!", Form(drawn).DisplayName);
    }

    /// <summary>A right click anywhere over the form is <c>esc</c>, a field included: an untouched form leaves.</summary>
    [Fact]
    public async Task ARightClickOverAField_Leaves()
    {
        using var drawn = await Editing();
        var field = drawn.Window.ProfileField<ComposeEditor>().FrameToScreen();

        drawn.RightClick(field.X, field.Y);

        Assert.IsType<AccountScreen>(drawn.Shell.Screen);
    }
}
