using System.Drawing;
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
    ///     On a terminal drawing through a box, sixel or Kitty, the bio's list draws each emoji's picture before its
    ///     shortcode too (#581): in the two columns after the mark, at a custom emoji's size — and once it closes, none is
    ///     left on the terminal.
    /// </summary>
    [Theory]
    [InlineData(PictureWay.Sixel)]
    [InlineData(PictureWay.Kitty)]
    public async Task ThroughABox_ARowDrawsItsEmojisPictureBeforeItsShortcode(PictureWay way)
    {
        using var drawn = await InTheBio(new FakePictures().HoldingCustomEmoji(FakeInstanceEmoji.Emoji("blobcat")), way: way);
        using var shown = ShownPictures.Over(drawn, way);

        Assert.Empty(shown.Showing());

        foreach (var letter in "hi :bl")
        {
            drawn.PressAsTheLoopDraws(new Key(letter));
        }

        var list = List(drawn).FrameToScreen();

        Assert.Equal(
            [new Point(list.X + 3, list.Y + 1), new Point(list.X + 4, list.Y + 1)],
            shown.Showing().Select(cell => cell.Cell).OrderBy(cell => cell.X));
        Assert.Equal(" :blobcat:", CustomEmojiTests.Text(drawn, list.Y + 1, list.X + 5, 10));

        drawn.PressAsTheLoopDraws(Key.Esc);

        Assert.False(List(drawn).Visible);
        Assert.Empty(shown.Showing());
    }

    /// <summary>
    ///     While the read is in flight, a <c>:</c>-word in the bio opens the list on its loading row, hung and lined up
    ///     as the list is, with its usual foot and nothing on the status row; the answer landing fills it in at once,
    ///     with no keystroke, picked on the first, growing down from where the loading row hung — and <c>tab</c> then
    ///     writes the pick, as ever.
    /// </summary>
    [Fact]
    public async Task WhileTheReadIsInFlightTheBioOpensTheListOnALoadingRow_AndItsAnswerLandingFillsItIn()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat", "blobfox", "thinking");
        emoji.Holding = true;

        using var drawn = await InTheBio(emoji: emoji);
        var bio = Bio(drawn).FrameToScreen();

        Type(drawn, "hi :bl");

        var loading = List(drawn).FrameToScreen();
        var rows = RowsOf(drawn, loading);

        Assert.True(List(drawn).Visible);
        Assert.Equal(bio.Y + 1, loading.Y);
        Assert.Equal(bio.X + 3, loading.X);
        Assert.Equal(3, rows.Count);
        Assert.Matches("^│  reading custom emoji… +│$", rows[1]);
        Assert.Contains("↑↓ pick  tab insert  esc close", rows[^1], StringComparison.Ordinal);
        Assert.Null(drawn.Shell.StatusRow.Notice);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        var filled = List(drawn).FrameToScreen();
        rows = RowsOf(drawn, filled);

        Assert.True(List(drawn).Visible);
        Assert.Equal(loading.Y, filled.Y);
        Assert.Equal(loading.X, filled.X);
        Assert.Equal(4, rows.Count);
        Assert.StartsWith("│▌ :blobcat:", rows[1], StringComparison.Ordinal);
        Assert.StartsWith("│  :blobfox:", rows[2], StringComparison.Ordinal);
        Assert.Single(emoji.Reads);

        drawn.Press(Key.Tab);

        Assert.Equal("hi :blobcat: ", Form(drawn).Bio);
    }

    /// <summary>
    ///     A read that answers nothing, or an instance offering none, just closes the bio's loading row as it lands,
    ///     saying nothing on the status row.
    /// </summary>
    [Theory]
    [InlineData("nothing")]
    [InlineData("none")]
    public async Task TheBiosLoadingRowJustCloses_FailedOrOfferingNone(string ending)
    {
        var emoji = ending == "none" ? FakeInstanceEmoji.Offering() : FakeInstanceEmoji.Unanswering();
        emoji.Holding = true;

        using var drawn = await InTheBio(emoji: emoji);

        Type(drawn, "hi :bl");

        Assert.True(List(drawn).Visible);

        Assert.Single(emoji.Held).Land();
        drawn.Settle();

        Assert.False(List(drawn).Visible);
        Assert.Null(drawn.Shell.StatusRow.Notice);
    }

    /// <summary>
    ///     A bio already holding <c>:blobcat:</c> and an unfinished <c>:blob</c> reads nothing as the form opens on it and
    ///     the typing walks into it, nor as the caret is walked by the keys or clicked into either word, or to the end of
    ///     the one that would open the list (#528 story 29). The first letter typed there reads, once.
    /// </summary>
    [Fact]
    public async Task WalkingOrClickingIntoAColonWordReadsNothing_AndTypingInOneReadsOnce()
    {
        var emoji = FakeInstanceEmoji.Offering("blobcat");
        emoji.Holding = true;

        using var drawn = await InTheBio(bio: "hi :blobcat: and :blob", emoji: emoji);

        Assert.Equal("hi :blobcat: and :blob", Bio(drawn).Text);
        Assert.Empty(emoji.Reads);

        drawn.Press(Key.CursorLeft);
        drawn.Press(Key.Home);

        for (var step = 0; step < "hi :blob".Length; step++)
        {
            drawn.Press(Key.CursorRight);
        }

        var bio = Bio(drawn).FrameToScreen();

        drawn.Click(bio.X + "hi :blo".Length, bio.Y);
        drawn.Click(bio.X + "hi :blobcat: and :bl".Length, bio.Y);
        drawn.Press(Key.End);

        Assert.Empty(emoji.Reads);
        Assert.False(List(drawn).Visible);

        Type(drawn, "c");

        Assert.Single(emoji.Reads);
        Assert.True(List(drawn).Visible);
    }

    /// <summary>
    ///     The form opened with <c>E</c> on <b>You</b> on <paramref name="bio" />, and the typing walked down into the bio
    ///     at its foot — on a terminal drawing pictures <paramref name="way" /> from <paramref name="pictures" /> where
    ///     they are said, Kitty's placeholders where no way is — the instance offering <paramref name="emoji" />, or
    ///     <c>blobcat</c> and <c>thinking</c> where it is not said.
    /// </summary>
    private static async Task<DrawnShell> InTheBio(
        FakePictures? pictures = null,
        FakeTerminalImages? terminal = null,
        PictureWay way = PictureWay.Placeholders,
        string bio = "",
        FakeInstanceEmoji? emoji = null)
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: bio, account: Me)),
            Emoji = emoji ?? FakeInstanceEmoji.Offering("blobcat", "thinking"),
        };

        var drawn = pictures is null
            ? await DrawnShell.Of(80, 30, Themes.Plain, built)
            : await DrawnShell.Of(
                80,
                30,
                Themes.Plain,
                built,
                pictures: pictures,
                drawsPictures: way is PictureWay.Sixel,
                kittyImages: terminal,
                drawsPlaceholders: way is PictureWay.Placeholders,
                answersKitty: way is PictureWay.Kitty,
                raster: way switch
                {
                    PictureWay.Sixel => ARaster.Sixel(),
                    PictureWay.Kitty => ARaster.Kitty(),
                    _ => ARaster.Placeholders(),
                });

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

    /// <summary>The rows drawn within <paramref name="at" />, as text.</summary>
    private static List<string> RowsOf(DrawnShell drawn, Rectangle at) =>
        [.. drawn.Rows()[at.Y..at.Bottom].Select(row => row.Substring(at.X, at.Width))];

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }
}
