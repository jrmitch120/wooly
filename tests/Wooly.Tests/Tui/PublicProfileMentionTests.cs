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

    /// <summary>
    ///     The shell with three people already seen on the home timeline, the form opened with <c>E</c> on <b>You</b> and
    ///     the typing walked down into an empty bio.
    /// </summary>
    private static async Task<DrawnShell> InTheBio()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "1", account: "maria@fosstodon.org", author: "Maria Gonzalez"),
                APost.With(id: "2", account: "mark@mastodon.social", author: "Mark"),
                APost.With(id: "3", account: "ben@hachyderm.io", author: "Ben")),
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(bio: string.Empty, account: Me)),
        };

        var drawn = await DrawnShell.Of(Wide, Tall, Themes.Plain, built);

        drawn.Shell.Rail.GoTo(DestinationKind.Profile);
        drawn.Settle();
        drawn.Press(new Key('E'));
        drawn.Settle();
        drawn.Press(Key.CursorDown);

        Assert.Equal(new PublicProfileStop(PublicProfilePart.Bio), Form(drawn).Typing);

        return drawn;
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
}
