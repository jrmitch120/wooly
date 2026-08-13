using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Search;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Shell;

/// <summary>
///     The TUI's application shell: the rail, the stack of screens you drill into and walk back out of, and the one
///     thing in the TUI that reaches an instance. Everything here is decided without a terminal — which destination is
///     selected, what a run of rail steps fetches, whether an answer that arrived late is drawn, whether a delete has
///     been agreed to — so all of it is testable at the same port seam the CLI's commands are (ADR-0005, ADR-0014).
/// </summary>
/// <remarks>
///     Views observe this and draw it. Nothing here holds a Terminal.Gui type, and the two things it needs a terminal
///     for — waiting, and getting back onto the drawing thread — come in through <see cref="IShellHost" />.
///     <para>
///         Every question a reader is waiting on is put through <see cref="Enquiry" />, which is what makes the
///         rate-limit wait, the failure notice and the drop-on-arrival rule the same at all of them rather than
///         copied at each. The counts the rail carries are the exception, and read their ports directly: nobody is
///         waiting on a badge, so one that could not be read is drawn as no count rather than counted down over.
///     </para>
/// </remarks>
public sealed class Shell
{
    /// <summary>
    ///     What a reader is told when a picked reference goes nowhere. Three of them, because there are three ways
    ///     for <c>⏎</c> to have nothing to open: a handle the post never named, an address no browser can be handed,
    ///     and a machine with no browser on it (<c>docs/tui-shell.md</c>, #85).
    /// </summary>
    private const string MentionUnresolved = "That mention couldn't be resolved.";

    /// <inheritdoc cref="MentionUnresolved" />
    private const string AddressRefused = "That kind of address isn't opened.";

    /// <inheritdoc cref="MentionUnresolved" />
    private const string NoBrowser = "No browser available.";

    /// <summary>
    ///     What arriving at a destination means, which is the same six steps at every one of them that reads a list
    ///     (#100).
    /// </summary>
    private readonly Arrival _arrival;

    /// <summary>
    ///     Where an address goes. The one thing this shell does that leaves the terminal, and deliberately not one of
    ///     <see cref="ShellPorts" />: those are what the shell reaches an <em>instance</em> through, and a browser is
    ///     not on one (ADR-0014, #85).
    /// </summary>
    private readonly IWebBrowser _browser;

    private readonly DestinationCache _cache;

    /// <summary>Everything this reaches an instance through, and the one place the stale-answer rule is stated.</summary>
    private readonly Enquiry _enquiry;

    private readonly IShellHost _host;
    private readonly ActiveProfile _profile;
    private readonly ShellPorts _ports;
    private readonly List<Screen> _stack = [];

    private Func<Task>? _confirming;

    public Shell(
        ActiveProfile profile,
        ShellPorts ports,
        IShellHost host,
        IWebBrowser browser,
        TimeProvider clock,
        ShellTiming timing,
        string? hashtag = null)
    {
        _profile = profile;
        _ports = ports;
        _host = host;
        _browser = browser;
        _cache = new DestinationCache(clock, timing.CacheFor);

        _enquiry = new Enquiry(host, clock, timing.CountdownStep);
        _enquiry.Said += Say;
        _enquiry.Changed += () => Changed?.Invoke();

        // An arrival settles what a destination is on screen and what its badge says; putting either there is this
        // shell's own business, since the stack and the rail are its.
        _arrival = new Arrival(profile, ports, _enquiry, _cache, host);
        _arrival.Shows += Reset;
        _arrival.Counts += Counted;

        Rail = new Rail(Destinations(profile, hashtag), host, timing.Settle);
        Rail.Selected += destination => _ = Go(destination);
        Rail.Changed += () => Changed?.Invoke();

        _stack.Add(new FeedScreen(Rail.Showing, []));
    }

    /// <summary>Raised whenever anything on screen has changed. Always on the drawing thread.</summary>
    public event Action? Changed;

    /// <summary>The rail: the nine destinations, the cursor, and the selection.</summary>
    public Rail Rail { get; }

    /// <summary>The screen on top of the stack, which is what the content region is showing.</summary>
    public Screen Screen => _stack[^1];

    /// <summary>How deep the drill is, where one is a destination with nothing opened from it.</summary>
    public int Depth => _stack.Count;

    /// <summary>Where you are, as the trail along the top: <c>home › post by @ben › @ben@hachyderm.io</c>.</summary>
    public string Breadcrumb => string.Join(" › ", _stack.Select(screen => screen.Crumb));

    /// <summary>Whether a fetch is in flight, which the breadcrumb says once and the rail never does.</summary>
    public bool Fetching => _enquiry.Fetching;

    /// <summary>
    ///     Something the shell has to say out loud that is not a screen: a refusal, or the countdown on a rate limit
    ///     being waited out.
    /// </summary>
    public string? Notice { get; private set; }

    /// <summary>Whether that notice is a failure rather than a remark, which settles the role it is drawn in.</summary>
    public bool NoticeIsError { get; private set; }

    /// <summary>What the shell is waiting to be told again before it does, or <see langword="null" /> if nothing.</summary>
    public Confirmation? Asking { get; private set; }

    /// <summary>What the instance last said is left of the profile's budget, for the rail's foot (story 54).</summary>
    public RateLimitQuota? Quota => _ports.RateLimit.Latest;

    /// <summary>The keys the current screen answers to, for the status row.</summary>
    public IReadOnlyList<KeyHint> Keys => Screen.Keys;

    /// <summary>
    ///     The conversation <c>m</c> would mark read: the one being read, or the one picked out on the list. The two
    ///     screens that have one, in one place, so that the key means the same thing on both.
    /// </summary>
    private Conversation? Reading => Screen switch
    {
        ConversationScreen conversation => conversation.Conversation,
        DirectMessagesScreen messages => messages.PickedConversation,
        _ => null,
    };

    /// <summary>Opens the shell onto its first destination, and reads the counts the rail carries.</summary>
    public async Task Open()
    {
        await Go(Rail.Showing);
        await Counts();
    }

    /// <summary>
    ///     A rail keypress. The cursor moves at once; the selection — and the fetch — follow when the pressing stops.
    /// </summary>
    public void Step(int by) => Rail.Step(by);

    /// <summary>
    ///     What a key that means different things on different screens means <em>here</em>. Every collision the
    ///     contract allows, in one table, so that a reader can see at once that <c>d</c> is dismiss on one screen and
    ///     delete on every other (<c>docs/tui-shell.md</c>).
    /// </summary>
    /// <remarks>
    ///     Only the four keys that collide come through here. A screen's own key that nothing else uses — <c>F</c>,
    ///     <c>M</c>, <c>B</c>, <c>D</c> — is a verb of its own and needs no table to tell it apart from anything.
    ///     <para>
    ///         It lives on the shell rather than in the window because the window binds keys and knows nothing about
    ///         screens — and each arm below is a public verb of its own, so a test can ask for the meaning it is
    ///         about without going through a keypress to get at it.
    ///     </para>
    /// </remarks>
    public Task Press(ShellKey key) => (key, Screen) switch
    {
        // A picked reference is a level of its own inside the screen, so ⏎ means the reference wherever one is picked
        // — which is what the status row says while one is, ahead of whatever the screen's own ⏎ would have meant
        // (docs/tui-shell.md, #85).
        (ShellKey.Enter, _) when Screen.Reference is not null => OpenReference(),
        (ShellKey.Enter, SearchScreen search) => search.IsTyping ? Find() : OpenResult(),
        (ShellKey.Enter, FollowRequestsScreen) => OpenAsker(),
        (ShellKey.Enter, DirectMessagesScreen) => OpenConversation(),
        (ShellKey.Author, FollowRequestsScreen) => AnswerRequest(accepted: true),
        (ShellKey.Discard, NotificationsScreen) => Dismiss(),
        (ShellKey.Reject, FollowRequestsScreen) => AnswerRequest(accepted: false),
        (ShellKey.Enter, _) => Enter(),
        (ShellKey.Author, _) => OpenAuthor(),
        (ShellKey.Discard, _) => AtOnce(AskToDelete),
        (ShellKey.Reject, _) => AtOnce(Reveal),
        _ => Task.CompletedTask,
    };

    /// <summary>Moves what is picked out on the current screen.</summary>
    public void Move(int by)
    {
        Screen.Move(by);
        Changed?.Invoke();
    }

    /// <summary>
    ///     What <c>j</c> and <c>k</c> do: walk the selection by <paramref name="by" /> posts — or, where the reader
    ///     has scrolled it off the page with the arrows, take back the post they are actually looking at (#51).
    /// </summary>
    /// <remarks>
    ///     Only a view knows how tall the terminal is and where the rows have been scrolled to, so the view is what
    ///     works out <paramref name="reclaiming" /> and this is what does something about it. The first press
    ///     reclaims and the next moves on from there, because after the first there is nothing left to reclaim.
    /// </remarks>
    /// <param name="by">How many posts to move, where the selection is still on the page.</param>
    /// <param name="reclaiming">
    ///     The topmost post on the page, where the selection has none of its rows on it, or <see langword="null" />
    ///     while it is still visible.
    /// </param>
    public void Walk(int by, int? reclaiming)
    {
        if (reclaiming is { } at)
        {
            Screen.Pick(at);
        }
        else
        {
            Screen.Move(by);
        }

        Changed?.Invoke();
    }

    /// <summary>
    ///     What <c>←</c> and <c>→</c> do: walk the references inside the picked post — <c>→</c> entering at the first
    ///     and <c>←</c> at the last, clamping at either end (#83).
    /// </summary>
    /// <returns>
    ///     Whether the screen had any references to walk, which is what settles whether the key was used: a screen
    ///     with none — the compose editor above all — leaves the arrows to whatever else wants them.
    /// </returns>
    public bool WalkReference(int by)
    {
        if (!Screen.WalkReference(by))
        {
            return false;
        }

        Changed?.Invoke();

        return true;
    }

    /// <summary>Shows what the picked post's content warning is hiding.</summary>
    public void Reveal()
    {
        if (Screen.Reveal())
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Opens the picked post, with what has been said in answer to it.</summary>
    /// <remarks>
    ///     What it opens is the screen's <see cref="Screen.Opens" /> rather than what is picked out, which are the same
    ///     post everywhere except inside a post: there, the post picked out at the top is the one already on screen
    ///     (#48).
    /// </remarks>
    public async Task Enter()
    {
        if (Screen.Opens is not { } opening)
        {
            return;
        }

        await _enquiry.Put(
            ask => ReadReplies(ask, opening),
            ifStillHere: replies => Push(new PostScreen(opening, replies)));
    }

    /// <summary>
    ///     Asks for what is there now: evicts what the destination last held and puts the same question its own
    ///     arrival puts, keeping the reader on the post they were reading (<c>docs/tui-shell.md</c>, #84).
    /// </summary>
    /// <remarks>
    ///     Only where the screen says it answers to <c>g</c>, which is the nine the contract names. A second press
    ///     while anything is already in flight does nothing at all — no second question, and no in-flight UI beyond
    ///     the <c>fetching…</c> marker the breadcrumb already carries.
    ///     <para>
    ///         Seven of the nine are destinations and go back through <see cref="Arrival" />, which is one refresh for
    ///         all of them: what to evict, what to read, what it becomes and what it counts are all things the
    ///         destination already says (#100). The other two are screens the stack was drilled into, and each puts
    ///         its own question again.
    ///     </para>
    /// </remarks>
    public Task Refresh()
    {
        if (!Screen.Refreshes || Fetching)
        {
            return Task.CompletedTask;
        }

        // Taken down before anything is asked, because the arrival below puts an empty screen up at once and the
        // reader's place goes with the screen it was on.
        var place = Screen.Place;

        return Screen switch
        {
            PostScreen post => RefreshPost(post, place),
            AccountScreen account => RefreshAccount(account, place),
            _ => RefreshDestination(place),
        };
    }

    /// <summary>
    ///     Opens whatever the picked reference points at, which is three different things: a hashtag's timeline, the
    ///     account a mention names, or an address, in the platform's own browser (#85).
    /// </summary>
    /// <remarks>
    ///     Which of the three it is, is the role the reference draws in — one vocabulary rather than two, which is the
    ///     bargain <c>Reference</c> already struck: a second enum saying the same thing would be a second place to add
    ///     the fourth kind to.
    ///     <para>
    ///         Only the address arm leaves the terminal, and it is the only arm that pushes nothing: the reader has
    ///         been sent somewhere this client does not draw, so there is nothing to come back from with <c>esc</c>.
    ///     </para>
    /// </remarks>
    public Task OpenReference()
    {
        if (Screen.Reference is not { } reference)
        {
            return Task.CompletedTask;
        }

        switch (reference.Role)
        {
            case Role.Hashtag:
                // The same screen and the same breadcrumb a search result for a tag opens, and the rail's own hashtag
                // destination left alone — that is a setting the reader wrote down, not something a keypress changes.
                return OpenTag(reference.Text.TrimStart('#'));

            case Role.Mention:
                return OpenMention();

            case Role.Link:
                OpenAddress(reference.Text);

                return Task.CompletedTask;

            default:
                // Named rather than left as the fall-through, because the fall-through here is the one path that
                // leaves the machine: a fourth kind of reference added to BodyText and forgotten about must open
                // nothing rather than be handed to a browser as an address.
                return Task.CompletedTask;
        }
    }

    /// <summary>Opens the account that wrote the picked post.</summary>
    public async Task OpenAuthor()
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        await OpenAccount(AccountAddress.Parse((picked.Boosted ?? picked).Account));
    }

    /// <summary>Walks back up one level of the stack. Never quits, and never leaves the shell with nothing on it.</summary>
    public void Back()
    {
        if (Asking is not null)
        {
            // Escaping out of a confirmation is answering it, and the answer is no.
            Asking = null;
            _confirming = null;

            Changed?.Invoke();

            return;
        }

        // A reference pick is a level of its own, so esc is up one level of whichever kind is open: the first press
        // lets the pick go and the next pops the screen (docs/tui-shell.md, #83).
        if (Screen.ClearReference())
        {
            Changed?.Invoke();

            return;
        }

        if (_stack.Count > 1)
        {
            _stack.RemoveAt(_stack.Count - 1);
        }

        Notice = null;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Goes to search, which is a frame key rather than a screen's (<c>docs/tui-shell.md</c>): it means the same
    ///     thing everywhere, and from the search destination itself it means a fresh prompt rather than nothing —
    ///     otherwise the one place the key is most likely to be pressed is the one place it does nothing.
    /// </summary>
    public void Search()
    {
        if (Rail.Showing.Kind == DestinationKind.Search)
        {
            Reset(new SearchScreen());

            return;
        }

        Rail.GoTo(DestinationKind.Search);
    }

    /// <summary>Puts a letter into whatever is being typed into, which is only ever the search prompt.</summary>
    public void Type(char letter)
    {
        if (Screen is SearchScreen { IsTyping: true } search)
        {
            search.Type(letter);
            Changed?.Invoke();
        }
    }

    /// <summary>Takes the last letter back out of it.</summary>
    public void Backspace()
    {
        if (Screen is SearchScreen { IsTyping: true } search)
        {
            search.Backspace();
            Changed?.Invoke();
        }
    }

    /// <summary>Asks the instance for what has been typed into the prompt.</summary>
    /// <remarks>
    ///     A search is one call, so a rate limit leaves nothing to draw and is waited out rather than half-answered
    ///     (ADR-0011) — which <see cref="Enquiry" /> already does, and is why this reads like every other fetch here.
    /// </remarks>
    public async Task Find()
    {
        if (Screen is not SearchScreen search)
        {
            return;
        }

        if (!SearchQuery.IsWellFormed(search.Query))
        {
            // The same words the command turns an empty query down with, so that the two front ends cannot come to
            // say different things about the same empty value.
            Say(SearchQuery.Rejection, isError: true);

            return;
        }

        var query = SearchQuery.For(search.Query);

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Search.Find(_profile, query, token)),
            ifStillHere: found =>
            {
                if (Screen is not SearchScreen still)
                {
                    return;
                }

                still.Found(query.Text, found);
                Changed?.Invoke();
            });
    }

    /// <summary>
    ///     Opens whatever a search turned up and the reader picked out: an account, a hashtag's timeline, or a post.
    /// </summary>
    /// <remarks>
    ///     A hashtag opens as a screen on the stack rather than as the rail's own hashtag destination. Which tag the
    ///     rail keeps a place for is a setting the reader wrote down (<c>docs/tui-shell.md</c>), and a search result
    ///     is not them changing their mind about it.
    /// </remarks>
    public async Task OpenResult()
    {
        if (Screen is not SearchScreen search)
        {
            return;
        }

        if (search.PickedAccount is { } account)
        {
            await OpenAccount(AccountAddress.Parse(account.Address));

            return;
        }

        if (search.PickedHashtag is { } hashtag)
        {
            await OpenTag(hashtag.Name);

            return;
        }

        await Enter();
    }

    /// <summary>Clears the picked notification, which is named by its own id and not by the post's (CONTEXT.md).</summary>
    public async Task Dismiss()
    {
        if (Screen is not NotificationsScreen notifications || notifications.PickedNotification is not { } picked)
        {
            return;
        }

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Notifications.Dismiss(_profile, picked.Id, token)),
            eitherWay: () => _cache.Forget(DestinationKind.Notifications),
            ifStillHere: () =>
            {
                notifications.Forget([picked.Id]);
                Counted(DestinationKind.Notifications, notifications.Notifications.Count);

                Changed?.Invoke();
            });
    }

    /// <summary>
    ///     Asks before emptying the inbox. Unlike dismissing one, this takes away a list nobody has necessarily read
    ///     yet and nothing brings it back — so it is asked on the same terms <c>notification clear</c> asks it.
    /// </summary>
    public void AskToClear()
    {
        if (Screen is not NotificationsScreen notifications || notifications.Notifications.Count == 0)
        {
            return;
        }

        Asking = new Confirmation("Clear every notification? This cannot be undone.", Going: "clear");
        _confirming = Clear;

        Changed?.Invoke();
    }

    /// <summary>Accepts or turns away the picked follow request.</summary>
    public async Task AnswerRequest(bool accepted)
    {
        if (Screen is not FollowRequestsScreen requests || requests.PickedAccount is not { } picked)
        {
            return;
        }

        // By id, as the list reports it, because that is what answering one takes: an address would cost a lookup to
        // arrive back at the id already in hand (ADR-0012).
        await _enquiry.Put(
            ask => ask.Of(token => _ports.Accounts.Answer(_profile, picked.Id, accepted, token)),
            eitherWay: _ => _cache.Forget(DestinationKind.Requests),
            ifStillHere: _ =>
            {
                requests.Answered(picked.Id);
                Counted(DestinationKind.Requests, requests.Waiting.Count);

                Say(
                    accepted ? $"@{picked.Address} can follow you." : $"@{picked.Address} was turned away.",
                    isError: false);
            });
    }

    /// <summary>
    ///     Opens the picked conversation: the thread its last post is in, oldest first. Named by the conversation's
    ///     own id, which is not the id of any post in it (CONTEXT.md).
    /// </summary>
    /// <remarks>
    ///     Reading one does not mark it read (ADR-0013). A client that cleared the mark on the way past would make
    ///     "what have I not read" unanswerable for anything that looked afterwards, so <see cref="MarkRead" /> is what
    ///     takes it off and nothing else does.
    /// </remarks>
    public async Task OpenConversation()
    {
        if (Screen is not DirectMessagesScreen messages || messages.PickedConversation is not { } picked)
        {
            return;
        }

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Messages.Show(_profile, picked.Id, token)),
            ifStillHere: thread => Push(new ConversationScreen(thread)));
    }

    /// <summary>
    ///     Takes the unread mark off the conversation being read, or the one picked out on the list — the conversation
    ///     carries the mark, so the conversation's own id is what clears it.
    /// </summary>
    public async Task MarkRead()
    {
        if (Reading is not { } conversation)
        {
            return;
        }

        if (!conversation.Unread)
        {
            // A key that did nothing and said nothing reads as a shell that missed the press, and asking an instance
            // to clear a mark it does not have would spend a request to be told what is already on screen.
            Say("Already read.", isError: false);

            return;
        }

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Messages.MarkRead(_profile, conversation.Id, token)),
            eitherWay: _ => _cache.Forget(DestinationKind.Messages),
            ifStillHere: marked =>
            {
                Replace(marked);
                Say("Marked as read.", isError: false);
            });
    }

    /// <summary>Opens the account of whoever is asking to follow, so the question can be answered knowing who asked.</summary>
    public async Task OpenAsker()
    {
        if (Screen is FollowRequestsScreen { PickedAccount: { } picked })
        {
            await OpenAccount(AccountAddress.Parse(picked.Address));
        }
    }

    /// <summary>
    ///     Puts one of the three ties on the account being shown, or takes it off — whichever it does not already
    ///     have, which is why a tie is on or off rather than an act of its own (ADR-0012).
    /// </summary>
    /// <remarks>
    ///     Only the account screen offers these, and only it says so on its status row. The keys are capitals so that
    ///     a lower-case mark key cannot fire one by accident (<c>docs/tui-shell.md</c>).
    /// </remarks>
    public async Task Tie(AccountTie tie)
    {
        if (Screen is not AccountScreen account)
        {
            return;
        }

        var address = AccountAddress.Parse(account.Account.Address);
        var wanted = !account.Has(tie);

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Accounts.Set(_profile, address, tie, wanted, token)),
            eitherWay: stands =>
            {
                account.Stands(stands);

                // Home is the profile's own following, so a follow or a block changes what belongs on it — and a mute
                // changes what belongs on all of them.
                _cache.Forget(DestinationKind.Home);

                Say(Said(tie, wanted, stands), isError: false);
            });
    }

    /// <summary>Shows the current screen's keymap, which is itself a place in the stack.</summary>
    public void Help()
    {
        if (Screen is not HelpScreen)
        {
            Push(new HelpScreen(Screen));
        }
    }

    /// <summary>
    ///     Puts <paramref name="mark" /> on the picked post, or takes it off — whichever the post does not already
    ///     have, which is why a post carries the reader's own marks.
    /// </summary>
    public async Task Mark(PostMark mark)
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        var about = picked.Boosted ?? picked;

        if (mark == PostMark.Pin && !IsMine(about))
        {
            Say("Only your own posts can be pinned.", isError: true);

            return;
        }

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Engagement.Mark(_profile, about.Id, mark, !about.Marks.Has(mark), token)),
            eitherWay: marked => Replace(marked));
    }

    /// <summary>Opens an editor answering the picked post.</summary>
    public void Reply() => Compose(ComposeFor.Reply);

    /// <summary>Opens an editor for a new post.</summary>
    public void Compose() => Compose(ComposeFor.Post);

    /// <summary>Opens an editor on one of the profile's own posts.</summary>
    public void Edit() => Compose(ComposeFor.Edit);

    /// <summary>
    ///     Asks before taking a post down. The one thing here whose effect running something else does not undo, so
    ///     nothing is deleted until it has been said twice (story 43).
    /// </summary>
    public void AskToDelete()
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        var about = picked.Boosted ?? picked;

        if (!IsMine(about))
        {
            Say("Only your own posts can be deleted.", isError: true);

            return;
        }

        Asking = new Confirmation($"Delete post {about.Id}? This cannot be undone.");
        _confirming = () => Delete(about.Id);

        Changed?.Invoke();
    }

    /// <summary>Answers whatever the shell was waiting to be told again.</summary>
    public async Task Answer(bool agreed)
    {
        var confirmed = _confirming;

        Asking = null;
        _confirming = null;

        Changed?.Invoke();

        if (agreed && confirmed is not null)
        {
            await confirmed();
        }
    }

    /// <summary>Publishes, replies with, or saves whatever the compose screen is holding.</summary>
    public async Task Send()
    {
        if (Screen is not ComposeScreen compose)
        {
            return;
        }

        if (compose.IsEmpty)
        {
            Say("There is nothing written to send.", isError: true);

            return;
        }

        await _enquiry.Put(
            ask => compose.Purpose == ComposeFor.Edit
                ? ask.Of(token =>
                    _ports.Author.Edit(_profile, compose.About!.Id, new PostEdit { Text = compose.Text }, token))
                : ask.Of(token => _ports.Author.Publish(
                    _profile,
                    new PostDraft
                    {
                        Text = compose.Text,

                        // Silence rather than a visibility of the shell's choosing. A reply is answered as narrowly as
                        // the post it answers, and a post says nothing so that the account's own default on the
                        // instance decides — this shell has no visibility picker to have been told anything by.
                        InReplyTo = compose.Purpose == ComposeFor.Reply ? compose.About?.Id : null,
                    },
                    token)),
            eitherWay: written =>
            {
                // This client is what changed the timeline, so its age says nothing useful about it any more.
                _cache.Forget(Rail.Showing.Kind);

                _stack.RemoveAt(_stack.Count - 1);

                if (compose.Purpose == ComposeFor.Edit)
                {
                    Replace(written);
                }
                else if (compose.Purpose == ComposeFor.Reply && Screen is ConversationScreen conversation)
                {
                    // A conversation is read in the order it was said in, so what was just said belongs at the end of
                    // it — otherwise a reply written in the thread appears nowhere until the conversation is read
                    // again. It is the conversation's last word too, which is what the row it was opened from shows.
                    conversation.Said(written);
                    Replace(conversation.Conversation);
                }

                Say(compose.Purpose == ComposeFor.Edit ? "Saved." : "Sent.", isError: false);
            });
    }

    /// <summary>The nine, in the order the rail draws them.</summary>
    private static IReadOnlyList<Destination> Destinations(ActiveProfile profile, string? hashtag) =>
    [
        new(DestinationKind.Home, "Home", Timeline.Home),
        new(DestinationKind.Local, "Local", Timeline.Local),
        new(DestinationKind.Federated, "Federated", Timeline.Federated),
        new(
            DestinationKind.Hashtag,
            hashtag is null ? "Hashtag" : $"#{hashtag}",
            hashtag is null ? null : Timeline.Tag(hashtag)),
        new(DestinationKind.Notifications, "Notifications"),
        new(DestinationKind.Messages, "Direct messages"),
        new(DestinationKind.Requests, "Follow requests"),
        new(DestinationKind.Search, "Search"),
        new(DestinationKind.Profile, profile.Account is { } account ? $"@{account.Split('@')[0]}" : "Profile"),
    ];

    /// <summary>
    ///     Arriving at a destination, which is what moving the rail's selection means. Every destination that reads a
    ///     list goes through <see cref="Arrival" /> and is nothing here but the four things it says about itself; the
    ///     three arms below are the ones that read none (#100).
    /// </summary>
    /// <remarks>
    ///     Walking to a destination is arriving somewhere, so whatever was drilled into from the last one is left
    ///     behind: the stack is where you went from here, and this is a different here.
    /// </remarks>
    private async Task Go(Destination destination)
    {
        switch (destination)
        {
            // The profile's own account, which is one account rather than a list of anything — and arrived at by
            // replacing what is on the stack rather than pushing onto it, since arriving is not drilling in.
            case { Kind: DestinationKind.Profile }:
                _arrival.At(new FeedScreen(destination, []));

                if (_profile.Account is { } account)
                {
                    await OpenAccount(AccountAddress.Parse(account), replacing: true);
                }

                return;

            // A prompt, which asks the instance for nothing until something has been typed into it.
            case { Kind: DestinationKind.Search }:
                _arrival.At(new SearchScreen());

                return;

            // A rail entry for a hashtag nobody has named has nothing to ask about, so what stands here is the line
            // that would name one rather than an empty timeline.
            case { Kind: DestinationKind.Hashtag, Timeline: null }:
                _arrival.At(new NoticeScreen(
                    "hashtag",
                    "No hashtag is set for the rail.",
                    """Put hashtag = "cats" under [preferences] in your config file to keep one here."""));

                return;

            default:
                await _arrival.At(destination);

                return;
        }
    }

    /// <summary>Opens an account screen: who they are, their standing, and their posts.</summary>
    private Task OpenAccount(AccountAddress address, bool replacing = false) =>
        _enquiry.Put(
            ask => ReadAccount(ask, address),
            ifStillHere: found =>
            {
                var screen = new AccountScreen(found.Account, found.Posts);

                if (replacing)
                {
                    Reset(screen);
                }
                else
                {
                    Push(screen);
                }
            });

    /// <summary>What an account screen is read with: who they are, and what they have posted.</summary>
    /// <remarks>
    ///     Two calls under one enquiry, so it is checked once at the end rather than after each: what matters is
    ///     whether the reader is still where they were when they asked, not how far the answer got.
    ///     <para>
    ///         Said here rather than at each of the two places that read an account — opening one, and asking it for
    ///         what is there now — so that a refresh is the same pair of calls the screen was opened by rather than a
    ///         second opinion about what an account screen is made of (#84).
    ///     </para>
    /// </remarks>
    private async Task<(Account Account, IReadOnlyList<Post> Posts)> ReadAccount(Enquiry.Ask ask, AccountAddress address)
    {
        var account = await ask.Of(token => _ports.Accounts.Show(_profile, address, token));
        var posts = await ask.Of(token =>
            _ports.Timelines.Read(_profile, Timeline.By(address), Arrival.PostsWanted, token));

        return (Account: account, Posts: posts.Items);
    }

    /// <summary>
    ///     Asking a destination for what is there now, which is the arrival it already arrives by with what it last
    ///     held taken away first — one refresh for all seven of them (#84).
    /// </summary>
    /// <remarks>
    ///     The destination is the rail's own rather than one read off the screen, and the two cannot differ here: only
    ///     a destination's screen answers to <c>g</c>, and a destination's screen is only ever the bottom of the stack
    ///     — anything drilled into from one is a screen of some other kind, which is refreshed by the two below.
    /// </remarks>
    private Task RefreshDestination(Place place)
    {
        _cache.Forget(Rail.Showing.Kind);

        return _arrival.Again(Rail.Showing, place);
    }

    /// <summary>
    ///     The same for the post screen, which no arrival reaches: the <c>Replies</c> call <see cref="Enter" /> ran to
    ///     open it, about the same post it is already about.
    /// </summary>
    private Task RefreshPost(PostScreen showing, Place place) =>
        _enquiry.Put(
            ask => ReadReplies(ask, showing.Post),
            ifStillHere: replies => Freshened(showing, new PostScreen(showing.Post, replies), place));

    /// <summary>
    ///     What has been said in answer to a post — asked about the post itself where what is in hand is a boost of
    ///     it, since a boost is the same post as far as its answers go.
    /// </summary>
    /// <remarks>
    ///     Said here rather than at both places that ask, for the reason <see cref="ReadAccount" /> gives: a refresh
    ///     is the same call the screen was opened by rather than a second opinion about what a post screen holds
    ///     (#84).
    /// </remarks>
    private Task<IReadOnlyList<Post>> ReadReplies(Enquiry.Ask ask, Post post)
    {
        var about = post.Boosted ?? post;

        return ask.Of(token => _ports.Engagement.Replies(_profile, about.Id, token));
    }

    /// <summary>And for the account screen, which is both of the calls that opened it.</summary>
    private Task RefreshAccount(AccountScreen showing, Place place) =>
        _enquiry.Put(
            ask => ReadAccount(ask, AccountAddress.Parse(showing.Account.Address)),
            ifStillHere: found =>
                Freshened(showing, new AccountScreen(found.Account, found.Posts), place));

    /// <summary>
    ///     Puts <paramref name="fresh" /> in place of the screen it is a fresher copy of, with the reader put back
    ///     where they were standing on it.
    /// </summary>
    /// <remarks>
    ///     Neither of the two screens refreshed this way is reached through an arrival, so neither is overtaken by
    ///     one: an <see cref="Enquiry" /> answers about the destination the reader is on, and drilling in and walking
    ///     back out again happen inside one. So the top of the stack is rechecked before anything is put on it — the
    ///     same idiom <see cref="Find" /> and <see cref="OpenResult" /> use, and what stops a screen the reader has
    ///     pressed <c>esc</c> out of landing on top of the one they walked back to.
    ///     <para>
    ///         In place of the top rather than pushed or reset: a refresh redraws where somebody is standing, so the
    ///         way they got there is still under them and <c>esc</c> still walks back out of it. A different screen
    ///         object rather than the same one changed, which is what puts the scroll offset back to nought — the
    ///         offset starts again whenever the screen is replaced (<c>docs/tui-shell.md</c>).
    ///     </para>
    /// </remarks>
    private void Freshened(Screen showing, Screen fresh, Place place)
    {
        if (!ReferenceEquals(Screen, showing))
        {
            return;
        }

        fresh.Resume(place);

        _stack[^1] = fresh;

        // Gone with the screen it was said over, the same as at a push or an arrival: what a reader was told about the
        // list they were looking at is not about the one in front of them now.
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>
    ///     Opens the account the picked mention names, off the post itself rather than out of a fetch: an instance
    ///     sends everyone a post names along with the post, so the account is already in hand (#85).
    /// </summary>
    /// <remarks>
    ///     A handle the post never named opens nothing and says so. Asking an instance to look one up instead would
    ///     spend a request on a guess — a bare <c>@maria</c> means nothing without an instance to put after it, and
    ///     guessing this profile's own would open somebody else under somebody's name.
    /// </remarks>
    private Task OpenMention()
    {
        // Well-formed as well as named, because a handle an instance sent is not something a reader can do anything
        // about, and one this client cannot look up is as good as one it was never given.
        if (Screen.Mentioned is not { } handle || !AccountAddress.IsWellFormed(handle))
        {
            Say(MentionUnresolved, isError: true);

            return Task.CompletedTask;
        }

        return OpenAccount(AccountAddress.Parse(handle));
    }

    /// <summary>
    ///     Sends <paramref name="written" /> to the platform's browser — the one thing this shell does that leaves the
    ///     terminal (#85).
    /// </summary>
    /// <remarks>
    ///     Two refusals, told apart because a reader can do something about one of them: an address this client will
    ///     not hand to a machine is the post's doing, and no browser to hand it to is the machine's. What is painted
    ///     as an address is matched by pattern (<c>BodyText</c>), so what arrives here is not necessarily an address
    ///     at all — which is the same refusal as a scheme nothing should hand to a shell, and is why the check is
    ///     <see cref="BrowserLaunch" />'s rather than a guess made here.
    /// </remarks>
    private void OpenAddress(string written)
    {
        if (BrowserLaunch.Address(written) is not { } address)
        {
            Say(AddressRefused, isError: true);

            return;
        }

        if (!_browser.TryOpen(address))
        {
            Say(NoBrowser, isError: true);
        }
    }

    /// <summary>Opens a hashtag's timeline as a screen on the stack, which is what a search result for one does.</summary>
    /// <remarks>
    ///     A screen pushed onto the stack rather than a destination arrived at, so it goes nowhere near
    ///     <see cref="Arrival" />: nothing here is overtaken, cached, reset or counted — but what an empty tag is told
    ///     is the same sentence the rail's own timelines are told, and is said in the one place.
    /// </remarks>
    private Task OpenTag(string name) =>
        _enquiry.Put(
            ask => ask.Of(token => _ports.Timelines.Read(_profile, Timeline.Tag(name), Arrival.PostsWanted, token)),
            ifStillHere: posts =>
            {
                // A destination of its own rather than the rail's, so that the breadcrumb says which tag this is
                // without the rail's own hashtag entry changing under a reader who did not ask it to.
                var tag = Timeline.Tag(name);
                var showing = new Destination(DestinationKind.Hashtag, $"#{name}", tag);

                Push(new FeedScreen(
                    showing,
                    posts.Items,
                    Arrival.Emptiness(posts.Items.Count, Arrival.NothingOn(tag), tag.Description, posts.StoppedBy)));
            });

    /// <summary>Empties the inbox, once it has been said twice.</summary>
    private Task Clear() =>
        _enquiry.Put(
            ask => ask.Of(token => _ports.Notifications.Clear(_profile, token)),
            eitherWay: () =>
            {
                _cache.Forget(DestinationKind.Notifications);

                if (Screen is NotificationsScreen notifications)
                {
                    notifications.Forget(notifications.Notifications.Select(notification => notification.Id).ToList());
                }

                Counted(DestinationKind.Notifications, 0);
                Say("Cleared.", isError: false);
            });

    private Task Delete(string postId) =>
        _enquiry.Put(
            ask => ask.Of(token => _ports.Author.Delete(_profile, postId, token)),
            eitherWay: () =>
            {
                _cache.Forget(Rail.Showing.Kind);

                // Walked out of first, because a post screen showing a post that is no longer there is a screen about
                // nothing.
                if (Screen is PostScreen post && (post.Post.Boosted ?? post.Post).Id == postId && _stack.Count > 1)
                {
                    _stack.RemoveAt(_stack.Count - 1);
                }

                foreach (var screen in _stack)
                {
                    screen.Remove(postId);
                }

                Say("Deleted.", isError: false);
            });

    /// <summary>Reads the counts the rail carries, none of which is worth failing the shell over.</summary>
    private async Task Counts()
    {
        await Count(
            DestinationKind.Notifications,
            async token => (await _ports.Notifications.Read(_profile, Arrival.CountedAtMost, token)).Items.Count);

        await Count(
            DestinationKind.Messages,
            async token => (await _ports.Messages.List(_profile, Arrival.CountedAtMost, token))
                .Items.Count(conversation => conversation.Unread));

        await Count(
            DestinationKind.Requests,
            async token => (await _ports.Accounts.PendingRequests(_profile, Arrival.CountedAtMost, token)).Items.Count);
    }

    private async Task Count(DestinationKind kind, Func<CancellationToken, Task<int>> read)
    {
        try
        {
            var unread = await read(CancellationToken.None);

            Apply(() => Counted(kind, unread));
        }
        catch (WoolyException)
        {
            // A count that could not be read is drawn as no count. It is the least of what is on screen, and a shell
            // that refused to open because a badge was unavailable would be trading the whole thing for a number.
        }
    }

    /// <summary>
    ///     Puts a count on the rail. Said in one place, because the badge is written from three: the read that opens
    ///     the shell, arriving at the destination itself, and clearing something off it — and a badge that disagreed
    ///     with the list under it would be the shell arguing with itself.
    /// </summary>
    private void Counted(DestinationKind kind, int unread) =>
        Rail.Update(Rail.Destinations.First(destination => destination.Kind == kind) with { Unread = unread });

    /// <summary>What a tie that has just gone on or come off is worth saying about, in this project's words.</summary>
    /// <remarks>
    ///     A follow is the one that may not have gone through as asked: following a locked account leaves a request
    ///     behind rather than a follow, and the instance's own answer is the only thing that says which happened
    ///     (CONTEXT.md).
    /// </remarks>
    private static string Said(AccountTie tie, bool wanted, Account account) => (tie, wanted) switch
    {
        (AccountTie.Follow, true) when account.Standing?.IsFollowWaiting == true => $"Asked to follow @{account.Address}.",
        (AccountTie.Follow, true) => $"Following @{account.Address}.",
        (AccountTie.Follow, false) => $"No longer following @{account.Address}.",
        (AccountTie.Block, true) => $"Blocked @{account.Address}.",
        (AccountTie.Block, false) => $"Unblocked @{account.Address}.",
        (AccountTie.Mute, true) => $"Muted @{account.Address}.",
        _ => $"Unmuted @{account.Address}.",
    };

    /// <summary>
    ///     An arm of <see cref="Press" /> that reaches no instance, as a task — so that the table above is one shape
    ///     all the way down rather than a mix of two.
    /// </summary>
    private static Task AtOnce(Action work)
    {
        work();

        return Task.CompletedTask;
    }

    private void Compose(ComposeFor purpose)
    {
        var about = Screen.Picked?.Boosted ?? Screen.Picked;

        // A mention picked out is an account the reader walked to, so c writes to them — a fresh post rather than a
        // reply, since what they picked is somebody named in the post rather than the post itself (#85).
        if (purpose == ComposeFor.Post && Screen.MentionedAs is { } handle)
        {
            Push(new ComposeScreen(purpose, addressing: $"@{handle}"));

            return;
        }

        switch (purpose)
        {
            case ComposeFor.Reply or ComposeFor.Edit when about is null:
                return;
            case ComposeFor.Edit when !IsMine(about!):
                Say("Only your own posts can be edited.", isError: true);

                return;
        }

        Push(new ComposeScreen(
            purpose,
            purpose == ComposeFor.Post ? null : about,
            purpose == ComposeFor.Reply ? Addressed(about!) : null,
            aboutIsMine: purpose == ComposeFor.Reply && IsMine(about!)));
    }

    /// <summary>
    ///     What a reply has to be written to, or <see langword="null" /> where it is nobody's business but the
    ///     reader's. Mastodon delivers a direct post to the accounts its text mentions and to nobody else (ADR-0013),
    ///     so a direct reply that named nobody would reach nobody — the mention is what makes it a message rather than
    ///     a note to self, which is why <c>dm send</c> writes one too.
    /// </summary>
    /// <remarks>
    ///     Only a direct message is addressed. Putting a mention on a public reply would be this client writing words
    ///     nobody asked it to, and the instance delivers that one to the thread without any help.
    ///     <para>
    ///         Who it goes to is the conversation where there is one, rather than whoever spoke last: a thread with
    ///         three accounts in it answered to only one of them is a reply that dropped the rest of the conversation.
    ///     </para>
    /// </remarks>
    private string? Addressed(Post about)
    {
        if (about.Visibility != PostVisibility.Direct)
        {
            return null;
        }

        // An instance says who a conversation is with rather than who is having it, so the profile's own account is
        // already not among them. Answering a direct message read anywhere else names whoever wrote it.
        IReadOnlyList<string> with = Screen switch
        {
            ConversationScreen conversation => conversation.Conversation.With,
            _ => IsMine(about) ? [] : [about.Account],
        };

        // An address this client cannot make sense of is left out rather than thrown over the reply, since a handle
        // an instance sent is not something the reader can do anything about. What is left is in the editor in front
        // of them, so a mention that is missing is missing where they can see it and type it themselves.
        var accounts = with.Where(AccountAddress.IsWellFormed).Select(AccountAddress.Parse).ToList();

        return accounts.Count == 0 ? null : DirectMessage.To(accounts, string.Empty);
    }

    /// <summary>
    ///     Whether a post is the profile's own, which is what settles whether pinning, editing and deleting are
    ///     offered. Compared on the address, because that is the one name for an account that means the same thing on
    ///     two instances.
    /// </summary>
    private bool IsMine(Post post) =>
        _profile.Account is { } account && string.Equals(post.Account, account, StringComparison.OrdinalIgnoreCase);

    private void Push(Screen screen)
    {
        _stack.Add(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Puts the stack back to one screen, which is what arriving at a destination does.</summary>
    private void Reset(Screen screen)
    {
        _stack.Clear();
        _stack.Add(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>
    ///     The same, for a conversation that has just changed — marked read, or spoken in. The list and the thread
    ///     opened from it are on the stack together, so a row that still said <c>unread</c> under a thread just marked,
    ///     or still showed the message before the one just sent, would be the shell arguing with itself.
    /// </summary>
    /// <remarks>
    ///     The badge goes with it, because a count and the list under it are one fact (<c>docs/tui-shell.md</c>).
    /// </remarks>
    private void Replace(Conversation conversation)
    {
        foreach (var screen in _stack)
        {
            switch (screen)
            {
                case DirectMessagesScreen listed:
                    listed.Marked(conversation);
                    Counted(DestinationKind.Messages, listed.Unread);

                    break;

                case ConversationScreen reading when reading.Conversation.Id == conversation.Id:
                    reading.Marked(conversation);

                    break;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Puts a post that has just changed in place of the copy every screen in the stack is holding.</summary>
    private void Replace(Post post)
    {
        foreach (var screen in _stack)
        {
            screen.Replace(post);
        }

        _cache.Forget(Rail.Showing.Kind);

        Changed?.Invoke();
    }

    private void Say(string? notice, bool isError)
    {
        Notice = notice;
        NoticeIsError = isError;

        Changed?.Invoke();
    }

    private void Apply(Action work) => _host.OnUiThread(work);
}
