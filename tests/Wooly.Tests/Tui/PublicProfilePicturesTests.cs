using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Drafting;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The avatar and the header on the edit public profile form (#407, ADR-0028) where they meet the shell and the
///     window: leaving a form with a picture chosen, <c>esc</c> in the browser, what a save reads again, and the keys and
///     clicks through the window. What the form does with its pictures itself is drafting's, and tested there
///     (<see cref="DraftingPublicProfilePicturesTests" />, #422).
/// </summary>
public class PublicProfilePicturesTests : IDisposable
{
    /// <summary>A small PNG's worth of bytes — what a screenshot on the clipboard reads as.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>A picture chosen is a touch: leaving asks first, as any other edit does (#373).</summary>
    [Fact]
    public async Task AFileChosen_MakesLeavingAsk()
    {
        _files.WriteFile("cat.png");
        var (shell, _, form) = await Editing();

        WalkTo(shell, form, PublicProfilePart.Header);
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.Enter);

        shell.Press(ShellKey.Escape);

        Assert.Equal("Discard these changes?", shell.Asking?.Ask);
    }

    /// <summary><c>esc</c> in the browser goes back to the form with nothing chosen and nothing touched.</summary>
    [Fact]
    public async Task Esc_InTheBrowser_ChoosesNothing()
    {
        _files.WriteFile("cat.png");
        var (shell, _, form) = await Editing();

        WalkTo(shell, form, PublicProfilePart.Avatar);
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.Escape);

        Assert.Same(form, shell.Screen);
        Assert.Null(form.Avatar);
        Assert.False(form.Touched);
    }

    /// <summary>
    ///     A new avatar saved is the one <b>You</b> draws once it reads again — never the old picture still held from
    ///     before the save, which is the same account's under the same handle (review of #403).
    /// </summary>
    [Fact]
    public async Task ASavedAvatar_IsDrawnOnYouInPlaceOfTheOneHeldBefore()
    {
        var cat = _files.WriteFile("cat.png");
        var renewed = Me with { AvatarUrl = "https://files.example/cat.png" };
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(account: Me), saved: renewed);
        built.LaunchedFrom = _files.Path;
        var shell = await built.Opened();

        shell.Rail.GoTo(DestinationKind.Profile);
        built.Host.Settle();

        var held = Wanted(Assert.IsType<AccountScreen>(shell.Screen), new FakePictures(), Me.AvatarUrl!);

        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        var form = Assert.IsType<PublicProfileScreen>(shell.Screen);

        WalkTo(shell, form, PublicProfilePart.Avatar);
        shell.Drafting.Edit<PublicProfileScreen>(editing => editing.Pick(cat));
        built.Accounts.NowShowing(renewed);
        shell.Press(ShellKey.CtrlS);
        built.Host.Settle();

        var you = Assert.IsType<AccountScreen>(shell.Screen);
        var pictures = new FakePictures().Holding(held.Id, 96, 96);
        var drawn = Wanted(you, pictures, renewed.AvatarUrl!);

        Assert.NotEqual(held.Id, drawn.Id);
        Assert.DoesNotContain(
            you.Lines(new Drawing(61, AShell.Now, pictures, ARaster.Sixel(), Height: 24)).SelectMany(line => line.Insets),
            inset => inset.Drawn == held);
    }

    /// <summary>
    ///     A header the instance has changed is the one the form draws when it is opened again, not the one held from
    ///     the last time it was open (review of #403).
    /// </summary>
    [Fact]
    public async Task AChangedHeader_IsDrawnWhenTheFormIsOpenedAgain()
    {
        var built = Built();
        var (shell, _, form) = await Editing(built);
        var held = Row(Lines(form, new FakePictures()), "Header").Wants!;

        shell.Press(ShellKey.Escape);
        built.PublicProfile.NowReading(APublicProfile.With(account: Me) with { HeaderUrl = "https://files.example/hills.png" });
        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        var reopened = Assert.IsType<PublicProfileScreen>(shell.Screen);
        var lines = Lines(reopened, new FakePictures().Holding(held.Id, 1500, 500));

        Assert.Equal("https://files.example/hills.png", Row(lines, "Header").Wants?.Address);
        Assert.Empty(Row(lines, "Header").Insets);
    }

    /// <summary>
    ///     In the window, with no colour, the avatar and the header read as words; <c>↓</c> walks onto them, <c>⏎</c> on
    ///     one opens the browser, and choosing a file there puts its name on the row.
    /// </summary>
    [Fact]
    public async Task InTheWindow_EnterOnAPictureOpensTheBrowserAndAFileComesBack()
    {
        _files.WriteFile("cat.png");
        using var drawn = await DrawnEditing();

        Assert.Contains(drawn.Rows(), row => row.Contains("  Avatar  change… ", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("  Header  change… ", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);

        var form = Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

        Assert.Equal(PublicProfilePart.Avatar, form.Typing.Part);

        drawn.Press(Key.Enter);

        Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen);

        drawn.Press(Key.Enter);
        drawn.Redraw();

        Assert.Same(form, drawn.Shell.Screen);
        Assert.Contains(drawn.Rows(), row => row.Contains(" Avatar  cat.png · sent on save", StringComparison.Ordinal));
    }

    /// <summary>A click on the header's row opens the browser for it, as <c>⏎</c> on it does.</summary>
    [Fact]
    public async Task InTheWindow_AClickOnAPictureOpensTheBrowser()
    {
        using var drawn = await DrawnEditing();
        var rows = drawn.Rows();
        var row = Array.FindIndex(rows, text => text.Contains("Header  change…", StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        drawn.Click(rows[row].IndexOf("change…", StringComparison.Ordinal), row);

        Assert.Equal("Choose header", Assert.IsType<FileBrowserScreen>(drawn.Shell.Screen).Crumb);
    }

    /// <summary>In the window, <c>ctrl-v</c> and <c>alt-v</c> on a picture's row paste a picture on the clipboard into it.</summary>
    [Fact]
    public async Task InTheWindow_CtrlVAndAltVPasteAPicture()
    {
        using var drawn = await DrawnEditing();
        var form = Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

        drawn.Built.Clipboard.Holding = new Clipped.Picture(Png);

        drawn.Press(Key.CursorDown);
        drawn.Press(Key.V.WithCtrl);
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.V.WithAlt);

        Assert.Equal("pasted-1.png", Path.GetFileName(form.Avatar));
        Assert.Equal("pasted-2.png", Path.GetFileName(form.Header));
    }

    /// <summary>The shell on an 80 × 30 terminal with no colour, the form opened with <c>E</c> on <b>You</b>.</summary>
    private async Task<DrawnShell> DrawnEditing()
    {
        var built = Built();

        built.LaunchedFrom = _files.Path;
        built.Pasted = new PastedPictures(_files.Path);

        var drawn = await DrawnShell.Of(80, 30, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        return drawn;
    }

    private static AShell Built(PublicProfile? read = null) => new()
    {
        Accounts = FakeAccountRelationships.Holding(Me),
        PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(account: Me)),
    };

    /// <summary>The form, opened with <c>E</c> on <b>You</b>, launched from the test's folder and pasting into it.</summary>
    private async Task<(Shell Shell, AShell Built, PublicProfileScreen Form)> Editing(AShell? built = null)
    {
        built ??= Built();
        built.LaunchedFrom = _files.Path;
        built.Pasted = new PastedPictures(_files.Path);

        var shell = await built.Opened();

        shell.Rail.GoTo(DestinationKind.Profile);
        built.Host.Settle();
        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        return (shell, built, Assert.IsType<PublicProfileScreen>(shell.Screen));
    }

    private static void WalkTo(Shell shell, PublicProfileScreen form, PublicProfilePart part)
    {
        while (form.Typing.Part != part)
        {
            Assert.True(shell.Press(ShellKey.Down));
        }
    }

    /// <summary>The form laid out at 61 columns on a sixel terminal, with <paramref name="pictures" />' pixels here.</summary>
    private static IReadOnlyList<Line> Lines(PublicProfileScreen form, FakePictures pictures) =>
        form.Lines(new Drawing(61, AShell.Now, pictures, ARaster.Sixel(), Height: 24));

    /// <summary>The picture <paramref name="you" /> wants for the avatar at <paramref name="address" />, on a sixel terminal.</summary>
    private static Drawn Wanted(AccountScreen you, FakePictures pictures, string address) =>
        you.Lines(new Drawing(61, AShell.Now, pictures, ARaster.Sixel(), Height: 24))
           .Select(line => line.Wants)
           .First(wants => wants?.Address == address)!;

    /// <summary>The first row of the picture <paramref name="label" /> names.</summary>
    private static Line Row(IReadOnlyList<Line> lines, string label) =>
        lines.First(line => line.Text.Contains($" {label}  ", StringComparison.Ordinal));
}
