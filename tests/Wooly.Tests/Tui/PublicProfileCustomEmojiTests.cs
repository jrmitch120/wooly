using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Typing <c>:</c> and two letters at the start of a word in the edit public profile form's bio hangs the instance's
///     custom emoji under it (#578), the list compose's post has — driven through the shell's window with the keys a
///     reader would use, and what is drawn read back off the terminal.
/// </summary>
public class PublicProfileCustomEmojiTests
{
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>
    ///     A <c>:</c>-word in the bio opens the list on the row under it, lined up on its <c>:</c>, naming what matches;
    ///     <c>tab</c> writes the picked one's shortcode and a space in place of the word, and closes it.
    /// </summary>
    [Fact]
    public async Task AColonWord_OpensTheList_AndTabWritesThePick()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi :bl");

        var rows = drawn.Rows();

        Assert.True(List(drawn).Visible);
        Assert.Equal(Bio(drawn).FrameToScreen().Y + 1, List(drawn).FrameToScreen().Y);
        Assert.Equal(Bio(drawn).FrameToScreen().X + 3, List(drawn).FrameToScreen().X);
        Assert.Contains(rows, row => row.Contains(":blobcat:", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains(":thinking:", StringComparison.Ordinal));

        drawn.Press(Key.Tab);

        Assert.Equal("hi :blobcat: ", Form(drawn).Bio);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     On a terminal drawing Kitty's placeholders, the bio's list draws each emoji's picture before its shortcode, as
    ///     the post's does (#580): two placeholder cells after the mark, naming the image sent at a custom emoji's size.
    /// </summary>
    [Fact]
    public async Task OnKittyPlaceholders_ARowDrawsItsEmojisPictureBeforeItsShortcode()
    {
        var terminal = new FakeTerminalImages();

        using var drawn = await InTheBio(
            new FakePictures().HoldingCustomEmoji(FakeInstanceEmoji.Emoji("blobcat")),
            terminal);

        Type(drawn, "hi :bl");

        var list = List(drawn).FrameToScreen();
        var sent = Assert.Single(terminal.Transmitted, image => (image.Columns, image.Rows) == (2, 1));
        var cells = CustomEmojiTests.Placeholders(drawn).Where(cell => list.Contains(cell.Column, cell.Row)).ToList();

        Assert.Equal([(list.Y + 1, list.X + 3), (list.Y + 1, list.X + 4)], cells.Select(cell => (cell.Row, cell.Column)));
        Assert.All(cells, cell => Assert.Equal(sent.Id, cell.Id));
        Assert.Equal(" :blobcat:", CustomEmojiTests.Text(drawn, list.Y + 1, list.X + 5, 10));
    }

    /// <summary>
    ///     The form opened with <c>E</c> on <b>You</b>, and the typing walked down into the bio at its foot — on a
    ///     terminal drawing Kitty's placeholders from <paramref name="pictures" /> where they are said.
    /// </summary>
    private static async Task<DrawnShell> InTheBio(FakePictures? pictures = null, FakeTerminalImages? terminal = null)
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: "", account: Me)),
            Emoji = FakeInstanceEmoji.Offering("blobcat", "thinking"),
        };

        var drawn = pictures is null
            ? await DrawnShell.Of(80, 30, Themes.Plain, built)
            : await DrawnShell.Of(
                80,
                30,
                Themes.Plain,
                built,
                pictures: pictures,
                kittyImages: terminal,
                drawsPlaceholders: true,
                raster: ARaster.Placeholders());

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

    /// <summary>The list hung under the word being typed, among the form's own views.</summary>
    private static PaintedView List(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<PublicProfileView>().Single()
             .SubViews.OfType<PaintedView>().Single(view => view.Id == HangingList.Id);

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }
}
