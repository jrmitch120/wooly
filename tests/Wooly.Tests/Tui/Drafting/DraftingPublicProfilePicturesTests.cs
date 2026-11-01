using Wooly.Core.Accounts;
using Wooly.Core.Errors;
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
///     The avatar and the header on the edit public profile form (#407, ADR-0028), asked of drafting (ADR-0029, #422):
///     each row draws the current picture, <c>⏎</c> opens the file browser for one picture and what is chosen there
///     comes back to the form by the one path a compose's attachments take, <c>ctrl-v</c>/<c>alt-v</c> paste one, and a
///     picture chosen is drawn in place with its file name beside it and sent only when the form is saved.
/// </summary>
public class DraftingPublicProfilePicturesTests : IDisposable
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
    public void Enter_OnTheAvatar_OpensTheBrowserForOnePicture()
    {
        _files.WriteFile("a.png");
        _files.WriteFile("b.jpg");
        _files.WriteFile("clip.mp4");
        _files.WriteFile("notes.txt");
        var (drafting, form) = Editing();

        WalkTo(drafting, form, PublicProfilePart.Avatar);
        Assert.Contains(form.Keys, key => key is { Key: "⏎", Does: "choose file" });

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Enter));

        var browser = Assert.IsType<FileBrowserScreen>(drafting.Stack.Top);

        Assert.Equal("Choose avatar", browser.Crumb);
        Assert.Equal(["..", "a.png", "b.jpg"], Names(browser));

        browser.Answer(Verb.Choose, drafting.Reach);
        browser.Pick(2);
        browser.Answer(Verb.Choose, drafting.Reach);

        Assert.Contains(Rows(browser), row => row.Contains("☑ a.png", StringComparison.Ordinal));
        Assert.Contains(Rows(browser), row => row.Contains("☐ b.jpg", StringComparison.Ordinal));

        // Every file is not to be had here: a picture is all either row takes.
        browser.Answer(Verb.EveryFile, drafting.Reach);

        Assert.Equal(["..", "a.png", "b.jpg"], Names(browser));
        Assert.Empty(drafting.Fakes.PublicProfile.Saves);
    }

    /// <summary>
    ///     A file chosen in the browser goes back to the form as the picture the walk was on: drawn in place of the
    ///     current one with its file name beside it, sent with nothing else until <c>ctrl-s</c> — and then in the save.
    /// </summary>
    [Theory]
    [InlineData(PublicProfilePart.Avatar)]
    [InlineData(PublicProfilePart.Header)]
    public void AFileChosen_IsShownThenSentOnlyOnSave(PublicProfilePart picture)
    {
        var cat = _files.WriteFile("cat.png");
        var (drafting, form) = Editing();

        WalkTo(drafting, form, picture);
        drafting.Press(ShellKey.Enter);
        drafting.Press(ShellKey.Enter);
        drafting.Fakes.Host.Drain();

        Assert.Same(form, drafting.Stack.Top);
        Assert.Equal(picture, form.Typing.Part);
        Assert.Contains(
            AShell.Drawn(form),
            row => row.Contains($"{picture}  cat.png · sent on save", StringComparison.Ordinal));
        Assert.Empty(drafting.Fakes.PublicProfile.Saves);

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        var expected = picture == PublicProfilePart.Avatar
            ? new PublicProfileChange { Avatar = cat }
            : new PublicProfileChange { Header = cat };

        Assert.Equal([expected], drafting.Fakes.PublicProfile.Saves);
    }

    /// <summary>
    ///     The form's avatar is an avatar as the feed's is (#532): on a cell of 20 by 45 its box is five columns by
    ///     two, as near square as whole columns allow, and the picture in it fitted whole — the instance's and a file
    ///     chosen for it alike — while the header's box stays twelve columns and its picture cropped. The words beside
    ///     both still start after the header's box.
    /// </summary>
    [Fact]
    public void OnAWiderCell_TheAvatarsBoxIsNearSquareAndItsPictureFitted()
    {
        var cat = _files.WriteFile("cat.png");
        var (drafting, form) = Editing();
        var avatar = Drawn.Avatar("jeff@mastodon.social", "https://files.example/avatar.png");
        var header = Drawn.HeaderPicture("jeff@mastodon.social", "https://files.example/header.png");
        var pictures = new FakePictures().Holding(avatar.Id, 96, 96).Holding(header.Id, 1500, 500);

        var lines = WiderCellLines(form, pictures);
        var current = Assert.Single(Row(lines, "Avatar").Insets);
        var headed = Assert.Single(Row(lines, "Header").Insets);

        Assert.Equal((5, PublicProfilePictures.Rows, BoxFill.Fitted), (current.Columns, current.Rows, current.Fill));
        Assert.Equal((PublicProfilePictures.HeaderColumns, BoxFill.Cropped), (headed.Columns, headed.Fill));

        WalkTo(drafting, form, PublicProfilePart.Avatar);
        drafting.Drafting.Edit<PublicProfileScreen>(editing => editing.Pick(cat));

        var chosen = WiderCellLines(form, pictures.Holding(Drawn.OnDisk(cat).Id, 300, 100));
        var drawn = Assert.Single(Row(chosen, "Avatar").Insets);

        Assert.Equal((Drawn.OnDisk(cat).Id, 5, BoxFill.Fitted), (drawn.Drawn.Id, drawn.Columns, drawn.Fill));
        Assert.Equal(
            Row(lines, "Avatar").Text.IndexOf("change…", StringComparison.Ordinal),
            Row(chosen, "Avatar").Text.IndexOf("cat.png", StringComparison.Ordinal));
        Assert.Equal(
            Row(lines, "Header").Text.IndexOf("change…", StringComparison.Ordinal),
            Row(chosen, "Avatar").Text.IndexOf("cat.png", StringComparison.Ordinal));
    }

    private static IReadOnlyList<Line> WiderCellLines(PublicProfileScreen form, FakePictures pictures) =>
        form.Lines(new Drawing(61, AShell.Now, pictures, ARaster.Sixel(new CellSize(20, 45)), Height: 24));

    /// <summary>
    ///     <c>ctrl-v</c> or <c>alt-v</c> on a picture's row with a picture on the clipboard writes it to a file of its own
    ///     and takes it as that picture — sent on <c>ctrl-s</c>, not before.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.CtrlV)]
    [InlineData(ShellKey.AltV)]
    public void APictureOnTheClipboard_IsPastedIntoTheRow(ShellKey paste)
    {
        var (drafting, form) = Editing();

        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);
        WalkTo(drafting, form, PublicProfilePart.Header);
        Assert.Contains(form.Keys, key => key is { Key: "ctrl-v/alt-v", Does: "paste" });

        Assert.Equal(Drafted.Used, drafting.Press(paste));

        Assert.Equal("pasted-1.png", Path.GetFileName(form.Header));
        Assert.Equal(Png, File.ReadAllBytes(form.Header!));
        Assert.Null(form.Avatar);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("Header  pasted-1.png", StringComparison.Ordinal));
        Assert.Empty(drafting.Fakes.PublicProfile.Saves);

        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        Assert.Equal([new PublicProfileChange { Header = form.Header }], drafting.Fakes.PublicProfile.Saves);
    }

    /// <summary>
    ///     Files copied in a file manager give the row the first of them that is a picture; with none, the status row
    ///     says so and nothing is taken.
    /// </summary>
    [Fact]
    public void CopiedFiles_GiveTheRowTheFirstPicture()
    {
        var (drafting, form) = Editing();
        var notes = _files.WriteFile("notes.txt");
        var cat = _files.WriteFile("cat.png");

        WalkTo(drafting, form, PublicProfilePart.Avatar);

        drafting.Fakes.Clipboard.Holding = new Clipped.Files([notes]);
        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        Assert.Null(form.Avatar);
        Assert.Equal("None of the copied files is a picture.", drafting.Notice);
        Assert.True(drafting.NoticeIsError);

        drafting.Fakes.Clipboard.Holding = new Clipped.Files([notes, cat, _files.WriteFile("dog.png")]);
        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        Assert.Equal(cat, form.Avatar);
        Assert.Null(drafting.Notice);
    }

    /// <summary>
    ///     Off the pictures, <c>ctrl-v</c> is no picture paste — the clipboard is not even read — and is left to the field
    ///     the typing is in.
    /// </summary>
    [Fact]
    public void CtrlV_OffThePictures_ReadsNoClipboard()
    {
        var (drafting, form) = Editing();

        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.Equal(Drafted.NotMine, drafting.Press(ShellKey.CtrlV));
        Assert.Equal(Drafted.Declined, drafting.Drafting.Do(Verb.PasteFromTheClipboard));
        Assert.Equal(0, drafting.Fakes.Clipboard.Reads);
        Assert.Null(form.Avatar);
        Assert.Null(form.Header);
    }

    /// <summary>A save the instance refuses keeps the form and the picture chosen, to be fixed and saved again.</summary>
    [Fact]
    public void ARefusedSave_KeepsTheFormAndThePick()
    {
        var cat = _files.WriteFile("cat.png");
        var (drafting, form) = Editing(FakePublicProfileEditor.Refusing(
            PublicProfileRefusedException.Saving("Avatar file size must be less than 2 MB"),
            APublicProfile.With(account: Me)));

        WalkTo(drafting, form, PublicProfilePart.Avatar);
        drafting.Press(ShellKey.Enter);
        drafting.Press(ShellKey.Enter);
        drafting.Press(ShellKey.CtrlS);
        drafting.Fakes.Host.Drain();

        Assert.Same(form, drafting.Stack.Top);
        Assert.Equal(cat, form.Avatar);
        Assert.Contains(AShell.Drawn(form), row => row.Contains("Avatar  cat.png", StringComparison.Ordinal));
        Assert.Equal(
            "The instance would not take that change to your public profile: Avatar file size must be less than 2 MB",
            drafting.Notice);
    }

    /// <summary>
    ///     On a terminal that draws, each row draws the current picture through the picture path — the avatar
    ///     <b>You</b> sent for, and the header — holding its box with the Stand-in's shade until the pixels are here.
    /// </summary>
    [Fact]
    public void OnATerminalThatDraws_EachRowDrawsTheCurrentPicture()
    {
        var (_, form) = Editing();
        var avatar = Drawn.Avatar("jeff@mastodon.social", "https://files.example/avatar.png");
        var header = Drawn.HeaderPicture("jeff@mastodon.social", "https://files.example/header.png");

        var coming = Lines(form, new FakePictures());

        Assert.Equal(avatar, Row(coming, "Avatar").Wants);
        Assert.Equal(header, Row(coming, "Header").Wants);
        Assert.Contains(Row(coming, "Avatar").Spans, span => span is { Text: "░░░░", Role: Role.StandIn });
        Assert.Empty(Row(coming, "Avatar").Insets);

        var here = Lines(form, new FakePictures().Holding(avatar.Id, 96, 96).Holding(header.Id, 1500, 500));

        Assert.Equal(avatar, Assert.Single(Row(here, "Avatar").Insets).Drawn);
        Assert.Equal(header, Assert.Single(Row(here, "Header").Insets).Drawn);
        Assert.Contains("change…", Row(here, "Avatar").Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A header narrower than its box fills the box, cropped to it from the middle as an instance crops one, rather
    ///     than standing in it with slack beside it (#438): under the avatar, starting in its column, and as wide and
    ///     tall as the box.
    /// </summary>
    [Fact]
    public void OnATerminalThatDraws_ANarrowHeaderFillsItsBoxUnderTheAvatar()
    {
        var (_, form) = Editing();
        var avatar = Drawn.Avatar("jeff@mastodon.social", "https://files.example/avatar.png");
        var header = Drawn.HeaderPicture("jeff@mastodon.social", "https://files.example/header.png");

        var here = Lines(form, new FakePictures().Holding(avatar.Id, 96, 96).Holding(header.Id, 500, 500));
        var drawn = Assert.Single(here, line => line.Insets.Any(inset => inset.Drawn == header)).Insets[0];

        Assert.Equal(Assert.Single(Row(here, "Avatar").Insets).Column, drawn.Column);
        Assert.Equal(
            (PublicProfilePictures.HeaderColumns, PublicProfilePictures.Rows),
            (drawn.Columns, drawn.Rows));
    }

    /// <summary>
    ///     On a terminal that draws, a blank row parts the avatar's box from the header's, so the two pictures do not
    ///     run into each other.
    /// </summary>
    [Fact]
    public void OnATerminalThatDraws_ABlankRowPartsTheAvatarFromTheHeader()
    {
        var (_, form) = Editing();

        var lines = Lines(form, new FakePictures());
        var header = lines.ToList().IndexOf(Row(lines, "Header"));

        Assert.Equal(string.Empty, lines[header - 1].Text.Trim());
        Assert.NotEqual(string.Empty, lines[header - 2].Text.Trim());
    }

    /// <summary>
    ///     A file chosen is drawn in place of the current picture, read off the disk as the browser's preview is, with its
    ///     name beside the box where the word "change…" was — the rows no taller, and the name where the word stood.
    /// </summary>
    [Fact]
    public void OnATerminalThatDraws_AFileChosenIsDrawnInPlace()
    {
        var cat = _files.WriteFile("cat.png");
        var (drafting, form) = Editing();
        var before = Lines(form, new FakePictures());

        WalkTo(drafting, form, PublicProfilePart.Avatar);
        drafting.Drafting.Edit<PublicProfileScreen>(editing => editing.Pick(cat));

        var after = Lines(form, new FakePictures().Holding(Drawn.OnDisk(cat).Id, 40, 40));
        var row = Row(after, "Avatar");

        Assert.Equal(before.Count, after.Count);
        // Fitted, as the instance's avatar is (#532).
        var fitted = Drawn.OnDisk(cat) with { Fill = BoxFill.Fitted };

        Assert.Equal(fitted, row.Wants);
        Assert.Equal(fitted, Assert.Single(row.Insets).Drawn);
        Assert.Equal(
            Row(before, "Avatar").Text.IndexOf("change…", StringComparison.Ordinal),
            row.Text.IndexOf("cat.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A terminal that draws no picture is asked for none: each row is one line of words, the label and what it holds,
    ///     at 61 columns.
    /// </summary>
    [Fact]
    public void OnATerminalThatCannotDraw_EachRowIsItsWords()
    {
        var cat = _files.WriteFile("cat.png");
        var (drafting, form) = Editing(
            FakePublicProfileEditor.Holding(APublicProfile.With(account: Me) with { HeaderUrl = null }));

        WalkTo(drafting, form, PublicProfilePart.Avatar);
        drafting.Drafting.Edit<PublicProfileScreen>(editing => editing.Pick(cat));

        var lines = form.Lines(new Drawing(61, AShell.Now, new FakePictures(), Raster.None));

        Assert.All(lines, line => Assert.Null(line.Wants));
        Assert.Contains(lines, line => line.Text.EndsWith(" Avatar  cat.png · sent on save", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text == "  Header  none · choose…");
    }

    /// <summary>
    ///     A terminal with a cell to draw at but nothing supplying pictures draws none either, by the one test every
    ///     other picture is drawn by (<see cref="Drawing.Draws" />, #438): no box and no shade, which would otherwise
    ///     stand for good waiting on pixels that nothing will send for — the rows laid out as a terminal that cannot
    ///     draw lays them out.
    /// </summary>
    [Fact]
    public void WhereNothingSuppliesPictures_EachRowIsItsWordsWithNoShade()
    {
        var (_, form) = Editing();

        var lines = form.Lines(new Drawing(61, AShell.Now, Pictures: null, ARaster.Sixel(), Height: 24));
        var undrawn = form.Lines(new Drawing(61, AShell.Now, new FakePictures(), Raster.None, Height: 24));

        Assert.DoesNotContain(lines, line => line.Has(Role.StandIn));
        Assert.All(lines, line => Assert.Empty(line.Insets));
        Assert.All(lines, line => Assert.Null(line.Wants));
        Assert.Equal(undrawn.Select(line => line.Text), lines.Select(line => line.Text));
    }

    /// <summary>
    ///     The form, opened with <c>E</c> on <b>You</b> and read — drafting launched from the test's folder and pasting
    ///     into it.
    /// </summary>
    private (ADrafting Drafting, PublicProfileScreen Form) Editing(FakePublicProfileEditor? publicProfile = null)
    {
        var drafting = new ADrafting
        {
            Bottom = new AccountScreen(Me, [], pinned: null, isOwnAccount: true),
            Fakes =
            {
                Accounts = FakeAccountRelationships.Holding(Me),
                PublicProfile = publicProfile ?? FakePublicProfileEditor.Holding(APublicProfile.With(account: Me)),
                LaunchedFrom = _files.Path,
                Pasted = new PastedPictures(_files.Path),
            },
        }.Build();

        drafting.Press(ShellKey.CapitalE);
        drafting.Fakes.Host.Drain();

        return (drafting, Assert.IsType<PublicProfileScreen>(drafting.Stack.Top));
    }

    private static void WalkTo(ADrafting drafting, PublicProfileScreen form, PublicProfilePart part)
    {
        while (form.Typing.Part != part)
        {
            Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Down));
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
