using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
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
    private static async Task<DrawnShell> Editing(PublicProfile? read = null, int tall = Tall, Raster? raster = null)
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(displayName: "Jeff", account: Me)),
        };

        var drawn = await DrawnShell.Of(Wide, tall, Themes.Plain, built, raster: raster);

        drawn.Shell.Rail.GoTo(drawn.Shell.Rail.Of(DestinationKind.Profile));
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
        drawn.Press(Key.CursorRight);
        Type(drawn, "Kansas");

        Assert.Equal("Home", Form(drawn).LabelOf(0));
        Assert.Equal("Kansas", Form(drawn).ValueOf(0));

        // ← moves the caret through the value, and off its start crosses back to the label, the caret at its end.
        for (var press = 0; press < "Kansas".Length; press++)
        {
            drawn.PressThroughTheApplication(Key.CursorLeft);
            Assert.Equal(new PublicProfileStop(PublicProfilePart.Value, 0), Form(drawn).Typing);
        }

        drawn.PressThroughTheApplication(Key.CursorLeft);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Label, 0), Form(drawn).Typing);
        Type(drawn, "town");
        Assert.Equal("Hometown", Form(drawn).LabelOf(0));

        // → off the label's end crosses to the value with the caret at its start, so ← goes straight back.
        drawn.PressThroughTheApplication(Key.CursorRight);
        Type(drawn, "W ");
        Assert.Equal("W Kansas", Form(drawn).ValueOf(0));
        drawn.PressThroughTheApplication(Key.Home);
        drawn.PressThroughTheApplication(Key.CursorLeft);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Label, 0), Form(drawn).Typing);

        // ⏎ walks on in the order the form is read: the label to its value.
        drawn.PressThroughTheApplication(Key.Enter);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Value, 0), Form(drawn).Typing);

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

    /// <summary>
    ///     <c>←</c> and <c>→</c> never walk up or down: on a picture or a toggle they are nobody's, and in the display
    ///     name or the bio they move the caret and stop at either end of what is written rather than carrying the walk
    ///     off to another field. A custom field's label and value, which they do cross between, are
    ///     <see cref="TheArrows_WalkTheTypingFromFieldToField" />'s.
    /// </summary>
    [Fact]
    public async Task LeftAndRight_StayOnTheFieldWalkedTo()
    {
        using var drawn = await Editing(APublicProfile.With(bio: "One line.", fields: [], account: Me), raster: ARaster.Sixel());
        var walked = new List<PublicProfileStop>();

        do
        {
            var on = Form(drawn).Typing;
            var focused = drawn.Window.MostFocused;
            walked.Add(on);

            if (on.Part is PublicProfilePart.Label or PublicProfilePart.Value)
            {
                drawn.PressThroughTheApplication(Key.CursorDown);

                continue;
            }

            for (var press = 0; press < 3; press++)
            {
                drawn.PressThroughTheApplication(Key.CursorLeft);
                Assert.Equal(on, Form(drawn).Typing);
                Assert.Same(focused, drawn.Window.MostFocused);
            }

            for (var press = 0; press < 30; press++)
            {
                drawn.PressThroughTheApplication(Key.CursorRight);
                Assert.Equal(on, Form(drawn).Typing);
                Assert.Same(focused, drawn.Window.MostFocused);
            }

            drawn.PressThroughTheApplication(Key.CursorDown);
        }
        while (Form(drawn).Typing != walked[^1]);

        Assert.Contains(new PublicProfileStop(PublicProfilePart.Avatar), walked);
        Assert.Contains(new PublicProfileStop(PublicProfilePart.Locked), walked);
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

    /// <summary>Every place on the form the typing can be, a custom field's label and value included.</summary>
    public static TheoryData<PublicProfilePart> EveryField => new(Enum.GetValues<PublicProfilePart>());

    /// <summary>
    ///     The question asked by <c>esc</c> from any of the form's fields is answered the same way, as on compose
    ///     (#373, #430): <c>space</c> — which the field would otherwise take, typed or flipped — keeps the form and the
    ///     field takes nothing of it, and <c>y</c> throws the form away.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryField))]
    public async Task FromEveryField_TheQuestionIsAnsweredByYAndKeptByAnythingElse(PublicProfilePart part)
    {
        using var drawn = await Editing(APublicProfile.With(
            displayName: "Jeff",
            bio: "Writes Wooly.",
            fields: [new PublicProfileField("Site", "wooly.example")],
            isDiscoverable: false,
            account: Me));
        var stop = new PublicProfileStop(part);

        Type(drawn, "!");
        _ = drawn.Shell.Drafting.Edit<PublicProfileScreen>(form => form.TypeInto(stop));
        drawn.Settle();

        Assert.Equal(stop, Form(drawn).Typing);

        drawn.Press(Key.Esc);

        Assert.Equal("Discard these changes?", drawn.Shell.Asking?.Ask);

        drawn.Press(Key.Space);
        drawn.Settle();

        var form = Form(drawn);

        Assert.Null(drawn.Shell.Asking);
        Assert.Equal(stop, form.Typing);
        Assert.Equal(
            ("Jeff!", "Writes Wooly.", "Site", "wooly.example", false, false, false),
            (form.DisplayName, form.Bio, form.LabelOf(0), form.ValueOf(0), form.IsLocked, form.IsBot, form.IsDiscoverable));

        drawn.Press(Key.Esc);
        drawn.Press(Key.Y);
        drawn.Settle();

        Assert.IsType<AccountScreen>(drawn.Shell.Screen);
    }

    /// <summary>
    ///     A sideways notch over the form while a question is open is no answer, as over compose's post
    ///     (<see cref="ComposeDiscardTests.ASidewaysNotchOverTheDraft_LeavesTheQuestionOpen" />): the question stays
    ///     open, and the bio does not scroll.
    /// </summary>
    [Fact]
    public async Task ASidewaysNotchOverTheForm_LeavesTheQuestionOpen()
    {
        using var drawn = await Editing();
        var bio = drawn.Window.ProfileField<ComposeEditor>();
        var at = bio.FrameToScreen();

        Type(drawn, "!");
        drawn.Press(Key.Esc);

        drawn.WheelSideways(at.X, at.Y);
        drawn.WheelSideways(at.X, at.Y, right: false);
        drawn.Settle();

        Assert.NotNull(drawn.Shell.Asking);
        Assert.Equal("Jeff!", Form(drawn).DisplayName);
        Assert.Equal(0, bio.Viewport.X);
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
