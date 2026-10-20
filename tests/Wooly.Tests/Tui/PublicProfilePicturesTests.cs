using Wooly.Core.Accounts;
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
///     and a picture chosen is drawn in place with its file name beside it.
/// </summary>
public class PublicProfilePicturesTests : IDisposable
{
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

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
}
