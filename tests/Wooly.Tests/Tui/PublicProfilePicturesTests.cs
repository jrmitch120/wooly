using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The avatar and the header on the edit public profile form (#407, ADR-0028): each row draws the current picture,
///     <c>⏎</c> opens the file browser for one picture, <c>ctrl-v</c>/<c>alt-v</c> paste one, and a picture chosen is
///     drawn in place with its file name beside it and sent only when the form is saved.
/// </summary>
public class PublicProfilePicturesTests : IDisposable
{
    /// <summary>A small PNG's worth of bytes — what a screenshot on the clipboard reads as.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>⏎</c> on the avatar opens the file browser over the form, crumbed for what it chooses, listing only the
    ///     pictures in the folder — and only one of them may be chosen.
    /// </summary>
    [Fact]
    public async Task Enter_OnTheAvatar_OpensTheBrowserForOnePicture()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.jpg");
        _files.WriteFile("clip.mp4");
        _files.WriteFile("notes.txt");
        var (shell, built, form) = await Editing();

        WalkTo(shell, form, PublicProfilePart.Avatar);
        Assert.Contains(shell.Keys, key => key is { Key: "⏎", Does: "choose file" });

        shell.Press(ShellKey.Enter);

        var browser = Assert.IsType<FileBrowserScreen>(shell.Screen);

        Assert.Equal([form.Crumb, "Choose avatar"], shell.Crumbs.TakeLast(2));
        Assert.Equal(["..", "a.png", "b.jpg"], Names(browser));

        shell.Press(ShellKey.Space);
        browser.Pick(2);
        shell.Press(ShellKey.Space);

        Assert.Contains(Rows(browser), row => row.Contains("☑ a.png", StringComparison.Ordinal));
        Assert.Contains(Rows(browser), row => row.Contains("☐ b.jpg", StringComparison.Ordinal));

        // Every file is not to be had here: a picture is all either row takes.
        shell.Press(ShellKey.CtrlA);

        Assert.Equal(["..", "a.png", "b.jpg"], Names(browser));
        Assert.Empty(built.PublicProfile.Saves);
    }

    /// <summary>
    ///     A file chosen in the browser goes back to the form as the picture the walk was on: drawn in place of the
    ///     current one with its file name beside it, sent with nothing else until <c>ctrl-s</c> — and then in the save.
    /// </summary>
    [Theory]
    [InlineData(PublicProfilePart.Avatar)]
    [InlineData(PublicProfilePart.Header)]
    public async Task AFileChosen_IsShownThenSentOnlyOnSave(PublicProfilePart picture)
    {
        var cat = _files.WriteFile("cat.png");
        var (shell, built, form) = await Editing();

        WalkTo(shell, form, picture);
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Same(form, shell.Screen);
        Assert.Equal(picture, form.Typing.Part);
        Assert.Contains(
            AShell.Drawn(form),
            row => row.Contains($"{picture}  cat.png · sent on save", StringComparison.Ordinal));
        Assert.Empty(built.PublicProfile.Saves);

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        var expected = picture == PublicProfilePart.Avatar
            ? new PublicProfileChange { Avatar = cat }
            : new PublicProfileChange { Header = cat };

        Assert.Equal([expected], built.PublicProfile.Saves);
    }

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
    ///     <c>ctrl-v</c> or <c>alt-v</c> on a picture's row with a picture on the clipboard writes it to a file of its own
    ///     and takes it as that picture — sent on <c>ctrl-s</c>, not before.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.CtrlV)]
    [InlineData(ShellKey.AltV)]
    public async Task APictureOnTheClipboard_IsPastedIntoTheRow(ShellKey paste)
    {
        var (shell, built, form) = await Editing();

        built.Clipboard.Holding = new Clipped.Picture(Png);
        WalkTo(shell, form, PublicProfilePart.Header);
        Assert.Contains(shell.Keys, key => key is { Key: "ctrl-v/alt-v", Does: "paste" });

        Assert.True(shell.Press(paste));

        Assert.Equal("pasted-1.png", Path.GetFileName(form.Header));
        Assert.Equal(Png, File.ReadAllBytes(form.Header!));
        Assert.Null(form.Avatar);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("Header  pasted-1.png", StringComparison.Ordinal));
        Assert.Empty(built.PublicProfile.Saves);

        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        Assert.Equal([new PublicProfileChange { Header = form.Header }], built.PublicProfile.Saves);
    }

    /// <summary>
    ///     Files copied in a file manager give the row the first of them that is a picture; with none, the status row
    ///     says so and nothing is taken.
    /// </summary>
    [Fact]
    public async Task CopiedFiles_GiveTheRowTheFirstPicture()
    {
        var (shell, built, form) = await Editing();
        var notes = _files.WriteFile("notes.txt");
        var cat = _files.WriteFile("cat.png");

        WalkTo(shell, form, PublicProfilePart.Avatar);

        built.Clipboard.Holding = new Clipped.Files([notes]);
        Assert.True(shell.Press(ShellKey.CtrlV));

        Assert.Null(form.Avatar);
        Assert.Equal("None of the copied files is a picture.", shell.Notice);
        Assert.True(shell.NoticeIsError);

        built.Clipboard.Holding = new Clipped.Files([notes, cat, _files.WriteFile("dog.png")]);
        Assert.True(shell.Press(ShellKey.CtrlV));

        Assert.Equal(cat, form.Avatar);
        Assert.Null(shell.Notice);
    }

    /// <summary>
    ///     Off the pictures, <c>ctrl-v</c> is no picture paste — the clipboard is not even read — and is left to the field
    ///     the typing is in.
    /// </summary>
    [Fact]
    public async Task CtrlV_OffThePictures_ReadsNoClipboard()
    {
        var (shell, built, form) = await Editing();

        built.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.False(shell.Press(ShellKey.CtrlV));
        Assert.False(shell.PasteFromTheClipboard());
        Assert.Equal(0, built.Clipboard.Reads);
        Assert.Null(form.Avatar);
        Assert.Null(form.Header);
    }

    /// <summary>A save the instance refuses keeps the form and the picture chosen, to be fixed and saved again.</summary>
    [Fact]
    public async Task ARefusedSave_KeepsTheFormAndThePick()
    {
        var cat = _files.WriteFile("cat.png");
        var built = Built();
        built.PublicProfile = FakePublicProfileEditor.Refusing(
            PublicProfileRefusedException.Saving("Avatar file size must be less than 2 MB"),
            APublicProfile.With(account: Me));
        var (shell, _, form) = await Editing(built);

        WalkTo(shell, form, PublicProfilePart.Avatar);
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.Enter);
        shell.Press(ShellKey.CtrlS);
        built.Host.Drain();

        Assert.Same(form, shell.Screen);
        Assert.Equal(cat, form.Avatar);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("Avatar  cat.png", StringComparison.Ordinal));
        Assert.Equal(
            "The instance would not take that change to your public profile: Avatar file size must be less than 2 MB",
            shell.Notice);
    }

    /// <summary>
    ///     On a terminal that draws, each row draws the current picture through the picture path — the avatar
    ///     <b>You</b> sent for, and the header — holding its box with the Stand-in's shade until the pixels are here.
    /// </summary>
    [Fact]
    public async Task OnATerminalThatDraws_EachRowDrawsTheCurrentPicture()
    {
        var (_, _, form) = await Editing();
        var avatar = Drawn.Avatar("jeff@mastodon.social", "https://files.example/avatar.png");
        var header = Drawn.ProfileHeader("jeff@mastodon.social", "https://files.example/header.png");

        var coming = Lines(form, new FakePictures());

        Assert.Equal(avatar, Row(coming, "Avatar").Wants);
        Assert.Equal(header, Row(coming, "Header").Wants);
        Assert.Contains(Row(coming, "Avatar").Spans, span => span is { Text: "░░░░", Role: Role.StandIn });
        Assert.Empty(Row(coming, "Avatar").Insets);

        var here = Lines(form, new FakePictures().Holding(avatar.Id, 96, 96).Holding(header.Id, 1500, 500));

        Assert.Equal(avatar, Assert.Single(Row(here, "Avatar").Insets).Drawn);
        Assert.Equal(header, Assert.Single(Row(here, "Header").Insets).Drawn);
        Assert.Contains("current", Row(here, "Avatar").Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A file chosen is drawn in place of the current picture, read off the disk as the browser's preview is, with its
    ///     name beside the box where the word "current" was — the rows no taller, and the name where the word stood.
    /// </summary>
    [Fact]
    public async Task OnATerminalThatDraws_AFileChosenIsDrawnInPlace()
    {
        var cat = _files.WriteFile("cat.png");
        var (shell, _, form) = await Editing();
        var before = Lines(form, new FakePictures());

        WalkTo(shell, form, PublicProfilePart.Avatar);
        shell.ChangePublicProfile(editing => editing.Pick(cat));

        var after = Lines(form, new FakePictures().Holding(Drawn.OnDisk(cat).Id, 40, 40));
        var row = Row(after, "Avatar");

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(Drawn.OnDisk(cat), row.Wants);
        Assert.Equal(Drawn.OnDisk(cat), Assert.Single(row.Insets).Drawn);
        Assert.Equal(
            Row(before, "Avatar").Text.IndexOf("current", StringComparison.Ordinal),
            row.Text.IndexOf("cat.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A terminal that draws no picture is asked for none: each row is one line of words, the label and what it holds,
    ///     at 61 columns.
    /// </summary>
    [Fact]
    public async Task OnATerminalThatCannotDraw_EachRowIsItsWords()
    {
        var cat = _files.WriteFile("cat.png");
        var (shell, _, form) = await Editing(Built(APublicProfile.With(account: Me) with { HeaderUrl = null }));

        WalkTo(shell, form, PublicProfilePart.Avatar);
        shell.ChangePublicProfile(editing => editing.Pick(cat));

        var lines = form.Lines(new Drawing(61, AShell.Now, new FakePictures(), Raster.None));

        Assert.All(lines, line => Assert.Null(line.Wants));
        Assert.Contains(lines, line => line.Text.EndsWith(" Avatar  cat.png · sent on save", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text == "  Header  none");
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

        Assert.Contains(drawn.Rows(), row => row.Contains("  Avatar  current ", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("  Header  current ", StringComparison.Ordinal));

        drawn.Press(Key.CursorDown);
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
        var row = Array.FindIndex(rows, text => text.Contains("Header  current", StringComparison.Ordinal));

        Assert.True(row >= 0, string.Join('\n', rows));

        drawn.Click(rows[row].IndexOf("current", StringComparison.Ordinal), row);

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

    /// <summary>The first row of the picture <paramref name="label" /> names.</summary>
    private static Line Row(IReadOnlyList<Line> lines, string label) =>
        lines.First(line => line.Text.Contains($" {label}  ", StringComparison.Ordinal));

    private static IReadOnlyList<string> Rows(FileBrowserScreen browser) =>
        [.. browser.Lines(new Drawing(78, AShell.Now)).Select(line => line.Text)];

    /// <summary>The names the browser lists, in order.</summary>
    private static IReadOnlyList<string> Names(FileBrowserScreen browser) =>
    [
        .. Rows(browser).Skip(3)
                        .Where(row => row.Length > 4 && row[2] is '◂' or '▸' or '☐' or '☑')
                        .Select(row => row[4..].Split("  ")[0].Trim()),
    ];
}
