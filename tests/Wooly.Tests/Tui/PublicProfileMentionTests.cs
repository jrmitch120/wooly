using Terminal.Gui.Input;
using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     Typing <c>@</c> at the start of a word in the edit public profile form's bio offers people to mention under it
///     (#408), the same list compose's post has (#318) — driven through the shell's window with the keys and clicks a
///     reader would use, and what is drawn read back off the terminal.
/// </summary>
public class PublicProfileMentionTests
{
    private const int Wide = 80;

    private const int Tall = 30;

    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>The form opened over <paramref name="bio" />, and the typing walked down into it.</summary>
    private static async Task<DrawnShell> InTheBio(string bio = "")
    {
        var drawn = await Opened(bio);

        WalkToTheBio(drawn);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Bio), Form(drawn).Typing);

        return drawn;
    }

    /// <summary>The shell with three people already seen on the home timeline, the form opened with <c>E</c> on <b>You</b>.</summary>
    private static async Task<DrawnShell> Opened(string bio)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "1", account: "maria@fosstodon.org", author: "Maria Gonzalez"),
                APost.With(id: "2", account: "mark@mastodon.social", author: "Mark"),
                APost.With(id: "3", account: "ben@hachyderm.io", author: "Ben")),
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: bio, account: Me)),
        };

        var drawn = await DrawnShell.Of(Wide, Tall, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();

        return drawn;
    }

    /// <summary>The typing walked down the form into the bio at its foot.</summary>
    private static void WalkToTheBio(DrawnShell drawn)
    {
        while (Form(drawn).Typing.Part != PublicProfilePart.Bio)
        {
            drawn.Press(Key.CursorDown);
        }
    }

    private static PublicProfileScreen Form(DrawnShell drawn) => Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);

    private static ComposeEditor Bio(DrawnShell drawn) => drawn.Window.ProfileField<ComposeEditor>();

    /// <summary>The list of people to mention, among the form's own views.</summary>
    private static PaintedView List(DrawnShell drawn) =>
        drawn.Window.SubViews.OfType<PublicProfileView>().Single()
             .SubViews.OfType<PaintedView>().Single(view => view.Id == MentionList.Id);

    private static void Type(DrawnShell drawn, string text)
    {
        foreach (var letter in text)
        {
            drawn.Press(new Key(letter));
        }
    }

    /// <summary>
    ///     An @-word in the bio opens the list on the row under it, naming everybody it matches; <c>tab</c> puts the
    ///     picked one in place of the word, with a space to carry on after, and closes it.
    /// </summary>
    [Fact]
    public async Task AnAtWord_OpensTheList_AndTabInsertsThePick()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi @ma");

        var rows = drawn.Rows();

        Assert.True(List(drawn).Visible);
        Assert.Equal(Bio(drawn).FrameToScreen().Y + 1, List(drawn).FrameToScreen().Y);
        Assert.Contains(rows, row => row.Contains("Maria Gonzalez  @maria@fosstodon.org", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("Mark  @mark@mastodon.social", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("@ben", StringComparison.Ordinal));

        drawn.Press(Key.Tab);

        Assert.Equal("hi @maria@fosstodon.org ", Form(drawn).Bio);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     While the list is open, <c>↓</c> moves the pick rather than walking off the bio's last line, and <c>⏎</c> puts
    ///     the picked one in rather than starting a new line — as the profile writes them, short for its own instance.
    /// </summary>
    [Fact]
    public async Task DownMovesThePick_AndEnterInsertsIt()
    {
        using var drawn = await InTheBio();

        Type(drawn, "@ma");
        drawn.Press(Key.CursorDown);
        drawn.Press(Key.Enter);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Bio), Form(drawn).Typing);
        Assert.Equal("@mark ", Form(drawn).Bio);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     <c>esc</c> closes the list and never the form: nothing is asked and the bio is as typed. With the list closed,
    ///     <c>esc</c> is the form's way out again, asking first over what was touched.
    /// </summary>
    [Fact]
    public async Task Esc_ClosesTheList_NotTheForm()
    {
        using var drawn = await InTheBio();

        Type(drawn, "@ma");
        drawn.Press(Key.Esc);

        Assert.False(List(drawn).Visible);
        Assert.Null(drawn.Shell.Asking);
        Assert.Equal("@ma", Form(drawn).Bio);

        drawn.Press(Key.Esc);

        Assert.Equal("Discard these changes?", drawn.Shell.Asking?.Ask);
        Assert.IsType<PublicProfileScreen>(drawn.Shell.Screen);
    }

    /// <summary>A click on a person inserts them, as <c>tab</c> does on the picked one, and closes the list.</summary>
    [Fact]
    public async Task AClickOnAPerson_InsertsThem()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi @ma");

        var at = List(drawn).FrameToScreen();

        drawn.Click(at.X + 4, at.Y + 2);

        Assert.Equal("hi @mark ", Form(drawn).Bio);
        Assert.False(List(drawn).Visible);
    }

    /// <summary>
    ///     A click outside the open list closes it and does nothing else: the typing stays in the bio, and the next letter
    ///     carries on the word.
    /// </summary>
    [Fact]
    public async Task AClickOutsideTheList_OnlyClosesIt()
    {
        using var drawn = await InTheBio();

        Type(drawn, "hi @ma");

        // On the display name's field, which a click would otherwise take the typing to.
        var name = drawn.Window.ProfileField<PublicProfileTextField>().FrameToScreen();

        drawn.Click(name.X + 1, name.Y);

        Assert.False(List(drawn).Visible);
        Assert.Equal(new PublicProfileStop(PublicProfilePart.Bio), Form(drawn).Typing);

        Type(drawn, "x");

        Assert.Equal("hi @max", Form(drawn).Bio);
    }

    /// <summary>
    ///     A bio that was written ending on an @-word opens no list over the form while the typing is elsewhere, so nothing
    ///     hangs over a field nobody is typing in; walked into, the bio offers it as typing it would.
    /// </summary>
    [Fact]
    public async Task ABioEndingOnAnAtWord_OffersNothingUntilTheTypingIsInIt()
    {
        using var drawn = await Opened("Ask @ma");

        Assert.Equal(new PublicProfileStop(PublicProfilePart.DisplayName), Form(drawn).Typing);
        Assert.False(List(drawn).Visible);

        WalkToTheBio(drawn);

        Assert.True(List(drawn).Visible);
    }

    /// <summary>
    ///     The form left with the caret on an @-word and opened again offers nothing until the typing is back in the bio,
    ///     and then offers it afresh, whatever was closed last time.
    /// </summary>
    [Fact]
    public async Task TheFormOpenedAgain_OffersAfresh()
    {
        using var drawn = await InTheBio("Ask @ma");

        Assert.True(List(drawn).Visible);

        drawn.Press(Key.Esc);
        drawn.Press(Key.Esc);

        Assert.IsType<AccountScreen>(drawn.Shell.Screen);

        drawn.Press(new Key('E'));
        drawn.Settle();

        Assert.Equal(new PublicProfileStop(PublicProfilePart.DisplayName), Form(drawn).Typing);
        Assert.False(List(drawn).Visible);

        WalkToTheBio(drawn);

        Assert.True(List(drawn).Visible);
    }
}
