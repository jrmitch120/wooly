using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What every key in <c>docs/tui-shell.md</c>'s tables means, asked of the two things between a terminal and a
///     verb: the translation that turns a press into a <see cref="ShellKey" />, and the <see cref="Keymap" /> that
///     says what one means on the screen it was pressed on (#147).
/// </summary>
/// <remarks>
///     The key is written here the way the contract writes it — <c>ctrl-w</c>, <c>PgDn</c>, <c>←</c> — and
///     <see cref="Sent" /> turns that spelling into the press a terminal makes, so that a test reads down beside the
///     table it is asserting. Nothing here builds a <c>Window</c>: what the window still keeps for itself is the
///     handful of verbs that need a page or a laid-out <c>ShellWindow</c>, and those are pressed for real in
///     <see cref="ShellKeyTests" />.
/// </remarks>
public class KeymapTests
{
    /// <summary>
    ///     The frame. These may not vary by screen, so each is asked on four of them — including the two most likely
    ///     to want a key for something else, a compose screen and a search screen.
    /// </summary>
    /// <remarks>
    ///     None of the four is taking letters, which is deliberate: the contract's one exception to the frame is that
    ///     a prompt takes <c>/</c> and <c>?</c> as a query rather than as keys, and that is settled ahead of this
    ///     table rather than in it — so it is asserted where it happens, on a real window, in
    ///     <see cref="ShellKeyTests" />.
    /// </remarks>
    [Theory]
    [InlineData("esc", Verb.Back)]
    [InlineData("ctrl-q", Verb.Quit)]
    [InlineData("?", Verb.Help)]
    [InlineData("/", Verb.Search)]
    [InlineData("tab", Verb.NextDestination)]
    [InlineData("shift-tab", Verb.PreviousDestination)]
    public void TheFrameKeysMeanTheSameThingOnEveryScreen(string key, Verb verb)
    {
        Assert.Equal(verb, Means(key, Feed()));
        Assert.Equal(verb, Means(key, new ComposeScreen(ComposeFor.Post)));
        Assert.Equal(verb, Means(key, Searched()));
        Assert.Equal(verb, Means(key, Notifications()));
    }

    /// <summary>
    ///     What a post answers to. <c>k</c> being the next post and <c>j</c> the one before it is the opposite way
    ///     round from vim, which is exactly the kind of thing that gets quietly reversed.
    /// </summary>
    [Theory]
    [InlineData("k", Verb.NextPost)]
    [InlineData("j", Verb.PreviousPost)]
    [InlineData("↓", Verb.ScrollDown)]
    [InlineData("↑", Verb.ScrollUp)]
    [InlineData("PgDn", Verb.PageDown)]
    [InlineData("PgUp", Verb.PageUp)]
    [InlineData("Home", Verb.FirstPost)]
    [InlineData("End", Verb.LastPost)]
    [InlineData("⏎", Verb.OpenPost)]
    [InlineData("a", Verb.OpenAuthor)]
    [InlineData("c", Verb.Compose)]
    [InlineData("r", Verb.Reply)]
    [InlineData("b", Verb.Boost)]
    [InlineData("f", Verb.Favorite)]
    [InlineData("p", Verb.Pin)]
    [InlineData("e", Verb.Edit)]
    [InlineData("d", Verb.Delete)]
    [InlineData("x", Verb.Reveal)]
    [InlineData("←", Verb.PreviousReference)]
    [InlineData("→", Verb.NextReference)]
    [InlineData("v", Verb.Vote)]
    [InlineData("g", Verb.Refresh)]
    public void AFeedAnswersToEveryKeyThatActsOnAPost(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, Feed()));

    /// <summary>
    ///     The account screen's three, which are capitals so that a lower-case mark key can never fire a tie by
    ///     accident — the one thing about this keymap that a case-blind match would quietly undo.
    /// </summary>
    [Theory]
    [InlineData("F", Verb.Follow)]
    [InlineData("M", Verb.Mute)]
    [InlineData("B", Verb.Block)]
    public void TheCapitalsAreTheTies(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, new AccountScreen(AnAccount.With(), [])));

    /// <summary>And their lower-case neighbours are still the marks, on the very screen the ties are offered on.</summary>
    [Theory]
    [InlineData("f", Verb.Favorite)]
    [InlineData("b", Verb.Boost)]
    public void ALowerCaseMarkKeyIsStillAMarkWhereTheTiesAreOffered(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, new AccountScreen(AnAccount.With(), [])));

    /// <summary>
    ///     The notifications inbox: <c>d</c> dismisses one by the notification's own id — the collision the contract
    ///     names — and <c>D</c> empties the lot.
    /// </summary>
    [Theory]
    [InlineData("d", Verb.Dismiss)]
    [InlineData("D", Verb.ClearAll)]
    public void TheInboxTakesDForItself(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, Notifications()));

    /// <summary>Follow requests: all three of the keys there are answers to a question about a person.</summary>
    [Theory]
    [InlineData("a", Verb.AcceptRequest)]
    [InlineData("x", Verb.RejectRequest)]
    [InlineData("⏎", Verb.OpenAsker)]
    public void TheRequestsScreenTakesThreeKeysForItself(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, new FollowRequestsScreen([AnAccount.With()])));

    /// <summary>The conversations list, and the thread it opens onto — <c>m</c> means the same thing on both.</summary>
    [Fact]
    public void TheMessagesScreenOpensAConversationAndMarksItRead()
    {
        var messages = new DirectMessagesScreen([AConversation.With()]);

        Assert.Equal(Verb.OpenConversation, Means("⏎", messages));
        Assert.Equal(Verb.MarkRead, Means("m", messages));
        Assert.Equal(Verb.MarkRead, Means("m", new ConversationScreen(AConversation.Thread())));
    }

    /// <summary>
    ///     A search prompt and what it found are the same screen answering <c>⏎</c> two ways: put the query, then open
    ///     what came back.
    /// </summary>
    [Fact]
    public void TheSearchScreenSearchesWhileItIsTypingAndOpensAResultAfterwards()
    {
        var search = new SearchScreen();

        Assert.Equal(Verb.Find, Means("⏎", search));

        search.Found("cats", new SearchResults { Accounts = [AnAccount.With()] });

        Assert.Equal(Verb.OpenResult, Means("⏎", search));
    }

    /// <summary>
    ///     A picked reference is a level of its own inside the screen, so <c>⏎</c> means the reference wherever one is
    ///     picked — ahead of whatever the screen's own <c>⏎</c> would have meant (#85).
    /// </summary>
    [Fact]
    public void EnterOpensThePickedReferenceAheadOfTheScreensOwnMeaning()
    {
        var feed = Feed(APost.With(content: "Read https://example.com/sheep"));

        Assert.Equal(Verb.OpenPost, Means("⏎", feed));

        Assert.True(feed.WalkReference(1));

        Assert.Equal(Verb.OpenReference, Means("⏎", feed));
    }

    /// <summary>The three a compose screen answers to, and the one before it that is the frame's (<c>esc</c>).</summary>
    [Theory]
    [InlineData("ctrl-s", Verb.Send)]
    [InlineData("ctrl-w", Verb.WriteWarning)]
    [InlineData("esc", Verb.Back)]
    public void AComposeScreenAnswersToItsOwnThree(string key, Verb verb) =>
        Assert.Equal(verb, Means(key, new ComposeScreen(ComposeFor.Post)));

    /// <summary>
    ///     And nowhere else. They are bound on the window as well as on the editor because the editor gives up focus
    ///     while the warning is taking letters (#123) — screen-local all the same, so off a compose screen they are
    ///     left to whatever else wants them.
    /// </summary>
    [Theory]
    [InlineData("ctrl-s")]
    [InlineData("ctrl-w")]
    public void TheComposeKeysMeanNothingOffAComposeScreen(string key) =>
        Assert.Equal(Verb.None, Means(key, Feed()));

    /// <summary>
    ///     The digits address the answers of the picked post's poll directly: <c>1</c>-<c>9</c> then <c>0</c>, so that
    ///     ten of them are reachable along one row of keys and the tenth is where a person's own counting puts it.
    /// </summary>
    [Theory]
    [InlineData("1", 0)]
    [InlineData("2", 1)]
    [InlineData("3", 2)]
    [InlineData("4", 3)]
    [InlineData("5", 4)]
    [InlineData("6", 5)]
    [InlineData("7", 6)]
    [InlineData("8", 7)]
    [InlineData("9", 8)]
    [InlineData("0", 9)]
    public void EveryDigitTogglesTheAnswerItAddresses(string key, int answer)
    {
        var pressed = Pressed(key);

        Assert.Equal(Verb.Toggle, Keymap.Means(pressed, Feed()));
        Assert.Equal(answer, Keymap.Answer(pressed));
    }

    /// <summary>Which answer a digit addresses is a fact about the key, so nothing else carries one.</summary>
    [Fact]
    public void NothingButADigitAddressesAnAnswer() =>
        Assert.Null(Keymap.Answer(Pressed("v")));

    /// <summary>
    ///     What a terminal sends that this shell has no word for: a function key, an alt pair, a ctrl pair that is not
    ///     one of the three, and a capital that is not one of the four. None of them reaches the keymap at all, which
    ///     is what keeps <c>ctrl-b</c> from boosting.
    /// </summary>
    [Fact]
    public void AKeyTheContractDoesNotNameIsNotTranslatedAtAll()
    {
        Assert.Null(ShellKeys.Of(Key.F1));
        Assert.Null(ShellKeys.Of(Key.B.WithCtrl));
        Assert.Null(ShellKeys.Of(Key.A.WithAlt));
        Assert.Null(ShellKeys.Of(new Key('K')));
        Assert.Null(ShellKeys.Of(new Key('z')));
    }

    /// <summary>A feed of one post, which is every screen that shows posts as far as a key is concerned.</summary>
    private static FeedScreen Feed(params Post[] posts) =>
        new(new Destination(DestinationKind.Home, "home"), posts.Length > 0 ? posts : [APost.With()]);

    private static NotificationsScreen Notifications() => new([ANotification.With()]);

    /// <summary>A search screen showing what it found, which is the one that has stopped taking letters.</summary>
    private static SearchScreen Searched()
    {
        var search = new SearchScreen();

        search.Found("cats", new SearchResults { Accounts = [AnAccount.With()] });

        return search;
    }

    /// <summary>What <paramref name="key" /> means on <paramref name="screen" />, pressed as a terminal sends it.</summary>
    private static Verb Means(string key, Screen screen) => Keymap.Means(Pressed(key), screen);

    /// <summary>Which of this shell's keys that press is, which every key the contract names has to be one of.</summary>
    private static ShellKey Pressed(string key)
    {
        var pressed = ShellKeys.Of(Sent(key));

        Assert.NotNull(pressed);

        return pressed.Value;
    }

    /// <summary>
    ///     The key as <c>docs/tui-shell.md</c> writes it, as a terminal delivers it — so that a test can say
    ///     <c>shift-tab</c> and mean the press rather than the code.
    /// </summary>
    private static Key Sent(string key) => key switch
    {
        "esc" => Key.Esc,
        "⏎" => Key.Enter,
        "tab" => Key.Tab,
        "shift-tab" => Key.Tab.WithShift,
        "ctrl-q" => Key.Q.WithCtrl,
        "ctrl-s" => Key.S.WithCtrl,
        "ctrl-w" => Key.W.WithCtrl,
        "↑" => Key.CursorUp,
        "↓" => Key.CursorDown,
        "←" => Key.CursorLeft,
        "→" => Key.CursorRight,
        "PgUp" => Key.PageUp,
        "PgDn" => Key.PageDown,
        "Home" => Key.Home,
        "End" => Key.End,
        _ => new Key(key.Single()),
    };
}
