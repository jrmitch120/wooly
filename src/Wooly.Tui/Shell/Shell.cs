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
using Wooly.Tui.Rendering;
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
///         rate-limit wait, the failure notice and the stale-answer rule the same at all of them rather than copied at
///         each — and every screen that is read is brought up from its <see cref="Subject" /> by
///         <see cref="Arrival" />,
///         so none of their reads is here (#233). The counts the rail carries are the exception, and read their
///         ports directly: nobody is waiting on a badge, so one that could not be read is drawn as no count rather
///         than counted down over.
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
    ///     What bringing a screen up from its subject means, which is the same steps at every screen that is read —
    ///     arrived at, drilled into or refreshed (#100, #233).
    /// </summary>
    private readonly Arrival _arrival;

    /// <summary>
    ///     Where an address goes. The one thing this shell does that leaves the terminal, and deliberately not one of
    ///     <see cref="ShellPorts" />: those are what the shell reaches an <em>instance</em> through, and a browser is
    ///     not on one (ADR-0014, #85).
    /// </summary>
    private readonly IWebBrowser _browser;

    /// <summary>What each cached subject last held: the rail's destinations, and follow lists held whole.</summary>
    private readonly SubjectCache _cache;

    /// <summary>Everything this reaches an instance through, and the one place the stale-answer rule is stated.</summary>
    private readonly Enquiry _enquiry;

    private readonly IShellHost _host;
    private readonly ActiveProfile _profile;
    private readonly ShellPorts _ports;
    private readonly List<Screen> _stack = [];

    /// <summary>What a screen can reach of this shell while it answers a verb of its own (#232).</summary>
    private readonly Reach _reach;

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
        _cache = new SubjectCache(clock, timing.CacheFor);

        // Asked from whatever is on top, which is the whole of the stale-answer rule: an answer lands only while the
        // screen it was asked from is still in front of the reader.
        _enquiry = new Enquiry(host, clock, timing.CountdownStep, timing.MarkStep, () => Screen);
        _enquiry.Said += Say;
        _enquiry.Changed += () => Changed?.Invoke();
        _enquiry.Ticked += () => Ticked?.Invoke();

        // An arrival settles what a subject is on screen and what its badge says; putting either there is this
        // shell's own business, since the stack and the rail are its.
        _arrival = new Arrival(profile, ports, _enquiry, _cache);
        _arrival.Arrives += Reset;
        _arrival.Drills += Push;
        _arrival.Refreshes += Freshened;
        _arrival.Filled += () => Say(null, isError: false);
        _arrival.Counts += Counted;

        Rail = new Rail(Destinations(profile, hashtag), host, timing.Settle);
        Rail.Selected += destination => _ = _arrival.At(destination);
        Rail.Changed += () => Changed?.Invoke();

        _stack.Add(new FeedScreen(Rail.Showing, []));

        _reach = new Reach(
            profile,
            ports,
            _enquiry,
            _arrival,
            _cache,
            say: Say,
            confirm: Confirm,
            changed: () => Changed?.Invoke(),
            count: Counted,
            stands: Stands);
    }

    /// <summary>Raised whenever anything on screen has changed. Always on the drawing thread.</summary>
    public event Action? Changed;

    /// <summary>
    ///     Raised when the breadcrumb's fetch mark has gained a dot and nothing else on screen has changed — so that
    ///     what is redrawn for it is the one row it is on rather than everything <see cref="Changed" /> redraws.
    /// </summary>
    public event Action? Ticked;

    /// <summary>The rail: the ten destinations, the cursor, and the selection.</summary>
    public Rail Rail { get; }

    /// <summary>The screen on top of the stack, which is what the content region is showing.</summary>
    public Screen Screen => _stack[^1];

    /// <summary>How deep the drill is, where one is a destination with nothing opened from it.</summary>
    public int Depth => _stack.Count;

    /// <summary>What each screen in the stack is called, outermost first — what the breadcrumb row is drawn from.</summary>
    public IReadOnlyList<string> Crumbs => [.. _stack.Select(screen => screen.Crumb)];

    /// <summary>
    ///     Where you are, as one line: <c>Home › Post by @ben › @ben@hachyderm.io</c>. The trail as a reader reads it
    ///     off the row, which is what the shell is asked where it is — the row itself is drawn from
    ///     <see cref="Crumbs" />, a crumb at a time, since each takes a role of its own (#216).
    /// </summary>
    public string Breadcrumb => string.Join(ChromeLines.Separator, Crumbs);

    /// <summary>Whether a fetch is in flight, which the breadcrumb says once and the rail never does.</summary>
    public bool Fetching => _enquiry.Fetching;

    /// <summary>
    ///     How many dots the breadcrumb's fetch mark has on it — none until a fetch has been in flight for a whole
    ///     tick. What the mark draws, where <see cref="Fetching" /> is what the shell's own guards ask.
    /// </summary>
    public int Dots => _enquiry.Dots;

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
        await _arrival.At(Rail.Showing);
        await Counts();
    }

    /// <summary>
    ///     A rail keypress. The cursor moves at once; the selection — and the fetch — follow when the pressing stops.
    /// </summary>
    public void Step(int by) => Rail.Step(by);

    /// <summary>
    ///     Carries out what a key meant, once <see cref="Keymap" /> has said what that is. The frame's verbs and the
    ///     ones that act on the picked post of any screen are public here in their own right, so this is a table of
    ///     one-line arms rather than anywhere a decision is made; every other verb is screen-local, and the screen
    ///     carries it out itself (<see cref="Screen.Answer" />, #232).
    /// </summary>
    /// <remarks>
    ///     Which screen the reader is on has already been accounted for: the collisions the contract allows —
    ///     <c>d</c> dismissing a notification and deleting a post — are settled in the keymap, so nothing here has to
    ///     name a screen to know which of them it is. Nor does a screen answering its own: the keymap only ever sends
    ///     a screen a verb that means something there, or one bound everywhere that the screen is free to ignore.
    ///     <para>
    ///         The verbs this does not carry are the ones that need a terminal, and they are taken by
    ///         <c>ShellWindow</c> before they ever reach here: quitting, the movements that walk the page rather than
    ///         the list, and the send that has to take the editor widget's text first.
    ///     </para>
    ///     <para>
    ///         Nothing is awaited. A verb that reaches an instance is put through <see cref="Enquiry" />, which lands
    ///         its answer on the drawing thread — so the press is spent the moment it is sent, and a caller that wants
    ///         to wait for what came back calls the verb itself.
    ///     </para>
    /// </remarks>
    /// <param name="answer">
    ///     Which poll answer <see cref="Verb.Toggle" /> addresses (<see cref="Keymap.Answer" />), and nothing to any
    ///     other verb.
    /// </param>
    /// <returns>
    ///     Whether the press was used. Three verbs can answer no — the two that walk a reference and the one that
    ///     toggles a poll answer — because there may be nothing on the picked post for them to act on, and an unused
    ///     key falls back through the window to whatever else wants it: the compose editor's own arrows above all
    ///     (#83, #87). That is settled here rather than in the keymap because the screen is the only thing that knows
    ///     what is on the post, and asking it in two places is how two places come to disagree.
    /// </returns>
    public bool Do(Verb verb, int? answer) => verb switch
    {
        Verb.NextReference => WalkReference(1),
        Verb.PreviousReference => WalkReference(-1),
        Verb.Toggle => answer is { } option && Toggle(option),

        Verb.Back => Ran(Back),
        Verb.Help => Ran(Help),
        Verb.Search => Ran(Search),
        Verb.NextDestination => Ran(() => Step(1)),
        Verb.PreviousDestination => Ran(() => Step(-1)),
        Verb.OpenPost => Ran(Enter),
        Verb.OpenAuthor => Ran(OpenAuthor),
        Verb.OpenReference => Ran(OpenReference),
        Verb.Compose => Ran(() => Compose()),
        Verb.Reply => Ran(Reply),
        Verb.Edit => Ran(Edit),
        Verb.Boost => Ran(() => Mark(PostMark.Boost)),
        Verb.Favorite => Ran(() => Mark(PostMark.Favorite)),
        Verb.Pin => Ran(() => Mark(PostMark.Pin)),
        Verb.Delete => Ran(AskToDelete),
        Verb.Reveal => Ran(Reveal),
        Verb.Vote => Ran(AskToVote),
        Verb.Refresh => Ran(Refresh),
        Verb.MarkRead => Ran(MarkRead),
        Verb.WriteWarning => Ran(WriteWarning),

        // Nothing, and the terminal's own — which the window has already taken, and which no screen answers either.
        Verb.None => false,
        _ when verb.NeedsATerminal() => false,

        // Everything else is the screen's own, and the screen is what carries it out.
        _ => Ran(() => Screen.Answer(verb, _reach)),
    };

    /// <summary>Moves what is picked out on the current screen.</summary>
    public void Move(int by)
    {
        Screen.Move(by);

        Paging();

        // The remark goes with the post it was said over, for the reason <see cref="Walk" /> gives.
        Say(null, isError: false);
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

        Paging();

        // A remark is about the post it was said over, and the reader has walked off it — so it goes with them, the
        // same way esc takes it off on the way out of a screen. It is not a small thing to leave standing: the status
        // row holds either a notice or the keymap, never both, so a stale one is every key the screen answers to,
        // hidden until the reader happens to go somewhere.
        Say(null, isError: false);
    }

    /// <summary>
    ///     What <c>[</c> and <c>]</c> do once the rows have said where they land: pick out the first thing of the run
    ///     jumped to (#166).
    /// </summary>
    /// <remarks>
    ///     Which thing that is belongs to the view, the way <see cref="Walk" />'s reclaim does: only a view knows how
    ///     tall the terminal is and where the arrows have left the scroll, and both answers are read off the very rows
    ///     it is showing. Never called at all where the jump had nowhere to go, so a clamp at either end costs the
    ///     reader nothing here — not even the notice they were reading.
    /// </remarks>
    public void Section(int at)
    {
        Screen.Pick(at);

        // The remark goes with the thing it was said over, for the reason Walk gives.
        Say(null, isError: false);
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

    /// <summary>Shows what the picked post is hiding — its warned text, its sensitive attachments, or both.</summary>
    public void Reveal()
    {
        if (Screen.Reveal())
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    ///     What <c>1</c>-<c>9</c> and <c>0</c> do: toggle the <paramref name="option" />th answer of the picked post's
    ///     poll, counted from zero. Nothing is sent — the toggle is local until <c>v</c> casts it (#87).
    /// </summary>
    /// <returns>
    ///     Whether there was an answer there to toggle, which is what settles whether the key was used: a digit on a
    ///     post with no poll on it leaves the key to whatever else wants it.
    /// </returns>
    public bool Toggle(int option)
    {
        if (!Screen.Toggle(option))
        {
            return false;
        }

        // The status row holds one thing at a time and a notice wins it, so a remark left standing is the whole keymap
        // gone — including the v the reader is being told to press next. A remark is about what had just happened, and
        // what had just happened is over (#87 follow-up).
        Say(null, isError: false);

        return true;
    }

    /// <summary>Opens the picked post, with what has been said in answer to it.</summary>
    /// <remarks>
    ///     What it opens is the screen's <see cref="Screen.Opens" /> rather than what is picked out, which are the same
    ///     post everywhere except inside a post: there, the post picked out at the top is the one already on screen
    ///     (#48).
    /// </remarks>
    public Task Enter() =>
        Screen.Opens is { } opening ? _arrival.Open(new Subject.Thread(opening)) : Task.CompletedTask;

    /// <summary>
    ///     Asks for what is there now: evicts what the screen's subject last held, puts the same question that brought
    ///     it up, and opens the answer at the top so that what has just arrived is what the reader is looking at
    ///     (<c>docs/tui-shell.md</c>, #84).
    /// </summary>
    /// <remarks>
    ///     Only where the screen says it answers to <c>g</c>, which is the nine the contract names. A second press
    ///     while anything is already in flight does nothing at all — no second question, and no in-flight UI beyond
    ///     the <c>fetching</c> mark the breadcrumb already carries.
    ///     <para>
    ///         Every one of them goes back through <see cref="Arrival" />, which is one refresh for all of them: what
    ///         to evict, what to read, what it becomes and what it counts are all things the screen's subject already
    ///         says (#100, #233). A fresher copy stands in place of the screen showing, where that screen is still in
    ///         front when it lands.
    ///     </para>
    ///     <para>
    ///         Nothing about where the reader was standing is carried over, and that is the whole point of the key: a
    ///         refresh is somebody asking to see what is new, and what is new is at the top. Keeping their place would
    ///         leave the new posts above the page, which is to say fetched and invisible.
    ///     </para>
    ///     <para>
    ///         Answers with nothing, and deliberately. Everything a refresh does happens on the drawing thread, in the
    ///         callback <see cref="Enquiry.Put" /> hands to the host — which is queued there and run by the main loop
    ///         <em>after</em> this task has already completed. A flag set inside that callback and returned from here
    ///         would be read before it was ever written. What the view needs to know is that a screen was replaced,
    ///         and it already learns that from <see cref="Changed" /> — in the right order, on the right thread.
    ///     </para>
    /// </remarks>
    public Task Refresh()
    {
        if (!Screen.Refreshes || Screen.Subject is not { } subject || Fetching)
        {
            return Task.CompletedTask;
        }

        return _arrival.Again(subject);
    }

    /// <summary>
    ///     Opens whatever the picked reference points at, which is four different things: a hashtag's timeline, the
    ///     account a mention names, an address in the platform's own browser, or a <c>Video</c>/<c>Animation</c>/
    ///     <c>Audio</c>/<c>Unknown</c> attachment's own address and a link preview's alike — in that same browser, the
    ///     exact way a picked link already reaches it (#85, ADR-0017, ADR-0018).
    /// </summary>
    /// <remarks>
    ///     Which of the four it is, is the role the reference draws in — one vocabulary rather than two, which is the
    ///     bargain <c>Reference</c> already struck: a second enum saying the same thing would be a second place to add
    ///     a kind to.
    ///     <para>
    ///         Only the two address arms leave the terminal, and each is the only thing in its arm that pushes
    ///         nothing: the reader has been sent somewhere this client does not draw, so there is nothing to come back
    ///         from with <c>esc</c>.
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
                return _arrival.Open(new Subject.Tag(reference.Text.TrimStart('#')));

            case Role.Mention:
                return OpenMention();

            case Role.Link:
            case Role.Media:
                // Media is an attachment's own address (AttachmentReferences) or a link preview's
                // (LinkPreviewReference), carried already-well-formed off the wire rather than matched by pattern out
                // of prose — but it goes through the very same call a Link reference does, so a refusal reads
                // identically whichever kind of reference it was refused on.
                OpenAddress(reference.Text);

                return Task.CompletedTask;

            default:
                // Named rather than left as the fall-through, because the fall-through here is the one path that
                // leaves the machine: a fifth kind of reference added and forgotten about must open nothing rather
                // than be handed to a browser as an address.
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

        var author = AccountAddress.Parse((picked.Boosted ?? picked).Account);

        await _arrival.Open(new Subject.Account(author, WithReplies: false));
    }

    /// <summary>Walks back up one level of the stack. Never quits, and never leaves the shell with nothing on it.</summary>
    public void Back()
    {
        if (Asking is not null)
        {
            // Escaping out of a confirmation is answering it, and the answer is no.
            Asking = null;

            Changed?.Invoke();

            return;
        }

        // A reference pick and an uncast vote are each a level of their own inside the picked post, so esc is up one
        // level of whichever kind is open: the first press lets what is inside go and the next pops the screen
        // (docs/tui-shell.md, #83, #87). Both at once, because both are the same half-finished sentence about the same
        // post — leaving one of them standing would make the next esc do nothing anybody asked for.
        if (Screen.ClearReference() | Screen.ClearChoices())
        {
            Changed?.Invoke();

            return;
        }

        // And a filter is a level of its own in the same sense: the first esc puts the whole list back and the next
        // one leaves the screen (#180).
        if (Screen.ClearFilter())
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

    /// <summary>
    ///     Puts a letter into whatever is being typed into: the search prompt, or a compose screen's content warning
    ///     while <c>ctrl-w</c> has it. Never a post's own text, which is typed into the editor widget itself.
    /// </summary>
    /// <remarks>
    ///     Which screen it is going to is the screen's own answer (<see cref="Screen.IsTyping" />) rather than a type
    ///     matched here, so a third screen that takes letters costs this nothing.
    /// </remarks>
    public void Type(char letter)
    {
        if (!Screen.IsTyping)
        {
            return;
        }

        Screen.Type(letter);
        Changed?.Invoke();
    }

    /// <summary>Takes the last letter back out of it.</summary>
    public void Backspace()
    {
        if (!Screen.IsTyping)
        {
            return;
        }

        Screen.Backspace();
        Changed?.Invoke();
    }

    /// <summary>
    ///     <c>ctrl-w</c>: moves the typing between the post being written and the warning over it (#123), on all three
    ///     compose screens since #140 gave an edit a field of its own. Nothing at all anywhere else.
    /// </summary>
    public void WriteWarning()
    {
        if (Screen is not ComposeScreen compose)
        {
            return;
        }

        compose.WriteTheWarning();
        Changed?.Invoke();
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
            eitherWay: _ => _cache.Forget(new Subject.Destination(DestinationKind.Messages)),
            ifStillHere: marked =>
            {
                Replace(marked);
                Say("Marked as read.", isError: false);
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

        Confirm(new Confirmation("Delete this post?", () => Delete(about.Id)));
    }

    /// <summary>
    ///     Asks before casting what the digits have toggled. Story 43's rule, and this qualifies for it more than a
    ///     delete does: an instance refuses a second vote outright rather than replacing the first, so a vote cast by
    ///     accident is not something the reader can put right by voting again (<c>docs/tui-shell.md</c>, #87).
    /// </summary>
    public void AskToVote()
    {
        // The poll rather than the post, because it is the poll that settles whether this key means anything — and a
        // post carrying one always has a post to vote on.
        if (Screen.Poll is not { } poll || Screen.Picked is not { } picked)
        {
            return;
        }

        if (!poll.TakesAVote)
        {
            // Said rather than passed over in silence, the same way m answers on a conversation already read: the
            // poll is on screen and the key is on the keyboard, so a press that did nothing at all would read as a
            // shell that missed it. Which of the two reasons it is, is the part worth saying.
            Say(poll.Closed ? "That poll has closed." : "You have already voted in this poll.", isError: false);

            return;
        }

        if (Screen.Chosen.Count == 0)
        {
            // The key is announced wherever there is a poll, so it has to answer wherever it is announced: a v that
            // did nothing and said nothing reads as a shell that missed the press.
            Say("Choose an answer first, with 1-9 or 0.", isError: false);

            return;
        }

        var screen = Screen;
        var about = picked.Boosted ?? picked;

        // Taken now rather than read back when the question is answered: what is being agreed to is what was on the
        // ballot when it was put, and in the order the poll lists its answers rather than the order they were pressed.
        var choices = Screen.Chosen.Order().ToList();

        Confirm(new Confirmation(VotingFor(choices), () => Cast(screen, about, choices), Going: "vote"));
    }

    /// <summary>Answers whatever the shell was waiting to be told again.</summary>
    public async Task Answer(bool agreed)
    {
        var confirmed = Asking;

        Asking = null;

        Changed?.Invoke();

        if (agreed && confirmed is not null)
        {
            await confirmed.Agreed();
        }
    }

    /// <summary>Publishes, replies with, or saves whatever the compose screen is holding.</summary>
    /// <remarks>
    ///     <em>Whatever it is holding</em> is the screen's own answer (<see cref="ComposeScreen.Outgoing" />, #146),
    ///     down to which of the two things the warning field means. What is left here is the part that needs a port
    ///     and a stack: the call, the pop, and what the reader is told — and the two arms below are the two writes a
    ///     post author has rather than two ways of putting a post together.
    ///     <para>
    ///         Each arm says the whole of what its own landing means, notice included, rather than matching once here
    ///         and asking <see cref="ComposeScreen.Purpose" /> the same question again afterwards. The one thing left
    ///         that reads the purpose is a different question: a reply written inside a conversation is put at the end
    ///         of it, which is about where the reader is standing rather than about what went out.
    ///     </para>
    /// </remarks>
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

        switch (compose.Outgoing)
        {
            case Outgoing.Saving(var postId, var edit):
                await _enquiry.Put(
                    ask => ask.Of(token => _ports.Author.Edit(_profile, postId, edit, token)),
                    eitherWay: saved =>
                    {
                        Popped();
                        Replace(saved);
                        Say("Saved.", isError: false);
                    });

                break;

            case Outgoing.Publishing(var draft):
                await _enquiry.Put(
                    ask => ask.Of(token => _ports.Author.Publish(_profile, draft, token)),
                    eitherWay: published =>
                    {
                        Popped();

                        if (compose.Purpose == ComposeFor.Reply && Screen is ConversationScreen conversation)
                        {
                            // A conversation is read in the order it was said in, so what was just said belongs at the
                            // end of it — otherwise a reply written in the thread appears nowhere until the
                            // conversation is read again. It is the conversation's last word too, which is what the
                            // row it was opened from shows.
                            conversation.Said(published);
                            Replace(conversation.Conversation);
                        }

                        Say("Sent.", isError: false);
                    });

                break;
        }

        // What both arms do to get back to where the reader was: the compose screen off the stack, and the timeline no
        // longer worth its age, this client being what changed it.
        void Popped()
        {
            _cache.Forget(new Subject.Destination(Rail.Showing.Kind));
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    /// <summary>The ten, in the order the rail draws them.</summary>
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

        // The tenth, and the only one the rail has ever grown by: immediately after Search and in its group, the
        // things you go to when you want something as against the timelines you read (ADR-0019, #181).
        new(DestinationKind.Discover, "Discover"),
        new(DestinationKind.Profile, profile.Account is { } account ? $"@{account.Split('@')[0]}" : "Profile"),
    ];

    /// <summary>
    ///     Reads the next page where the reader has walked onto the end of a list browsed a page at a time, which is
    ///     what <c>j</c> past the bottom means there (#180). Whether that is where they are is the screen's to say,
    ///     and what the next page is its subject's.
    /// </summary>
    private void Paging()
    {
        if (Screen.WantsMore)
        {
            _ = _arrival.More(Screen);
        }
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

        return _arrival.Open(new Subject.Account(AccountAddress.Parse(handle), WithReplies: false));
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

    private Task Delete(string postId) =>
        _enquiry.Put(
            ask => ask.Of(token => _ports.Author.Delete(_profile, postId, token)),
            eitherWay: () =>
            {
                _cache.Forget(new Subject.Destination(Rail.Showing.Kind));

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

    /// <summary>
    ///     What the reader is being asked to agree to: the answers ticked, counted rather than named. The ballot is on
    ///     screen with every answer being agreed to drawn <c>[x]</c> on it, so the question names nothing the reader
    ///     cannot check there — and quoting an answer somebody else wrote is what put the whole row at the contract's
    ///     80 columns (#219).
    /// </summary>
    private static string VotingFor(IReadOnlyList<int> choices) => choices.Count == 1
        ? "Cast the answer you ticked?"
        : $"Cast the {choices.Count} answers you ticked?";

    /// <summary>
    ///     Sends the agreed vote, and puts the poll the instance answers with in place of the one on screen.
    /// </summary>
    /// <remarks>
    ///     No refetch: Mastodon answers a vote with the complete updated poll, which the port grafts back onto the
    ///     post — so this is the same <see cref="Replace(Post)" /> a mark already makes, over an answer that cost one
    ///     call rather than two.
    ///     <para>
    ///         The ballot is let go as the vote leaves rather than when it lands. What is on screen from here on is
    ///         what the instance says the poll is, and a refusal is not something a reader can put right by leaving
    ///         their boxes ticked — the instance has already settled it.
    ///     </para>
    /// </remarks>
    /// <param name="screen">
    ///     The screen the vote was toggled on, which is where the ballot is. Named rather than read back off the
    ///     stack, so that a vote agreed to cannot clear a ballot on some screen the reader has since walked to.
    /// </param>
    private Task Cast(Screen screen, Post about, IReadOnlyList<int> choices)
    {
        screen.ClearChoices();

        return _enquiry.Put(
            ask => ask.Of(token => _ports.Engagement.Vote(_profile, about, choices, token)),
            eitherWay: voted =>
            {
                Replace(voted);
                Say("Vote cast.", isError: false);
            });
    }

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

    /// <summary>
    ///     An arm of <see cref="Do" /> that answers at once, as a used key — so that the table there is one shape all
    ///     the way down rather than a mix of two, and so that only the three arms which can decline say so.
    /// </summary>
    private static bool Ran(Action verb)
    {
        verb();

        return true;
    }

    /// <inheritdoc cref="Ran(Action)" />
    /// <remarks>
    ///     The same for a verb that reaches an instance. Not awaited, for the reason <see cref="Do" /> gives: what
    ///     came back lands on the drawing thread of its own accord, and the key was spent on the way out.
    /// </remarks>
    private static bool Ran(Func<Task> verb)
    {
        _ = verb();

        return true;
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
    ///     What a reply has to be written to, or <see langword="null" /> where there is nobody left to name. Mastodon
    ///     routes by the handles it parses out of a post's <em>text</em>: a direct post reaches the accounts its text
    ///     mentions and nobody else (ADR-0013), and on every other visibility <c>in_reply_to_id</c> puts the reply in
    ///     the thread and notifies nobody at all. Either way a reply that named nobody would not reach the account it
    ///     answers, which is the one thing a reply is for (#130) — so every reply is addressed, and <c>dm send</c>
    ///     writes its mention for the same reason.
    /// </summary>
    /// <remarks>
    ///     The mention is written into the editor rather than added on the way out, so a reader who does not want to
    ///     ping somebody can delete the name before sending — and so this client never sends words the reader could
    ///     not see.
    ///     <para>
    ///         Who it goes to is the conversation where there is one, rather than whoever spoke last: a thread with
    ///         three accounts in it answered to only one of them is a reply that dropped the rest of the conversation.
    ///         Elsewhere it is the account being answered followed by everyone their post named, which is that same
    ///         conversation as the post itself carries it — a direct message included, since the accounts a direct post
    ///         names are the accounts its instance delivered it to (#132). The conversation still wins where there is
    ///         one on screen, being who is left in it rather than who one message in it happened to name.
    ///     </para>
    /// </remarks>
    private string? Addressed(Post about)
    {
        // An instance says who a conversation is with rather than who is having it, so the profile's own account is
        // already not among them. Everywhere else it is the post itself, whatever its visibility: the account being
        // answered and everyone their post named, off the post already in hand.
        IReadOnlyList<string> with = (about.Visibility, Screen) switch
        {
            (PostVisibility.Direct, ConversationScreen conversation) => conversation.Conversation.With,
            _ => [about.Account, .. about.Mentions],
        };

        // An address this client cannot make sense of is left out rather than thrown over the reply, since a handle
        // an instance sent is not something the reader can do anything about. What is left is in the editor in front
        // of them, so a mention that is missing is missing where they can see it and type it themselves. The reader's
        // own account goes the same way: nobody is notified of their own reply, and a post answering a thread they are
        // in names them where the instance listed them.
        var accounts = with
            .Where(account => AccountAddress.IsWellFormed(account) && !IsMe(account))
            .Select(AccountAddress.Parse)
            .DistinctBy(account => account.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return accounts.Count == 0 ? null : DirectMessage.To(accounts, string.Empty);
    }

    /// <summary>
    ///     Whether a post is the profile's own, which is what settles whether pinning, editing and deleting act.
    /// </summary>
    /// <remarks>
    ///     Asked of the profile rather than read off <see cref="Post.IsMine" />, which is what the status row reads: both
    ///     are the one comparison <see cref="ActiveProfile.SignsInAs" /> makes, so the row and the refusal cannot come
    ///     to disagree, and a press is still refused by the shell that has the profile in hand (#220).
    /// </remarks>
    private bool IsMine(Post post) => IsMe(post.Account);

    /// <summary>Whether an account is the profile's own, compared the way <see cref="IsMine" /> compares one.</summary>
    private bool IsMe(string account) => _profile.SignsInAs(account);

    private void Push(Screen screen)
    {
        _stack.Add(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Waits to be told again before going ahead with what <paramref name="confirmation" /> carries.</summary>
    private void Confirm(Confirmation confirmation)
    {
        Asking = confirmation;

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

    /// <summary>Puts <paramref name="fresh" /> in place of the screen on top.</summary>
    /// <remarks>
    ///     Only ever the screen the question was asked from, since an answer lands only while that one is still in
    ///     front (<see cref="Enquiry" />) — so there is nothing to recheck here, and <c>esc</c> pressed before a
    ///     refresh lands leaves the screen walked back to alone.
    ///     <para>
    ///         In place of the top rather than pushed or reset: a refresh redraws where somebody is standing, so the
    ///         way they got there is still under them and <c>esc</c> still walks back out of it. A different screen
    ///         object rather than the same one changed, which is how the view is told the screen was replaced at all
    ///         (<c>docs/tui-shell.md</c>) — and being a new screen is also what opens it at the top, since the page a
    ///         screen remembers is its own and a fresh one remembers none. Walking back out is the one replacement
    ///         that keeps the page it was left on, and a refresh is not one (#133).
    ///     </para>
    /// </remarks>
    private void Freshened(Screen fresh)
    {
        _stack[^1] = fresh;

        // Gone with the screen it was said over, the same as at a push or an arrival: what a reader was told about the
        // list they were looking at is not about the one in front of them now.
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

    /// <summary>
    ///     Puts an account whose tie has just changed in place of the copy every screen in the stack is holding.
    /// </summary>
    /// <remarks>
    ///     Every screen and not only the top one, for the reason <see cref="Replace(Conversation)" /> gives: Discover
    ///     and an account screen opened from a row on it are on the stack together, so a row that still said nothing
    ///     under a follow just made would be the shell arguing with itself (#181).
    /// </remarks>
    private void Stands(Account account)
    {
        foreach (var screen in _stack)
        {
            screen.Stands(account);
        }
    }

    /// <summary>Puts a post that has just changed in place of the copy every screen in the stack is holding.</summary>
    private void Replace(Post post)
    {
        foreach (var screen in _stack)
        {
            screen.Replace(post);
        }

        _cache.Forget(new Subject.Destination(Rail.Showing.Kind));

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
