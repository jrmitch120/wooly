using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The direct messages destination: the conversations waiting, the thread one of them opens onto, and the reply
///     written without leaving either. Every one of these is a decision the shell makes without a terminal — which id
///     is shown, which id is marked read, what a reply is addressed to — which is the seam ADR-0005 asks for.
/// </summary>
public class ShellMessageTests
{
    /// <summary>Where the rail's direct messages destination is, counting from Home.</summary>
    private const int ToMessages = 5;

    [Fact]
    public async Task Step_ListsTheConversationsAndCountsTheUnreadOnesOnTheRail()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "7", with: ["alice@hachyderm.io"]),
                AConversation.With(id: "8", unread: false)),
        };

        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var screen = Assert.IsType<DirectMessagesScreen>(opened.Screen);
        Assert.Equal(["7", "8"], screen.Conversations.Select(conversation => conversation.Id));
        Assert.Equal("direct messages", opened.Breadcrumb);

        // The badge counts conversations with something unread in them, not conversations.
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Messages).Unread);
    }

    /// <summary>The indicator is on the row itself, not only on the rail: a list of who is waiting says which are new.</summary>
    [Fact]
    public async Task Step_MarksTheUnreadConversationsWhereTheyAreListed()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "7", with: ["alice@hachyderm.io"]),
                AConversation.With(id: "8", with: ["ben@hachyderm.io"], unread: false)),
        };

        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(61, AShell.Now).ToList();

        var alice = drawn.First(line => line.Text.Contains("alice", StringComparison.Ordinal));
        var ben = drawn.First(line => line.Text.Contains("ben", StringComparison.Ordinal));

        Assert.Contains("unread", alice.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("unread", ben.Text, StringComparison.Ordinal);
    }

    /// <summary>An account nobody has written to says so, rather than being a destination that swallowed a keypress.</summary>
    [Fact]
    public async Task Step_SaysSoWhereNobodyHasWritten()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("No direct conversations", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A listing a rate limit stopped part way through is said out loud (ADR-0007) — a reader told "no
    ///     conversations" would believe it.
    /// </summary>
    [Fact]
    public async Task Step_SaysWhereARateLimitStoppedTheListingPartWayThrough()
    {
        var shell = new AShell { Messages = FakeDirectMessages.RateLimitedAfter(AConversation.With()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Rate limited part way through", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A conversation is shown by its own id and not by the id of any post in it (CONTEXT.md) — the one mistake the
    ///     noun exists to make impossible.
    /// </summary>
    [Fact]
    public async Task Enter_OpensThePickedConversationByItsOwnId()
    {
        var thread = AConversation.Thread(
            AConversation.With(id: "7"),
            AConversation.DirectPost(id: "110", content: "Are you about?"),
            AConversation.DirectPost(id: "111", content: "All week"));

        var shell = new AShell { Messages = FakeDirectMessages.Threading(thread) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Equal("7", Assert.Single(shell.Messages.Shown).ConversationId);

        var screen = Assert.IsType<ConversationScreen>(opened.Screen);

        // Oldest first, which is the order it was said in.
        Assert.Equal(["110", "111"], screen.Posts.Select(post => post.Id));
        Assert.Equal("direct messages › with @alice@hachyderm.io", opened.Breadcrumb);
    }

    /// <summary>The thread is a screen on the same stack as everything else, so <c>esc</c> walks back to the list.</summary>
    [Fact]
    public async Task Back_WalksOutOfAConversationToTheListItWasOpenedFrom()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Threading(AConversation.Thread()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        opened.Back();

        Assert.IsType<DirectMessagesScreen>(opened.Screen);
    }

    /// <summary>
    ///     Reading a conversation does not mark it read (ADR-0013). A client that cleared the mark on the way past
    ///     would make "what have I not read" unanswerable for anything that looked afterwards.
    /// </summary>
    [Fact]
    public async Task Enter_LeavesTheUnreadMarkExactlyAsItFoundIt()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Threading(AConversation.Thread()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Empty(shell.Messages.MarkedRead);
        Assert.True(Assert.IsType<ConversationScreen>(opened.Screen).Conversation.Unread);
    }

    /// <summary>The conversation carries the mark, so the conversation's own id is what takes it off.</summary>
    [Fact]
    public async Task MarkRead_ClearsThePickedConversationByItsOwnId()
    {
        var shell = new AShell
        {
            Messages = FakeDirectMessages.Holding(
                AConversation.With(id: "7"),
                AConversation.With(id: "8")),
        };

        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.MarkRead();
        shell.Host.Drain();

        Assert.Equal("7", Assert.Single(shell.Messages.MarkedRead).ConversationId);

        var screen = Assert.IsType<DirectMessagesScreen>(opened.Screen);
        Assert.False(screen.Conversations[0].Unread);
        Assert.True(screen.Conversations[1].Unread);

        // The badge and the list are one fact, so one cannot say two over a list of one unread.
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Messages).Unread);
    }

    /// <summary>The same key, from inside the thread, where a reader who has just read it is most likely to press it.</summary>
    [Fact]
    public async Task MarkRead_ClearsTheConversationBeingReadFromInsideIt()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Threading(AConversation.Thread()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        await opened.MarkRead();
        shell.Host.Drain();

        Assert.Equal("7", Assert.Single(shell.Messages.MarkedRead).ConversationId);
        Assert.False(Assert.IsType<ConversationScreen>(opened.Screen).Conversation.Unread);

        opened.Back();

        // The list underneath it was holding the same conversation, and it says the same thing about it.
        Assert.False(Assert.IsType<DirectMessagesScreen>(opened.Screen).Conversations[0].Unread);
        Assert.Equal(0, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Messages).Unread);
    }

    /// <summary>Nothing to clear is nothing to ask an instance for, since the answer would be what it already says.</summary>
    [Fact]
    public async Task MarkRead_AsksForNothingWhereTheConversationIsAlreadyRead()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With(unread: false)) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.MarkRead();

        Assert.Empty(shell.Messages.MarkedRead);
        Assert.Equal("Already read.", opened.Notice);
    }

    /// <summary>
    ///     A conversation this client has just marked read is not worth remembering, whatever its age says — the same
    ///     rule a dismissed notification puts on the inbox.
    /// </summary>
    [Fact]
    public async Task MarkRead_ForgetsWhatTheDestinationHeldSoItIsListedAgain()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var listings = shell.Messages.Listings.Count;

        await opened.MarkRead();

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(listings + 1, shell.Messages.Listings.Count);
    }

    /// <summary>
    ///     Walking out along the rail and back is one listing rather than one per arrival (ADR-0014) — the question the
    ///     cache answers is "is this still what I just left", and a conversation list has the same answer as a
    ///     timeline.
    /// </summary>
    [Fact]
    public async Task Step_HoldsWhatTheDestinationListedForAShortWhile()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Holding(AConversation.With(id: "7")) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        var listings = shell.Messages.Listings.Count;

        opened.Step(1);
        shell.Host.Settle();
        opened.Step(-1);
        shell.Host.Settle();

        Assert.Equal(listings, shell.Messages.Listings.Count);

        var screen = Assert.IsType<DirectMessagesScreen>(opened.Screen);
        Assert.Equal(["7"], screen.Conversations.Select(conversation => conversation.Id));

        // Drawn from what was held, so the badge over it says what the rows do.
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Messages).Unread);
    }

    /// <summary>
    ///     A message in a thread is an ordinary post, so a mark put on one lands on the row the thread was opened from
    ///     as well — the two screens are on the stack together and hold the same message.
    /// </summary>
    [Fact]
    public async Task Mark_ShowsOnTheRowTheThreadWasOpenedFrom()
    {
        var message = AConversation.DirectPost(id: "110");
        var thread = AConversation.Thread(AConversation.With(id: "7", latest: message), message);

        var shell = new AShell
        {
            Messages = FakeDirectMessages.Threading(thread),
            Engagement = FakePostEngagement.Answering(message with { Marks = APost.Marked(favorited: true) }),
        };

        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();

        opened.Back();

        var listed = Assert.IsType<DirectMessagesScreen>(opened.Screen);

        Assert.True(listed.Conversations[0].Latest?.Marks.Favorited);
    }

    /// <summary>
    ///     A message taken down goes from the thread and from the row the thread was opened from — the conversation
    ///     itself stays, because it is still there to be read or written to.
    /// </summary>
    [Fact]
    public async Task Delete_TakesAMessageOffTheThreadAndTheConversationItWasTheLastOf()
    {
        var mine = APost.With(
            id: "110",
            account: "jeff@mastodon.social",
            content: "Sent in error",
            visibility: PostVisibility.Direct);

        var thread = AConversation.Thread(AConversation.With(id: "7", latest: mine), mine);

        var shell = new AShell { Messages = FakeDirectMessages.Threading(thread) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        opened.AskToDelete();
        await opened.Answer(agreed: true);
        shell.Host.Drain();

        Assert.Equal("110", Assert.Single(shell.Author.Deletions).PostId);
        Assert.Empty(Assert.IsType<ConversationScreen>(opened.Screen).Posts);

        opened.Back();

        var listed = Assert.IsType<DirectMessagesScreen>(opened.Screen);

        Assert.Single(listed.Conversations);
        Assert.Null(listed.Conversations[0].Latest);
        Assert.Contains(
            listed.Lines(61, AShell.Now),
            line => line.Text.Contains("Nothing left", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The whole of the third acceptance criterion: a reply is written where the thread is, and it goes out
    ///     answering the message it was written under.
    /// </summary>
    [Fact]
    public async Task Reply_AnswersAMessageFromInsideTheThread()
    {
        var thread = AConversation.Thread(
            AConversation.With(id: "7"),
            AConversation.DirectPost(id: "110", content: "Are you about?"));

        var shell = new AShell { Messages = FakeDirectMessages.Threading(thread) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);
        compose.Text += "All week";

        await opened.Send();
        shell.Host.Drain();

        var draft = Assert.Single(shell.Author.Published).Draft;

        Assert.Equal("110", draft.InReplyTo);
        Assert.Contains("All week", draft.Text, StringComparison.Ordinal);

        // Nothing is said about visibility: a reply goes out as narrowly as the post it answers, which the instance
        // adapter settles by reading that post rather than by this shell guessing (ADR-0013).
        Assert.Null(draft.Visibility);
    }

    /// <summary>
    ///     Mastodon delivers a direct post to the accounts its text mentions and to nobody else (ADR-0013), so a reply
    ///     in a conversation opens with the mention already written — otherwise it would reach nobody at all.
    /// </summary>
    [Fact]
    public async Task Reply_OpensWithTheConversationWrittenIntoIt()
    {
        var thread = AConversation.Thread(
            AConversation.With(id: "7", with: ["alice@hachyderm.io", "ben@hachyderm.io"]),
            AConversation.DirectPost(id: "110"));

        var shell = new AShell { Messages = FakeDirectMessages.Threading(thread) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Reply();

        var compose = Assert.IsType<ComposeScreen>(opened.Screen);

        Assert.Equal("@alice@hachyderm.io @ben@hachyderm.io ", compose.Text);

        compose.Text += "Both of you then";

        await opened.Send();
        shell.Host.Drain();

        Assert.StartsWith(
            "@alice@hachyderm.io @ben@hachyderm.io",
            Assert.Single(shell.Author.Published).Draft.Text,
            StringComparison.Ordinal);
    }

    /// <summary>A reply that is nothing but the mention it opened with is nothing the reader wrote, so nothing is sent.</summary>
    [Fact]
    public async Task Send_RefusesAReplyWithNothingOfTheReadersOwnInIt()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Threading(AConversation.Thread()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Reply();

        await opened.Send();
        shell.Host.Drain();

        Assert.Empty(shell.Author.Published);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary>A reply lands where the reader is looking, rather than only in the next listing of the conversation.</summary>
    [Fact]
    public async Task Send_PutsWhatWasJustSaidAtTheEndOfTheThread()
    {
        var thread = AConversation.Thread(
            AConversation.With(id: "7"),
            AConversation.DirectPost(id: "110", content: "Are you about?"));

        var sent = APost.With(id: "112", account: "jeff@mastodon.social", content: "All week");

        var shell = new AShell
        {
            Messages = FakeDirectMessages.Threading(thread),
            Author = FakePostAuthor.Answering(sent),
        };

        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();
        opened.Reply();

        ((ComposeScreen)opened.Screen).Text += "All week";

        await opened.Send();
        shell.Host.Drain();

        var screen = Assert.IsType<ConversationScreen>(opened.Screen);

        Assert.Equal(["110", "112"], screen.Posts.Select(post => post.Id));

        opened.Back();

        // And on the row it was opened from, since what was just said is the conversation's last word.
        var listed = Assert.IsType<DirectMessagesScreen>(opened.Screen);

        Assert.Equal("112", listed.Conversations[0].Latest?.Id);
    }

    /// <summary>
    ///     A direct message answered from anywhere but its conversation names whoever wrote it, and only them: there
    ///     is no conversation on screen to read the rest of it off, and a handle the message's text happened to name is
    ///     not the same thing as an account the instance is delivering it to (ADR-0013).
    /// </summary>
    [Fact]
    public async Task Reply_AddressesADirectMessageReadOutsideItsConversationToItsAuthor()
    {
        var message = APost.With(
            id: "220",
            account: "alice@hachyderm.io",
            visibility: PostVisibility.Direct,
            mentions: ["ben@hachyderm.io"]);

        var shell = new AShell { Timelines = FakeTimelineReader.Holding(message) };
        var opened = await shell.Opened();

        opened.Reply();

        Assert.Equal("@alice@hachyderm.io ", Assert.IsType<ComposeScreen>(opened.Screen).Text);
    }

    /// <summary>
    ///     The keys a reader can find on no other screen, on the part of the status row that survives the cut
    ///     (<c>docs/tui-shell.md</c>). A conversation list is a list of people rather than posts, so it offers none of
    ///     the keys that act on one.
    /// </summary>
    [Fact]
    public async Task Keys_SayWhatEachOfTheTwoScreensAnswersTo()
    {
        var shell = new AShell { Messages = FakeDirectMessages.Threading(AConversation.Thread()) };
        var opened = await shell.Opened();

        opened.Step(ToMessages);
        shell.Host.Settle();

        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "open" });
        Assert.Contains(opened.Keys, key => key is { Key: "m", Does: "mark read" });
        Assert.DoesNotContain(opened.Keys, key => key.Does == "delete");

        // A rail destination is the bottom of the stack, so it says tab rather than esc.
        Assert.Contains(opened.Keys, key => key.Key == "tab");
        Assert.DoesNotContain(opened.Keys, key => key.Key == "esc");

        await opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Contains(opened.Keys, key => key is { Key: "m", Does: "mark read" });
        Assert.Contains(opened.Keys, key => key is { Key: "r", Does: "reply" });
        Assert.Contains(opened.Keys, key => key.Key == "esc");
    }
}
