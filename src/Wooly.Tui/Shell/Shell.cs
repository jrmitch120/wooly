using System.Diagnostics.CodeAnalysis;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Configuration;
using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Search;
using Wooly.Core.Timelines;
using Wooly.Tui.Clipboard;
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
    ///     What the status row says where an instance refused the token a question was put with, once the session is
    ///     past its launch: what went wrong, and the key that fixes it (#248).
    /// </summary>
    private const string TokenRefused = "This profile's token was refused — ctrl-p to sign in again.";

    /// <summary>What <c>x</c> on the default profile answers, before anything is asked or after the registry refuses.</summary>
    private const string MakeAnotherTheDefault = "Make another profile the default before removing this one.";

    /// <summary>
    ///     Where an address goes. The one thing this shell does that leaves the terminal, and deliberately not one of
    ///     <see cref="ShellPorts" />: those are what the shell reaches an <em>instance</em> through, and a browser is
    ///     not on one (ADR-0014, #85).
    /// </summary>
    private readonly IWebBrowser _browser;

    /// <summary>
    ///     This machine's clipboard, which <c>ctrl-v</c> on a compose screen attaches a picture or copied files from
    ///     (#380). Like the browser, not a port: it is not on an instance.
    /// </summary>
    private readonly IClipboard _clipboard;

    /// <summary>
    ///     The temporary folder pictures pasted from the clipboard are written to before they are attached (#380), made
    ///     at the first.
    /// </summary>
    private string? _pastedTo;

    /// <summary>How many pictures have been pasted from the clipboard this session, which names the next (#380).</summary>
    private int _pasted;

    /// <summary>
    ///     Whether the status row has said this machine has nothing to read the clipboard with, which it says once a
    ///     session (#380).
    /// </summary>
    private bool _toldNoClipboard;

    private readonly IShellHost _host;
    private readonly TimeProvider _clock;
    private readonly ShellTiming _timing;
    private readonly string? _hashtag;

    /// <summary>
    ///     What the config file says a post goes out at — <c>default_visibility</c> and <c>default_language</c>, either
    ///     unknown where it says nothing — which a compose screen's To and Lang start on ahead of the account's own
    ///     (ADR-0024, #338, #340).
    /// </summary>
    private readonly PostDefaults _preferred;

    /// <summary>
    ///     Who this session is acting as, and everything that asks as them or holds what was read as them — which
    ///     <c>⏎</c> on the profiles screen puts a new one in place of, whole (ADR-0020, #243). Nobody, while the shell
    ///     has nobody it can act as and is standing on adding one (#247).
    /// </summary>
    private Acting? _acting;

    /// <summary>
    ///     What every question is put through, and the one place the stale-answer rule is stated — abandoned at a
    ///     switch, with everything still in flight, and a fresh one put in its place.
    /// </summary>
    private Enquiry _enquiry;

    /// <summary>What each step of adding a profile does, which puts its questions through <see cref="_enquiry" />.</summary>
    private ProfileAdding _adding;

    /// <summary>
    ///     Whether the profile acted as is the one the shell was launched as, so that a token refused before anything
    ///     has been read as it signs it in again rather than leaving a shell that can read nothing (#247).
    /// </summary>
    private bool _launching;

    private readonly ShellPorts _ports;

    /// <summary>
    ///     The query the people to mention were last asked for, and the wait before the instance's search of the
    ///     follows is asked for it — which the next different query calls off (#322).
    /// </summary>
    private (string Query, IDisposable Wait)? _pause;

    /// <summary>
    ///     This machine's profiles, which the profiles screen lists. Not one of <see cref="ShellPorts" />, for the
    ///     reason <see cref="_browser" /> is not: those reach an instance, and this reaches the local config (ADR-0020).
    /// </summary>
    private readonly ProfilePorts _profiles;

    /// <summary>
    ///     The instance the rail's foot names, read when the shell is built and again when a profile is added: the
    ///     profiles on this machine are the local config's, and asking it on every redraw would read a file to draw one
    ///     row.
    /// </summary>
    private string? _instance;

    private readonly List<Screen> _stack = [];

    /// <summary>
    ///     The last budget the old profile's instance reported, where the session has switched since — which the rail's
    ///     foot does not draw, being somebody else's budget, until an instance reports the new one's (#243).
    /// </summary>
    private RateLimitQuota? _quotaBeforeSwitch;

    /// <summary>How long each instance lets a post be, asked as posts are first written there (#319).</summary>
    private readonly LimitsByInstance _limits;

    /// <summary>
    ///     What each account acted as posts at by default, asked as the session starts acting as it — the fallback for
    ///     what compose starts on after the config's own preferences (ADR-0024, #339).
    /// </summary>
    private readonly DefaultsByProfile _defaults;

    /// <summary>
    ///     What calls off the files each compose screen is still sending up (ADR-0026, #375), called when the screen
    ///     leaves the stack — what was sent already is left for the instance to clear away.
    /// </summary>
    private readonly Dictionary<ComposeScreen, CancellationTokenSource> _sendingUp = [];

    /// <summary>
    ///     The compose screen whose <c>ctrl-s</c> is waiting on its attachments to finish before it sends (ADR-0026),
    ///     or none.
    /// </summary>
    private ComposeScreen? _waitingToSend;

    /// <param name="opening">
    ///     Who to act as — or, with nobody, what to open onto instead: adding a profile, as the only screen (#247).
    /// </param>
    /// <param name="preferences">
    ///     The config file's preferences: the hashtag the rail keeps a place for, and what compose starts on. None
    ///     where the file sets none.
    /// </param>
    public Shell(
        Opening opening,
        ShellPorts ports,
        ProfilePorts profiles,
        IShellHost host,
        IWebBrowser browser,
        IClipboard clipboard,
        TimeProvider clock,
        ShellTiming timing,
        Preferences? preferences = null)
    {
        _ports = ports;
        _profiles = profiles;
        _host = host;
        _limits = new LimitsByInstance(ports.Limits, host);
        _limits.Heard += Measured;
        _defaults = new DefaultsByProfile(ports.Defaults, host);
        _browser = browser;
        _clipboard = clipboard;
        _clock = clock;
        _timing = timing;
        _hashtag = preferences?.Hashtag;
        _preferred = new PostDefaults(preferences?.DefaultVisibility, preferences?.DefaultLanguage);

        Rail = new Rail(Destinations(opening.Profile, _hashtag), host, timing.Settle);

        Begin(opening.Profile);

        // Read off the field rather than captured, so that the rail asks whichever profile is being acted as.
        Rail.Selected += destination =>
        {
            if (_acting is { } acting)
            {
                _ = acting.Arrival.At(destination);
            }
        };

        Rail.Changed += () => Changed?.Invoke();

        if (opening.Profile is null)
        {
            _stack.Add(SigningIn(opening.Again, opening.Why));

            return;
        }

        _launching = true;
        _instance = RailInstance();
        _stack.Add(new FeedScreen(Rail.Showing, []));
    }

    /// <summary>Raised whenever anything on screen has changed. Always on the drawing thread.</summary>
    public event Action? Changed;

    /// <summary>
    ///     Raised when the breadcrumb's fetch mark has turned a frame and nothing else on screen has changed — so that
    ///     what is redrawn for it is the one row it is on rather than everything <see cref="Changed" /> redraws.
    /// </summary>
    public event Action? Ticked;

    /// <summary>The rail: the ten destinations, the cursor, and the selection.</summary>
    public Rail Rail { get; }

    /// <summary>The screen on top of the stack, which is what the content region is showing.</summary>
    public Screen Screen => _stack[^1];

    /// <summary>How deep the drill is, where one is a destination with nothing opened from it.</summary>
    public int Depth => _stack.Count;

    /// <summary>What each screen in the stack is called, outermost first — what the content panel is titled with.</summary>
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
    ///     Whether the rail is drawn, which is whenever somebody is being acted as. With nobody, the shell is adding a
    ///     profile and nothing else: every destination is read as somebody, so there is none to show (#247).
    /// </summary>
    public bool ShowsRail => _acting is not null;

    /// <summary>
    ///     Which frame of the breadcrumb's spinner is drawn — none until a fetch has been in flight for a whole tick.
    ///     What the mark draws, where <see cref="Fetching" /> is what the shell's own guards ask.
    /// </summary>
    public int SpinnerFrame => _enquiry.SpinnerFrame;

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
    /// <remarks>Nothing where the last one said is the old profile's, from before a switch (#243).</remarks>
    public RateLimitQuota? Quota =>
        _ports.RateLimit.Latest is { } latest && !ReferenceEquals(latest, _quotaBeforeSwitch) ? latest : null;

    /// <summary>
    ///     The instance this session is acting as, for the rail's foot — or <see langword="null" /> with only one profile
    ///     set up, where there is nobody to tell it apart from. <c>@jeff</c> on two instances is two people (ADR-0020).
    /// </summary>
    public string? Instance => _instance;

    /// <summary>
    ///     The people a post can mention who best match <paramref name="query" />, what follows the <c>@</c> of the
    ///     word being typed — best first, at most <see cref="PeopleToMention.Most" />, and asking nothing of the
    ///     instance while it answers (#318). The first ask of a session starts reading the profile's follows in the
    ///     background, which join the answers as they arrive (#321). Where those are not all of the follows and fewer
    ///     than enough answer, the instance's search of them is asked once the asking has paused for a settle — once
    ///     per pause, and once per query in a session (#322). Nobody while nobody is being acted as.
    /// </summary>
    public IReadOnlyList<Mentionable> PeopleMatching(string query)
    {
        if (_acting is not { } acting)
        {
            return [];
        }

        if (acting.People.FirstAsked())
        {
            _ = ReadFollows(acting);
        }

        // The same query asked again — a redraw, or the caret moving within the word — is the same pause, not a new
        // one: only a different query means the typing went on.
        if (_pause?.Query != query)
        {
            _pause?.Wait.Dispose();
            _pause = acting.People.WorthSearching(query)
                ? (query, _host.After(_timing.Settle, () =>
                {
                    _pause = null;
                    _ = SearchFollows(acting, query);
                }))
                : null;
        }

        return acting.People.Matching(query);
    }

    /// <summary>What mentioning <paramref name="person" /> writes into a post, as the profile acted as writes it.</summary>
    public string MentionOf(Mentionable person) => _acting?.People.MentionOf(person) ?? $"@{person.Address}";

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

    /// <summary>
    ///     Who is being acted as, where only somebody could have got this far: <see cref="Do" /> lets nothing through
    ///     with nobody but the add screen's own verbs, which ask as nobody (#247).
    /// </summary>
    private Acting Actor => _acting ?? throw new InvalidOperationException("Nobody is being acted as.");

    /// <summary>Opens the shell onto its first destination, and reads the counts the rail carries.</summary>
    /// <remarks>
    ///     The counts are asked as whoever was acted as when this was called, and not at all where a switch has come
    ///     between — the switch opens the shell again for the new profile, counts and all (#243). Nothing with nobody
    ///     to act as: the add screen is already up, and asks nothing until the reader has typed something to send.
    /// </remarks>
    public async Task Open()
    {
        if (_acting is not { } acting)
        {
            return;
        }

        var abandoned = _enquiry.Abandoned;

        await acting.Arrival.At(Rail.Showing);

        if (!abandoned.IsCancellationRequested)
        {
            await Counts(acting.Profile, abandoned);
        }
    }

    /// <summary>
    ///     A rail keypress. The cursor moves at once; the selection — and the fetch — follow when the pressing stops.
    ///     A touched draft is asked about first, and taken off as the cursor moves rather than left standing until the
    ///     pressing stops, where more could be written into it only to be thrown away unasked (#373).
    /// </summary>
    public void Step(int by) => Stepping(() => Rail.Step(by));

    /// <summary>A rail group keypress, <c>`</c> or <c>~</c>, which settles and fetches as <see cref="Step" /> does.</summary>
    public void StepGroup(int by) => Stepping(() => Rail.StepGroup(by));

    /// <summary>
    ///     Takes a draft off and moves the rail cursor with <paramref name="step" />. A step that took a draft off
    ///     arrives at the destination stepped to there and then rather than after the settle, which would stand the
    ///     screen under the draft in front for the wait (#373, ADR-0014 amended). Stepped onto the destination already
    ///     shown, it walks back out to it, as a click on it does (<see cref="Arrive" />). Tabbing on from there settles
    ///     as ever.
    /// </summary>
    private void Stepping(Action step) =>
        Leaving(0, () =>
        {
            var dropped = DropDrafts();

            step();

            if (!dropped)
            {
                return;
            }

            var shown = Rail.Current == Rail.Cursor;

            Rail.LandNow();

            if (shown)
            {
                Unwind(0);
            }
        });

    /// <summary>
    ///     A click on the <paramref name="at" />th destination on the rail. Another destination is arrived at at once:
    ///     the cursor and the selection go there together, abandoning whatever the tabbing left waiting (#288). The
    ///     destination already shown is walked back out to instead (<see cref="Unwind" />, #289). Either way a touched
    ///     draft is asked about first (#373). Nothing with nobody to act as, where the keys go nowhere either.
    /// </summary>
    /// <remarks>
    ///     The keys have no equivalent of the walk back: tabbing back onto the destination shown is still a walk that
    ///     ended where it began.
    /// </remarks>
    public void Arrive(int at)
    {
        if (_acting is null)
        {
            return;
        }

        Leaving(0, () =>
        {
            var shown = Rail.Current == at;

            Rail.GoTo(Rail.Destinations[at].Kind);

            if (shown)
            {
                Unwind(0);
            }
        });
    }

    /// <summary>
    ///     A click on the crumb <paramref name="depth" /> screens up the stack, counted from nought at the destination's
    ///     own: everything drilled in above it comes off in one move, and it is in front again as it was left
    ///     (<see cref="Unwind" />, #308). The first crumb is the destination shown on the rail, and a click on it is a
    ///     click on that (<see cref="Arrive" />), so the two are one rule.
    /// </summary>
    /// <remarks>
    ///     The crumb in front is nothing, the first among them: one screen deep, it must not snap back a rail cursor
    ///     tabbing has left waiting the way the rail's click does.
    /// </remarks>
    public void WalkBack(int depth)
    {
        if (depth >= _stack.Count - 1)
        {
            return;
        }

        if (depth == 0 && _acting is not null)
        {
            Arrive(Rail.Current);

            return;
        }

        Leaving(depth + 1, () => Unwind(depth));
    }

    /// <summary>
    ///     What a click does while a question is open, which is all it does: an open confirmation is declined, as any
    ///     key but the agreeing one declines it, and an open filter prompt is closed (#286). Whatever the click was
    ///     on is not carried out, so a stray one never confirms anything or acts behind a question.
    /// </summary>
    /// <returns>Whether there was a question to spend the click on.</returns>
    public bool DeclineOpenQuestion()
    {
        if (Asking is not null)
        {
            _ = Answer(agreed: false);

            return true;
        }

        if (!Screen.CloseFilterPrompt())
        {
            return false;
        }

        Changed?.Invoke();

        return true;
    }

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
    public bool Do(Verb verb, int? answer) => (_acting is not null || WithNobody(verb)) && verb switch
    {
        Verb.NextReference => WalkReference(1),
        Verb.PreviousReference => WalkReference(-1),
        Verb.Toggle => answer is { } option && Toggle(option),

        Verb.Back => Ran(Back),
        Verb.Help => Ran(Help),
        Verb.Search => Ran(Search),
        Verb.Profiles => Ran(Profiles),
        Verb.AddProfile => Ran(AddProfile),
        Verb.ActAs => Ran(ActAs),
        Verb.MakeDefault => Ran(MakeDefault),
        Verb.RemoveProfile => Ran(AskToRemoveProfile),
        Verb.SignInAgain => Ran(SignInAgain),
        Verb.Continue => Ran(Continue),
        Verb.PasteToken => Ran(PasteToken),
        Verb.NextDestination => Ran(() => Step(1)),
        Verb.PreviousDestination => Ran(() => Step(-1)),
        Verb.NextGroup => Ran(() => StepGroup(1)),
        Verb.PreviousGroup => Ran(() => StepGroup(-1)),
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
        Verb.WriteWarning => Ran(() => _ = ChangeCompose(compose => compose.WriteTheWarning())),
        // Answered whether or not there was anywhere to go, so that an arrow off either end of the walk or of To stops
        // there rather than falling through to Terminal.Gui, which would carry the focus round to the other end.
        Verb.PreviousField => Ran(() => _ = ChangeCompose(compose => compose.Walk(-1))),
        Verb.NextField => Ran(() => _ = ChangeCompose(compose => compose.Walk(1))),
        Verb.PreviousChoice => Ran(() => _ = ChangeCompose(compose => compose.Choose(-1))),
        Verb.NextChoice => Ran(() => _ = ChangeCompose(compose => compose.Choose(1))),
        Verb.RemoveAttachment => Ran(() => RemoveAttachment(compose => compose.Remove())),
        Verb.BringBackAttachment => Ran(() => _ = ChangeCompose(compose => compose.BringBack())),
        Verb.EarlierAttachment => Ran(() => _ = ChangeCompose(compose => compose.Reorder(-1))),
        Verb.LaterAttachment => Ran(() => _ = ChangeCompose(compose => compose.Reorder(1))),
        Verb.RetryAttachment => Ran(() => Retry(compose => compose.Retry())),
        Verb.ToggleSensitive => Ran(ToggleSensitive),

        // Nothing, and the terminal's own — which the window has already taken, and which no screen answers either.
        Verb.None => false,
        _ when verb.NeedsATerminal() => false,

        // Everything else is the screen's own, and the screen is what carries it out.
        _ => Ran(() => Screen.Answer(verb, Actor.Reach)),
    };

    /// <summary>
    ///     The verbs that mean something with nobody to act as, which are the add screen's own: every other one — the
    ///     rail, search, the profiles, the keymap — is somewhere to go, and there is nowhere (#247).
    /// </summary>
    private static bool WithNobody(Verb verb) => verb is Verb.Continue or Verb.PasteToken or Verb.Back;

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
    ///     jumped to (#166). And what a click in the content does once the rows have said which thing is under the
    ///     pointer (#290).
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
        Screen.Opens is { } opening ? Actor.Arrival.Open(new Subject.Thread(opening)) : Task.CompletedTask;

    /// <summary>
    ///     Asks for what is there now: evicts what the screen's subject last held, puts the same question that brought
    ///     it up, and opens the answer at the top so that what has just arrived is what the reader is looking at
    ///     (<c>docs/tui-shell.md</c>, #84).
    /// </summary>
    /// <remarks>
    ///     Only where the screen says it answers to <c>g</c>, which is the nine the contract names. A second press
    ///     while anything is already in flight does nothing at all — no second question, and no in-flight UI beyond
    ///     the spinner the breadcrumb already carries.
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

        return Actor.Arrival.Again(subject);
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
                return Actor.Arrival.Open(new Subject.Tag(reference.Text.TrimStart('#')));

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

        await Actor.Arrival.Open(new Subject.Account(author, WithReplies: false));
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

        // A send waiting on its attachments is a level of its own over the draft: esc calls the send off and leaves the
        // draft as it was, to be changed or sent again (ADR-0026, #375).
        if (_waitingToSend is not null && ReferenceEquals(_waitingToSend, Screen))
        {
            _waitingToSend = null;

            Say("Not sent — the draft is as it was.", isError: false);

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
            Leaving(_stack.Count - 1, Pop);

            return;
        }

        if (Screen is AddProfileScreen alone)
        {
            // Standing alone there is nothing under it to go back to, so esc goes back to its first step instead —
            // which is still what gives a waiting sign-in up (#247).
            alone.StartOver();
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
    ///     Opens the profiles screen: every profile on this machine, marked with which one this session is acting as
    ///     and which one is the default (ADR-0020).
    /// </summary>
    /// <remarks>
    ///     Read off the local config there and then, rather than put through <see cref="Enquiry" />: nothing here
    ///     reaches an instance, so there is nothing to wait for, no fetch mark and no answer that could land late.
    ///     Pressed on the screen itself it is where the reader already is, the way <c>?</c> is on the keymap.
    ///     <para>
    ///         From compose it is a way out of the draft rather than a screen over it: drafts do not survive a switch
    ///         (ADR-0020), so the draft is taken off — asked about first where it is touched (#373) — rather than left
    ///         under a switch that would throw it away without asking.
    ///     </para>
    /// </remarks>
    public void Profiles()
    {
        if (Screen is ProfilesScreen)
        {
            return;
        }

        Leaving(0, () =>
        {
            DropDrafts();
            Push(Listed());
        });
    }

    /// <summary>
    ///     <c>ctrl-q</c>: <paramref name="quit" />, which only a terminal can do — asked about first where a touched draft
    ///     would go with it (#373).
    /// </summary>
    public void Quit(Action quit) => Leaving(0, quit);

    /// <summary>
    ///     <c>a</c> on the profiles screen: adds a profile, from the instance up (#245). Nothing is read or sent until
    ///     the reader has typed something to send.
    /// </summary>
    public void AddProfile()
    {
        if (Screen is ProfilesScreen)
        {
            Push(new AddProfileScreen());
        }
    }

    /// <summary>
    ///     <c>R</c> on the profiles screen: signs the picked profile in again, its instance and name fixed — only the
    ///     sign-in and the check, and the token replaced without asking, since replacing it is why <c>R</c> was pressed
    ///     (#248). Nothing is read or sent until the reader goes on.
    /// </summary>
    public void SignInAgain()
    {
        if (Screen is ProfilesScreen { PickedProfile: { } picked })
        {
            Push(AddProfileScreen.Again(picked, why: null, alone: false));
        }
    }

    /// <summary>
    ///     <c>⏎</c> on the profiles screen: acts as the picked profile for the rest of the session, which starts again
    ///     on Home as if it had been launched with it (<see cref="Act" />, ADR-0020, #243). Nothing on the profile
    ///     already acted as.
    /// </summary>
    public void ActAs()
    {
        if (Screen is not ProfilesScreen { ToActAs: { } name })
        {
            return;
        }

        Act(name);
    }

    /// <summary>
    ///     <c>D</c> on the profiles screen: makes the picked profile the default, the one the CLI and the next launch act
    ///     as when not told otherwise — <c>profile switch</c>, through the same <see cref="IProfileRegistry.Switch" />
    ///     (ADR-0020, #244). Nothing on the profile already the default.
    /// </summary>
    /// <remarks>
    ///     Who this session is acting as does not move, and nothing reaches an instance: browsing as a profile and
    ///     saving one are kept apart so that a look at one account never changes which one a script posts as. Nothing
    ///     is asked first either, since <c>D</c> on the old default puts it back. The list is read again with the row
    ///     still picked, and the status row says what changed in <c>profile switch</c>'s words.
    /// </remarks>
    public void MakeDefault()
    {
        if (Screen is not ProfilesScreen { ToMakeDefault: { } name })
        {
            return;
        }

        try
        {
            _profiles.Registry.Switch(name);
        }
        catch (WoolyException failure)
        {
            Say(failure.Message, isError: true);

            return;
        }

        var list = Listed();

        list.Pick(name);
        Freshened(list);

        // Said after the list is up, which clears what was said over the one it replaced.
        Say(ProfileWords.ActsAs(name), isError: false);
    }

    /// <summary>
    ///     <c>x</c> on the profiles screen: asks before removing the picked profile, in the form deleting a post does
    ///     (story 43, ADR-0020, #246). Refused on the profile this session is acting as, and on the default.
    /// </summary>
    /// <remarks>
    ///     The one acted as, since a session whose own token went out from under it would have nothing left to act as;
    ///     the default, since commands and the next launch would have nothing to act as either, and choosing another is
    ///     the reader's (<c>D</c>), not this client's. Both refused before anything is asked, and refused rather than
    ///     not offered — the one exception to #220 (#246) — because each refusal says what to do first.
    /// </remarks>
    public void AskToRemoveProfile()
    {
        if (Screen is not ProfilesScreen { PickedProfile: { } picked })
        {
            return;
        }

        if (picked.Name == Actor.Profile.Name)
        {
            Say("Switch to another profile before removing this one.", isError: true);

            return;
        }

        if (picked.IsCurrent)
        {
            Say(MakeAnotherTheDefault, isError: true);

            return;
        }

        Confirm(new Confirmation("Remove this profile?", () => RemoveProfile(picked.Name), Going: "remove"));
    }

    /// <summary>
    ///     <c>x</c> agreed to: the profile goes through <see cref="IProfileRegistry.Remove" />, config entry and token
    ///     together, as <c>profile remove</c> does. Its row goes with the pick moved beside it, and the status row says
    ///     what went.
    /// </summary>
    /// <remarks>
    ///     Nothing reaches an instance: the token is only this machine's copy, and the authorization is still the
    ///     instance's to revoke. The rail's instance row is asked again, since one profile left is nobody to tell apart.
    /// </remarks>
    private Task RemoveProfile(string name)
    {
        if (Screen is not ProfilesScreen shown)
        {
            return Task.CompletedTask;
        }

        var beside = shown.Beside(name);

        try
        {
            _profiles.Registry.Remove(name);
        }
        catch (DefaultProfileRemovalException)
        {
            // Made the default since the list was read — profile switch in another terminal. Said in this screen's
            // words, which name D, rather than the registry's, which name the CLI.
            Say(MakeAnotherTheDefault, isError: true);

            return Task.CompletedTask;
        }
        catch (WoolyException failure)
        {
            Say(failure.Message, isError: true);

            return Task.CompletedTask;
        }

        var list = Listed();

        if (beside is not null)
        {
            list.Pick(beside);
        }

        _instance = RailInstance();
        Freshened(list);

        // Said after the list is up, which clears what was said over the one it replaced.
        Say($"Removed profile {name}.", isError: false);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     <c>⏎</c> on the add screen: on to the next step — the instance signed in at, the token checked, the name
    ///     saved under (<see cref="ProfileAdding" />).
    /// </summary>
    public Task Continue() => Screen is AddProfileScreen adding ? _adding.Continue(adding) : Task.CompletedTask;

    /// <summary><c>t</c> on the add screen: the browser given up for a pasted token.</summary>
    public void PasteToken()
    {
        if (Screen is AddProfileScreen adding)
        {
            _adding.PasteToken(adding);
        }
    }

    /// <summary>
    ///     Puts a letter into whatever is being typed into: the search prompt, say. Never a compose screen's fields,
    ///     which are widgets of their own and take their own letters (#320).
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

    /// <summary>
    ///     Puts a paste into whatever is being typed into, the way <see cref="Type" /> puts a letter: a terminal in
    ///     bracketed-paste mode delivers one as a single string rather than as keys, so without this a field the shell
    ///     types into itself would never see it — the add screen's token field above all, which is there to be pasted
    ///     into (#245).
    /// </summary>
    /// <remarks>
    ///     Every field here is one row, so a line break or a tab in the paste stands as a space — a token copied with its
    ///     line break is then trimmed of it like any other whitespace — and any other control character is left out.
    ///     <para>
    ///         Nothing where the screen is not typing, and not a run of keys either: a paste is text, and replaying it
    ///         as keys would boost, compose and delete by whatever letters it happened to hold. The compose editor and
    ///         its warning field are widgets of their own and take their own pastes, which is what answering no leaves
    ///         them to — unless the paste is files dropped onto the terminal, which a compose screen attaches
    ///         (<see cref="Dropped" />, #375).
    ///     </para>
    /// </remarks>
    /// <returns>Whether the paste was taken, which is what settles whether it is left for whatever has focus.</returns>
    public bool Paste(string text)
    {
        if (Screen is ComposeScreen { TakesAttachments: true } compose
            && Dropped.Paths(text, compose.Limits) is { } dropped)
        {
            Attach(compose, dropped);

            return true;
        }

        if (!Screen.IsTyping)
        {
            return false;
        }

        foreach (var letter in text.Replace("\r\n", "\n"))
        {
            if (char.IsWhiteSpace(letter))
            {
                Screen.Type(' ');
            }
            else if (!char.IsControl(letter))
            {
                Screen.Type(letter);
            }
        }

        Changed?.Invoke();

        return true;
    }

    /// <summary>
    ///     <c>ctrl-v</c> on a compose screen (#380): a picture on this machine's clipboard is written to a file of its
    ///     own and attached from there, and copied files are attached, as a drop is (<see cref="Paste" />, ADR-0026).
    /// </summary>
    /// <returns>Whether the paste was taken; one that was not is left to the field's own paste.</returns>
    public bool PasteFromTheClipboard()
    {
        if (Screen is not ComposeScreen { TakesAttachments: true } compose)
        {
            return false;
        }

        switch (_clipboard.Read())
        {
            case Clipped.Picture(var png):
                Attach(compose, [Pasted(png)]);

                return true;

            // Subject to what a drop is: the files there that the instance takes, up to what the post has room for.
            case Clipped.Files(var paths):
                var accepted = paths.Where(path => File.Exists(path) && compose.Limits.Accepts(path)).ToList();

                if (accepted.Count == 0)
                {
                    Say("None of the copied files is a type this instance takes.", isError: true);
                }
                else
                {
                    Attach(compose, accepted);
                }

                return true;

            // Said once a session: the author who has no wish to install either is still pasting text every time. Said
            // once the field has pasted, since the paste is an edit and an edit spends a notice (#364).
            case Clipped.NoTool(var why) when !_toldNoClipboard:
                _toldNoClipboard = true;
                Apply(() => Say(why, isError: false));

                return false;

            default:
                return false;
        }
    }

    /// <summary>
    ///     <paramref name="png" /> written to <c>pasted-N.png</c> — counted over the session, so each pasted picture's
    ///     row says something of its own — in a temporary folder of the session's (#380).
    /// </summary>
    private string Pasted(byte[] png)
    {
        _pastedTo ??= Directory.CreateTempSubdirectory("wooly-pasted-").FullName;

        var path = Path.Combine(_pastedTo, $"pasted-{++_pasted}.png");

        File.WriteAllBytes(path, png);

        return path;
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
    ///     Makes <paramref name="change" /> to the compose screen on top — every change to a draft comes in here, from
    ///     its fields, its lists and its keys — and settles what follows from it by the one rule (#364): a change that
    ///     says it <see cref="ComposeChange.Edited" /> spends a notice said over the draft, and anything but no change at
    ///     all is announced so the screen is drawn again.
    /// </summary>
    /// <remarks>
    ///     A notice is spent by an edit of the draft and not by a move (#319). The status row holds a notice or the
    ///     keymap and never both, and while a post is being written the keys go to its fields rather than to anything
    ///     that would otherwise take a notice down — so a refusal of the post would stand, hiding every key compose
    ///     answers to, until <c>esc</c> threw the draft away. Changing the draft is doing what the notice asked; walking
    ///     the fields, choosing on To or opening a list is not, and leaves the notice to be read.
    /// </remarks>
    /// <returns>What the change was, or nothing where compose is not on top and no change was made.</returns>
    public ComposeChange ChangeCompose(Func<ComposeScreen, ComposeChange> change)
    {
        if (Screen is not ComposeScreen compose)
        {
            return ComposeChange.None;
        }

        var made = change(compose);

        if (made == ComposeChange.Edited && Notice is not null)
        {
            // Saying nothing announces the change itself.
            Say(null, isError: false);
        }
        else if (made != ComposeChange.None)
        {
            Changed?.Invoke();
        }

        return made;
    }

    /// <summary>
    ///     <c>s</c> on compose's Media header, or a click on its toggle: puts what is attached behind a click or takes it
    ///     back out (#379). While a warning holds it on, the status row says so instead, since a press that changes
    ///     nothing on screen would otherwise read as one the shell missed.
    /// </summary>
    public void ToggleSensitive()
    {
        if (Screen is ComposeScreen { SensitiveByAWarning: true, Attachments.Count: > 0 })
        {
            Say("The warning already hides what is attached.", isError: false);

            return;
        }

        _ = ChangeCompose(compose => compose.ToggleSensitive());
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

        // Still drawn unread until the first press is answered, so a second one lands here rather than above. Nothing
        // is said: the breadcrumb's fetch mark already says something is on its way, and "Marked as read." follows.
        var marking = Actor.Marking;

        if (!marking.Add(conversation.Id))
        {
            return;
        }

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var profile = Actor.Profile;

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Messages.MarkRead(profile, conversation.Id, token)),
            eitherWay: marked => Tell(new Change.ConversationMarked(marked)),
            ifStillHere: _ => Say("Marked as read.", isError: false));

        // Let go on the drawing thread, behind whatever the answer or its failure has already queued there: by then the
        // conversation is drawn as the instance has it, and a press that fails can be pressed again.
        Apply(() => marking.Remove(conversation.Id));
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

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var profile = Actor.Profile;

        await _enquiry.Put(
            ask => ask.Of(token => _ports.Engagement.Mark(profile, about.Id, mark, !about.Marks.Has(mark), token)),
            eitherWay: marked => Tell(new Change.PostChanged(marked)));
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

    /// <summary>
    ///     Answers whatever the shell was waiting to be told again with <paramref name="pressed" />: a yes where it is
    ///     the key that agrees to it (<see cref="Confirmation.AgreedBy" />), and a no for anything else, a key that means
    ///     nothing to the shell included (story 43).
    /// </summary>
    public Task Answer(ShellKey? pressed) => Answer(agreed: Asking?.AgreedBy(pressed) == true);

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

        // A Lang holding something that is not a language is a typo, and a typo is not published as one (#340).
        if (compose.Lang.Refusal is { } refusal)
        {
            Say(refusal, isError: true);

            return;
        }

        // A post never goes out without something its author attached (ADR-0026): not without one the instance refused,
        // and not before the rest are ready, which it waits for rather than refuses — esc calls the wait off (#375).
        if (compose.Refused > 0)
        {
            Say(
                compose.Refused == 1
                    ? "Something attached was refused — take it off to send."
                    : $"{compose.Refused} attachments were refused — take them off to send.",
                isError: true);

            return;
        }

        if (compose.Attachments.Count(attachment => attachment.Unfinished) is > 0 and var unfinished)
        {
            _waitingToSend = compose;

            Say(
                unfinished == 1
                    ? "Will send once 1 attachment finishes — esc to stop."
                    : $"Will send once {unfinished} attachments finish — esc to stop.",
                isError: false);

            return;
        }

        _waitingToSend = null;

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var profile = Actor.Profile;

        switch (compose.Outgoing)
        {
            case Outgoing.Saving(var postId, var edit):
                await _enquiry.Put(
                    ask => ask.Of(token => _ports.Author.Edit(profile, postId, edit, token)),
                    eitherWay: saved =>
                    {
                        Popped();
                        Tell(new Change.PostChanged(saved));
                        Say("Saved.", isError: false);
                    });

                break;

            case Outgoing.Publishing(var draft):
                // A reply written inside a conversation carries which one, taken now from the screen it was written
                // over rather than read back when it lands — by then the reader may be anywhere.
                var within = compose.Purpose == ComposeFor.Reply && _stack is [.., ConversationScreen conversation, _]
                    ? conversation.Conversation.Id
                    : null;

                await _enquiry.Put(
                    ask => ask.Of(token => _ports.Author.PublishAttached(profile, draft, token)),
                    eitherWay: published =>
                    {
                        // A reply written in a conversation goes on the end of it, which the conversation and the
                        // list it was opened from each hear for themselves.
                        Popped();
                        Tell(new Change.PostSent(published, within));
                        Say("Sent.", isError: false);
                    });

                break;
        }

        // What both arms do to get back to where the reader was: the compose screen off the stack, before the change
        // is heard by what is left on it.
        void Popped() => Leave(_stack.Count - 1);
    }

    /// <summary>
    ///     Builds everything that asks as <paramref name="profile" />, or holds what was read as it: at launch, and
    ///     again at every switch (#243). Nothing built for one profile is handed to the next — the next gets its own.
    ///     With nobody, only what adding a profile asks through (#247).
    /// </summary>
    [MemberNotNull(nameof(_enquiry), nameof(_adding))]
    private void Begin(ActiveProfile? profile)
    {
        // Asked from whatever is on top, which is the whole of the stale-answer rule: an answer lands only while the
        // screen it was asked from is still in front of the reader.
        var enquiry = new Enquiry(_host, _clock, _timing.CountdownStep, _timing.MarkStep, () => Screen);
        enquiry.Said += Say;
        enquiry.Changed += () => Changed?.Invoke();
        enquiry.Ticked += () => Ticked?.Invoke();
        enquiry.Refused += refused => Refused(enquiry, refused);

        _enquiry = enquiry;

        _adding = new ProfileAdding(
            enquiry,
            _profiles,
            _browser,
            held: screen => _stack.Contains(screen),
            changed: () => Changed?.Invoke(),
            say: Say,
            confirm: Confirm,
            added: Added);

        _pause?.Wait.Dispose();
        _pause = null;
        _acting = profile is null ? null : Acts(profile, enquiry, new SubjectCache(_clock, _timing.CacheFor));

        // Asked now rather than when compose opens, so that opening it asks nothing (ADR-0024).
        if (profile is not null)
        {
            _defaults.Ask(profile);
        }
    }

    /// <summary>
    ///     Everything that asks as <paramref name="profile" /> through <paramref name="enquiry" />, holding what it
    ///     reads in <paramref name="cache" /> — fresh at launch and at a switch, and the same enquiry and cache again
    ///     where the profile acted as has only been signed in again (#248).
    /// </summary>
    private Acting Acts(ActiveProfile profile, Enquiry enquiry, SubjectCache cache)
    {
        // An arrival settles what a subject is on screen and what its badge says, and a change what goes stale;
        // putting any of it there is this shell's own business, since the stack and the rail are its.
        var arrival = new Arrival(profile, _ports, enquiry, cache, showing: () => Rail.Showing.Kind);

        arrival.Arrives += Reset;
        arrival.Drills += Push;
        arrival.Refreshes += Freshened;
        arrival.Filled += () =>
        {
            Saw(Screen);
            Say(null, isError: false);
        };
        arrival.Counts += Counted;
        arrival.Moves += Moved;
        arrival.Heard += Heard;

        var reach = new Reach(
            profile,
            _ports,
            enquiry,
            arrival,
            say: Say,
            confirm: Confirm,
            changed: () => Changed?.Invoke());

        return new Acting(profile, arrival, reach, cache, new PeopleToMention(profile));
    }

    /// <summary>
    ///     Acts as the profile called <paramref name="name" /> for the rest of the session, which starts again on Home as
    ///     if it had been launched with it (ADR-0020, #243) — from another profile, or from nobody (#247).
    /// </summary>
    /// <remarks>
    ///     Reset in place rather than rebuilt, with everything that belonged to the old profile let go of: the enquiry
    ///     it asked through is abandoned, which calls off every question in flight and drops every answer still to
    ///     land — what the instance already did is not undone, only never drawn — and the arrival, its cache, the
    ///     stack, the rail's labels and counts, a confirmation waiting and the quota go with it. Pictures stay, being a
    ///     file server's rather than anybody's, and they are the window's rather than this shell's anyway.
    ///     <para>
    ///         Resolved before anything is let go of, so a profile that cannot be — its token gone from the store —
    ///         leaves the session exactly as it was, and says why. The config is only read: acting as a profile is
    ///         for this session, and making one the default is <c>D</c>'s.
    ///     </para>
    /// </remarks>
    private void Act(string name)
    {
        if (ResolvedOrSaid(name) is not { } next)
        {
            return;
        }

        LetGo();
        Begin(next);
        _instance = RailInstance();

        Rail.Restart(Destinations(next, _hashtag));
        Reset(new FeedScreen(Rail.Showing, []));

        _ = Open();
    }

    /// <summary>
    ///     The profile acted as, read again for the token it was just signed in with: what asks as it is built again
    ///     around the same enquiry and cache, so nothing in flight is called off and nothing read is thrown away —
    ///     the account is the same one, which is what makes it the same person carrying on (#248).
    /// </summary>
    private void Renewed(string name)
    {
        if (ResolvedOrSaid(name) is not { } renewed)
        {
            return;
        }

        _acting = Acts(renewed, _enquiry, Actor.Cache) with { Marking = Actor.Marking };
    }

    /// <summary>
    ///     The profile called <paramref name="name" />, token and all — or <see langword="null" />, with the resolver's
    ///     own words for why on the status row, where it can't be: its token gone from the store above all.
    /// </summary>
    private ActiveProfile? ResolvedOrSaid(string name)
    {
        try
        {
            return _profiles.Registry.Resolve(name);
        }
        catch (WoolyException failure)
        {
            Say(failure.Message, isError: true);

            return null;
        }
    }

    /// <summary>
    ///     An instance refused the token <paramref name="enquiry" /> asked with. At launch, before anything has been read
    ///     as the profile, that is a token revoked since it was stored, and a shell that can read nothing is no use to
    ///     anybody: it signs the profile in again instead, filled in for it, with nobody acted as until it has (#247).
    ///     Anywhere else it is said, naming the key that fixes it, and nothing opens by itself: whether to sign in again
    ///     now is the reader's (#248).
    /// </summary>
    /// <remarks>Nothing where the question was asked as a profile no longer acted as, whose token is not this one's.</remarks>
    private void Refused(Enquiry enquiry, AuthenticationException refused)
    {
        if (enquiry != _enquiry || _acting is not { } acting)
        {
            return;
        }

        if (!_launching || enquiry.Answered)
        {
            Say(TokenRefused, isError: true);

            return;
        }

        var again = _profiles.Registry.List().SingleOrDefault(profile => profile.Name == acting.Profile.Name);

        LetGo();
        Begin(profile: null);
        _instance = null;

        Reset(SigningIn(again, refused.Message));
    }

    /// <summary>
    ///     Lets go of everything asked as the profile acted as, before <see cref="Begin" /> puts whoever is next in its
    ///     place: every question in flight called off, a confirmation waiting dismissed, and its quota no longer drawn.
    /// </summary>
    private void LetGo()
    {
        _enquiry.Abandon();

        Asking = null;
        _launching = false;
        _quotaBeforeSwitch = _ports.RateLimit.Latest;
    }

    /// <summary>
    ///     The add screen standing alone, for a shell with nobody to act as: filled in for <paramref name="again" /> where
    ///     that profile is to be signed in again, saying <paramref name="why" /> (#247).
    /// </summary>
    private static AddProfileScreen SigningIn(ProfileSummary? again, string? why) =>
        again is null ? new AddProfileScreen(alone: true) : AddProfileScreen.Again(again, why, alone: true);

    /// <summary>The ten, in the order the rail draws them: its groups' order (ADR-0021).</summary>
    private static IReadOnlyList<Destination> Destinations(ActiveProfile? profile, string? hashtag) =>
    [
        new(DestinationKind.Home, "Home", Timeline.Home),
        new(DestinationKind.Local, "Local", Timeline.Local),
        new(DestinationKind.Federated, "Federated", Timeline.Federated),
        new(
            DestinationKind.Hashtag,
            hashtag is null ? "Hashtag" : $"#{hashtag}",
            hashtag is null ? null : Timeline.Tag(hashtag)),

        // Discover, the only entry the rail has ever grown by (ADR-0019), leads Explore: the tools you go looking with,
        // ahead of the Inbox that comes to you (ADR-0021).
        new(DestinationKind.Discover, "Discover"),
        new(DestinationKind.Search, "Search"),
        new(DestinationKind.Notifications, "Notifications"),
        new(DestinationKind.Messages, "Direct messages"),
        new(DestinationKind.Requests, "Follow requests"),
        new(DestinationKind.Profile, profile?.Handle ?? "Profile"),
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
            _ = Actor.Arrival.More(Screen);
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

        return Actor.Arrival.Open(new Subject.Account(AccountAddress.Parse(handle), WithReplies: false));
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

    private Task Delete(string postId)
    {
        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var profile = Actor.Profile;

        return _enquiry.Put(
            ask => ask.Of(token => _ports.Author.Delete(profile, postId, token)),
            eitherWay: () =>
            {
                Tell(new Change.PostGone(postId));
                Say("Deleted.", isError: false);
            });
    }

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
    ///     post — so this is the same <see cref="Change.PostChanged" /> a mark already reports, over an answer that
    ///     cost one call rather than two.
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

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var profile = Actor.Profile;

        return _enquiry.Put(
            ask => ask.Of(token => _ports.Engagement.Vote(profile, about, choices, token)),
            eitherWay: voted =>
            {
                Tell(new Change.PostChanged(voted));
                Say("Vote cast.", isError: false);
            });
    }

    /// <summary>
    ///     Reads the accounts <paramref name="acting" />'s profile follows into the people a post can mention (#321), a
    ///     page at a time, each offered as it lands so that an open list redraws with it.
    /// </summary>
    /// <remarks>
    ///     Not asked through the enquiry, for the reason <see cref="LimitsByInstance" /> gives: it would count a rate
    ///     limit down over the post and say a failure on the status row, and a suggestion is worth neither. A read the
    ///     rate limit stopped keeps the pages that came, and one refused keeps the people already known — silently, and
    ///     not asked again in the session. Nothing lands after a switch of profile.
    /// </remarks>
    private async Task ReadFollows(Acting acting)
    {
        var abandoned = _enquiry.Abandoned;

        try
        {
            var read = await _ports.Accounts.List(
                acting.Profile,
                FollowSide.Following,
                account: null,
                PeopleToMention.FollowsRead,
                abandoned,
                arrived: page => Offer(acting, page, abandoned));

            Apply(() => acting.People.FollowsEnded(read));
        }
        catch (OperationCanceledException) when (abandoned.IsCancellationRequested)
        {
            // Called off by a switch, and nobody left to tell.
        }
        catch (WoolyException)
        {
            // Refused, which the reads that fill the screen already say with the key that fixes it.
        }
    }

    /// <summary>
    ///     Asks the instance's search of <paramref name="acting" />'s follows for <paramref name="query" /> (#322), where
    ///     it is still worth asking once the pause has come — the follows may have finished arriving meanwhile — and
    ///     offers what it finds alongside what was known.
    /// </summary>
    /// <remarks>
    ///     Not asked through the enquiry, for the reason <see cref="ReadFollows" /> gives. A refused search leaves the
    ///     people known as they were, silently, and is not asked again for that query. Nothing lands after a switch of
    ///     profile.
    /// </remarks>
    private async Task SearchFollows(Acting acting, string query)
    {
        if (_acting != acting || !acting.People.WorthSearching(query))
        {
            return;
        }

        acting.People.SearchedFor(query);

        var abandoned = _enquiry.Abandoned;

        try
        {
            Offer(acting, await _ports.Search.FindFollowed(acting.Profile, query, abandoned), abandoned);
        }
        catch (OperationCanceledException) when (abandoned.IsCancellationRequested)
        {
            // Called off by a switch, and nobody left to tell.
        }
        catch (WoolyException)
        {
            // Refused or rate limited, which a suggestion is not worth a word on the status row about.
        }
    }

    /// <summary>
    ///     Offers <paramref name="follows" /> as people to mention, and redraws an open list with them — unless a switch
    ///     of profile has called off what found them.
    /// </summary>
    private void Offer(Acting acting, IEnumerable<Account> follows, CancellationToken abandoned) =>
        Apply(() =>
        {
            if (!abandoned.IsCancellationRequested)
            {
                acting.People.Followed(follows);
                Changed?.Invoke();
            }
        });

    /// <summary>Reads the counts the rail carries, none of which is worth failing the shell over.</summary>
    /// <param name="profile">Who the counts are read as.</param>
    /// <param name="abandoned">
    ///     Cancelled where the session switches profile, after which none of them is read or drawn (#243).
    /// </param>
    private async Task Counts(ActiveProfile profile, CancellationToken abandoned)
    {
        await Count(
            DestinationKind.Notifications,
            abandoned,
            async token => (await _ports.Notifications.Read(profile, Arrival.CountedAtMost, token)).Items.Count);

        await Count(
            DestinationKind.Messages,
            abandoned,
            async token => (await _ports.Messages.List(profile, Arrival.CountedAtMost, token))
                .Items.Count(conversation => conversation.Unread));

        await Count(
            DestinationKind.Requests,
            abandoned,
            async token => (await _ports.Accounts.PendingRequests(profile, Arrival.CountedAtMost, token)).Items.Count);
    }

    private async Task Count(
        DestinationKind kind,
        CancellationToken abandoned,
        Func<CancellationToken, Task<int>> read)
    {
        if (abandoned.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var unread = await read(abandoned);

            Apply(() =>
            {
                if (!abandoned.IsCancellationRequested)
                {
                    Counted(kind, unread);
                }
            });
        }
        catch (OperationCanceledException) when (abandoned.IsCancellationRequested)
        {
            // Called off by a switch, and nobody left to tell.
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
        Rail.Update(Badged(kind) with { Unread = unread });

    /// <summary>Moves a count on the rail by <paramref name="by" />, and never below nothing (#234).</summary>
    private void Moved(DestinationKind kind, int by) => Counted(kind, Math.Max(0, Badged(kind).Unread + by));

    /// <summary>The rail's entry for <paramref name="kind" />, badge and all.</summary>
    private Destination Badged(DestinationKind kind) =>
        Rail.Destinations.First(destination => destination.Kind == kind);

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
            Composing(new ComposeScreen(
                purpose,
                addressing: $"@{handle}",
                from: ComposeFrom.Of(Actor.Profile),
                starting: StartingDefaults(purpose, about: null)));

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

        Composing(new ComposeScreen(
            purpose,
            purpose == ComposeFor.Post ? null : about,
            purpose == ComposeFor.Reply ? Addressed(about!) : null,
            aboutIsMine: purpose == ComposeFor.Reply && IsMine(about!),
            from: ComposeFrom.Of(Actor.Profile),
            starting: StartingDefaults(purpose, about)));
    }

    /// <summary>
    ///     What a compose screen's To and Lang start on, which is what would go out if nobody touched them (ADR-0024): the
    ///     config's <c>default_visibility</c> and <c>default_language</c>, else the account's own as its instance said
    ///     them (#339), else nothing known. On a reply the visibility is the narrower of that and the post being
    ///     answered, which is where a reply with no visibility of its own goes out anyway (#338); the language is not,
    ///     since a reply is in its author's language rather than the answered post's (#340). An edit opens on the post's
    ///     own, which is the screen's to say.
    /// </summary>
    private PostDefaults StartingDefaults(ComposeFor purpose, Post? about)
    {
        var preferred = _preferred.Or(_defaults.For(Actor.Profile));

        return purpose == ComposeFor.Reply && about is { } answered
            ? preferred with
            {
                Visibility = PostAudience.Narrower(preferred.Visibility ?? answered.Visibility, answered.Visibility),
            }
            : preferred;
    }

    /// <summary>
    ///     Pushes <paramref name="compose" />, measured against its instance's own limit as far as that is known (#319).
    /// </summary>
    private void Composing(ComposeScreen compose)
    {
        compose.Limits = _limits.For(Actor.Profile);

        Push(compose);
    }

    /// <summary>
    ///     Attaches the files at <paramref name="paths" /> to <paramref name="compose" />, as many as it has room for,
    ///     and starts each going up to the instance there and then (ADR-0026, #375). Attaching changes the draft, which
    ///     spends a notice over it as typing does (#364) — and where some would not fit, the status row says how many
    ///     were left out, so that nobody is unsure what is on the post (#378).
    /// </summary>
    private void Attach(ComposeScreen compose, IReadOnlyCollection<string> paths)
    {
        IReadOnlyList<ComposeAttachment> attached = [];

        ChangeCompose(screen =>
        {
            attached = screen.Attach(paths.Select(ComposeAttachment.Of));

            return attached.Count > 0 ? ComposeChange.Edited : ComposeChange.None;
        });

        if (paths.Count - attached.Count is > 0 and var left)
        {
            var most = $"{compose.Limits.Attachments} is the most a post can carry.";

            Say(attached.Count == 0 ? $"Nothing attached — {most}" : $"{left} left out — {most}", isError: true);
        }

        foreach (var attachment in attached)
        {
            _ = SendUp(compose, attachment);
        }
    }

    /// <summary>
    ///     Takes an attachment off the compose in front, the one <paramref name="removing" /> says (#378) — and where a
    ///     send was waiting on nothing else, the send: what it waited on is no longer on the post.
    /// </summary>
    private void RemoveAttachment(Func<ComposeScreen, ComposeChange> removing)
    {
        if (ChangeCompose(removing) != ComposeChange.None && Screen is ComposeScreen compose)
        {
            Waited(compose);
        }
    }

    /// <summary>
    ///     Sends up again the attachment <paramref name="retrying" /> starts over on the compose in front, where a retry
    ///     could mend its refusal (#378) — the author's to ask for, never done by itself (ADR-0006).
    /// </summary>
    private void Retry(Func<ComposeScreen, ComposeAttachment?> retrying)
    {
        if (Screen is not ComposeScreen compose || retrying(compose) is not { } attachment)
        {
            return;
        }

        Changed?.Invoke();

        _ = SendUp(compose, attachment);
    }

    /// <summary>
    ///     A row under the Media header dragged by the pointer to the <paramref name="place" />th row, live, the others
    ///     making way (#378).
    /// </summary>
    /// <returns>Whether it moved, which makes the button's release a drop rather than a click.</returns>
    public bool DragAttachment(ComposeAttachment attachment, int place) =>
        ChangeCompose(compose => compose.ReorderTo(attachment, place)) != ComposeChange.None;

    /// <summary>
    ///     A click on <paramref name="attachment" />'s row, on <paramref name="part" /> of it (#378): its <c>x</c> takes
    ///     it off, its <c>retry (r)</c> sends it up again, and anywhere else picks it.
    /// </summary>
    public void ClickAttachment(ComposeAttachment attachment, AttachmentPart part)
    {
        switch (part)
        {
            case AttachmentPart.Remove:
                RemoveAttachment(compose => compose.Remove(attachment));

                break;
            case AttachmentPart.Retry:
                Retry(compose => compose.Retry(attachment));

                break;
            default:
                _ = ChangeCompose(compose => compose.Pick(attachment));

                break;
        }
    }

    /// <summary>
    ///     Sends <paramref name="attachment" /> up as the profile acted as, feeding where it has got to back into
    ///     <paramref name="compose" /> on the drawing thread as it hears — the screen holding what it is told and
    ///     reaching nothing itself (ADR-0015).
    /// </summary>
    /// <remarks>
    ///     Not through the enquiry: an upload goes on while the author writes, and its progress, its processing and any
    ///     refusal are the row's to say rather than the status row's — nor does it land only while the screen it was
    ///     sent from is on top, since a screen pushed over the draft does not stop it. Never retried here (ADR-0006): a
    ///     dropped connection is said on the row, and trying again is the author's. Called off with the screen.
    /// </remarks>
    private async Task SendUp(ComposeScreen compose, ComposeAttachment attachment)
    {
        var profile = Actor.Profile;

        if (!_sendingUp.TryGetValue(compose, out var sending))
        {
            _sendingUp[compose] = sending = new CancellationTokenSource();
        }

        var token = sending.Token;
        var progress = new Reporting(this, compose, attachment);
        AttachmentState landed;

        try
        {
            landed = new AttachmentState.Ready(await _ports.Author.Attach(profile, attachment.Path, progress, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (TransientNetworkException)
        {
            // The one failure sending it again could mend, so the one its row offers a retry for (#378).
            landed = new AttachmentState.Refused("connection lost", Retryable: true);
        }
        catch (AttachmentRefusedException refused)
        {
            landed = new AttachmentState.Refused(refused.Reason ?? "refused by the instance");
        }
        catch (WoolyException failed)
        {
            landed = new AttachmentState.Refused(failed.Message);
        }

        Apply(() => Progressed(compose, attachment, landed));
    }

    /// <summary>
    ///     Where <paramref name="attachment" /> on <paramref name="compose" /> has got to, heard on the drawing thread —
    ///     and, where that was the last thing a waiting send was waiting on, the send (ADR-0026, #375).
    /// </summary>
    private void Progressed(ComposeScreen compose, ComposeAttachment attachment, AttachmentState state)
    {
        if (!compose.Progressed(attachment, state))
        {
            return;
        }

        Changed?.Invoke();

        Waited(compose);
    }

    /// <summary>
    ///     The send <paramref name="compose" /> was waiting on, where it no longer has anything unfinished to wait on
    ///     (ADR-0026, #375).
    /// </summary>
    private void Waited(ComposeScreen compose)
    {
        if (ReferenceEquals(_waitingToSend, compose) && !compose.Unfinished)
        {
            _waitingToSend = null;

            // Only from the screen it was asked on, as any send is; one that was refused meanwhile is said rather than
            // sent, by the same rule as a send asked for then.
            if (ReferenceEquals(Screen, compose))
            {
                _ = Send();
            }
        }
    }

    /// <summary>Calls off whatever <paramref name="screen" /> is still sending up, as it leaves the stack (#375).</summary>
    private void LetUploadsGo(Screen screen)
    {
        if (screen is not ComposeScreen compose)
        {
            return;
        }

        if (ReferenceEquals(_waitingToSend, compose))
        {
            _waitingToSend = null;
        }

        if (_sendingUp.Remove(compose, out var sending))
        {
            sending.Cancel();
            sending.Dispose();
        }
    }

    /// <summary>
    ///     An upload's progress, handed onto the drawing thread as it is reported from wherever the HTTP stack is
    ///     (<see cref="IShellHost.OnUiThread" />) — not <see cref="Progress{T}" />, which posts to a synchronisation
    ///     context a terminal does not have.
    /// </summary>
    private sealed class Reporting(Shell shell, ComposeScreen compose, ComposeAttachment attachment)
        : IProgress<AttachmentProgress>
    {
        public void Report(AttachmentProgress value) =>
            shell.Apply(() => shell.Progressed(
                compose,
                attachment,
                value is AttachmentProgress.Sending(var done)
                    ? new AttachmentState.Sending(done)
                    : new AttachmentState.Processing()));
    }

    /// <summary>
    ///     An instance said how long it lets a post be, which a compose on that instance still in front is measured
    ///     against from now on.
    /// </summary>
    private void Measured(string instance, PostLimits limits)
    {
        if (Screen is ComposeScreen compose
            && string.Equals(_acting?.Profile.Instance, instance, StringComparison.OrdinalIgnoreCase))
        {
            compose.Limits = limits;
            Changed?.Invoke();
        }
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
    private bool IsMe(string account) => Actor.Profile.SignsInAs(account);

    private void Push(Screen screen)
    {
        _stack.Add(screen);
        Saw(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Waits to be told again before going ahead with what <paramref name="confirmation" /> carries.</summary>
    private void Confirm(Confirmation confirmation)
    {
        Asking = confirmation;

        Changed?.Invoke();
    }

    /// <summary>
    ///     Walks the stack back out to the screen <paramref name="depth" /> up it — nought, the destination's own — as
    ///     that many presses of <c>esc</c> would pop it, so the screen keeps the page, the pick and the reference picked
    ///     on it, what the screens taken off asked for is dropped with them, and nothing is asked of the instance
    ///     (#289, #308). Not <see cref="Reset" />, which lets the bottom screen go too.
    /// </summary>
    /// <remarks>
    ///     Where it is already in front it is nothing at all, the notice included: a click on where a reader already is
    ///     must leave the status row saying what it said. A draft it would take off has been asked about already, by
    ///     whatever walked back (<see cref="Leaving" />, #373).
    /// </remarks>
    private void Unwind(int depth)
    {
        if (depth < 0 || _stack.Count <= depth + 1)
        {
            return;
        }

        while (_stack.Count > depth + 1)
        {
            Leave(_stack.Count - 1);
        }

        Notice = null;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Goes ahead with <paramref name="leave" />, a way out that takes every screen above the first
    ///     <paramref name="keeping" /> off the stack — or, where a compose screen among those is touched
    ///     (<see cref="ComposeScreen.Touched" />), asks first, on the confirmation row, and goes ahead only once agreed
    ///     (#373). Every way out of a compose screen comes through here, so that none of them loses what was written
    ///     without a word.
    /// </summary>
    /// <param name="keeping">How many screens at the bottom of the stack the way out leaves standing.</param>
    /// <param name="leave">The way out.</param>
    private void Leaving(int keeping, Action leave)
    {
        if (!_stack.Skip(keeping).OfType<ComposeScreen>().Any(compose => compose.Touched))
        {
            leave();

            return;
        }

        Confirm(Confirmation.Discarding(() =>
        {
            leave();

            return Task.CompletedTask;
        }));
    }

    /// <summary>Takes the screen on top off, which is what <c>esc</c> does once nothing inside it is left to let go.</summary>
    private void Pop()
    {
        Leave(_stack.Count - 1);

        Notice = null;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Takes every compose screen off the stack and everything above it, for a way out that leaves the stack to
    ///     something else to settle — a destination arrived at, a screen pushed over it — and must not leave a draft
    ///     standing under that in the meantime. Nothing where there is none.
    /// </summary>
    /// <returns>Whether there was a draft to take off.</returns>
    private bool DropDrafts()
    {
        var at = _stack.FindIndex(screen => screen is ComposeScreen);

        if (at < 1)
        {
            return false;
        }

        while (_stack.Count > at)
        {
            Leave(_stack.Count - 1);
        }

        Notice = null;
        Changed?.Invoke();

        return true;
    }

    /// <summary>Puts the stack back to one screen, which is what arriving at a destination does.</summary>
    private void Reset(Screen screen)
    {
        _stack.ForEach(left =>
        {
            left.Left();
            LetUploadsGo(left);
        });
        _stack.Clear();
        _stack.Add(screen);
        Saw(screen);
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
        _stack[^1].Left();
        _stack[^1] = fresh;
        Saw(fresh);

        // Gone with the screen it was said over, the same as at a push or an arrival: what a reader was told about the
        // list they were looking at is not about the one in front of them now.
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Says what happened on the instance, which <see cref="Arrival.Apply" /> settles the rest of.</summary>
    private void Tell(Change change) => Actor.Arrival.Apply(change);

    /// <summary>
    ///     Tells every screen on the stack what changed, and takes off it any that are now about nothing — a post
    ///     screen whose post was deleted (#234).
    /// </summary>
    /// <remarks>
    ///     Every screen and not only the top one: a list and whatever was opened from a row on it are on the stack
    ///     together, so a row that still said what the screen above it has just changed would be the shell arguing
    ///     with itself (#181). Walked from the top, so that a stack down to one screen keeps it — the shell is never
    ///     left with nothing on it.
    /// </remarks>
    private void Heard(Change change)
    {
        if (change is Change.Tied(var tied))
        {
            _acting?.People.Tied(tied);
        }

        for (var at = _stack.Count - 1; at >= 0; at--)
        {
            if (_stack[at].Heard(change) && _stack.Count > 1)
            {
                Leave(at);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    ///     A profile has been written from the add screen: back to the list it was opened from, read again with the new
    ///     row picked — and not switched to, which is <c>⏎</c>, one key away (#245).
    /// </summary>
    /// <remarks>
    ///     A fresh list in place of the one underneath rather than the old one told, since the list is the local
    ///     config's and was read when <c>ctrl-p</c> was pressed. The rail's instance row is asked again too: a second
    ///     profile is what puts it there.
    /// </remarks>
    private void Added(AddProfileScreen adding, string name, ProfileAddition addition)
    {
        // With nobody acted as, the profile just written is who the shell starts acting as — on Home, the way a
        // launch with it would have opened (#247).
        if (_acting is null)
        {
            Act(name);

            return;
        }

        Leave(_stack.IndexOf(adding));

        // Written over the profile acted as. Signed in as the same account, that is the same person carrying on: the
        // stack stays as it is, and asks with the new token from here on (#248). As somebody else — a plain add,
        // replacing the name, which checks no account on record — it is a switch to them, starting again on Home.
        if (name == Actor.Profile.Name)
        {
            if (adding.Account is not { } account || !Actor.Profile.SignsInAs(account))
            {
                Act(name);
                Say($"Replaced profile {name}.", isError: false);

                return;
            }

            Renewed(name);
        }

        var list = Listed();

        list.Pick(name);

        _instance = RailInstance();

        if (Screen is ProfilesScreen)
        {
            Freshened(list);
        }
        else
        {
            Push(list);
        }


        var what = addition.ReplacedExisting ? "Replaced" : "Added";

        // Said after the list is up, which clears what was said over the one it replaced — in profile add's words.
        Say(
            addition.IsCurrent ? $"{what} profile {name}. {ProfileWords.ActsAs(name)}" : $"{what} profile {name}.",
            isError: false);
    }

    /// <summary>
    ///     Every profile on this machine as the profiles screen lists them, read off the local config now.
    /// </summary>
    private ProfilesScreen Listed() =>
        new(_profiles.Registry.List(), Actor.Profile.Name, _profiles.Warnings);

    /// <summary>
    ///     The instance the rail's foot names: this session's, where two or more profiles are set up and there is
    ///     somebody to tell it apart from — otherwise none (#241).
    /// </summary>
    private string? RailInstance() =>
        _acting is { } acting && _profiles.Registry.List().Count >= 2 ? acting.Profile.Instance : null;

    /// <summary>
    ///     Takes in everybody <paramref name="screen" /> shows as people a post can mention (#318), as it arrives — at
    ///     no cost, being what was already read. Nothing while nobody is being acted as.
    /// </summary>
    private void Saw(Screen screen) => _acting?.People.Saw(screen.Seen);

    /// <summary>
    ///     Takes the screen at <paramref name="at" /> off the stack, and lets it know (<see cref="Screen.Left" />).
    /// </summary>
    private void Leave(int at)
    {
        _stack[at].Left();
        LetUploadsGo(_stack[at]);
        _stack.RemoveAt(at);
    }

    private void Say(string? notice, bool isError)
    {
        Notice = notice;
        NoticeIsError = isError;

        Changed?.Invoke();
    }

    private void Apply(Action work) => _host.OnUiThread(work);

    /// <summary>
    ///     Who the session is acting as, with everything that holds what was read as them: the arrival and its cache,
    ///     what a screen can reach, and the conversations being marked read. One thing rather than four, so that a
    ///     switch puts a whole new one in place and none of it can be left behind by accident (#243). The enquiry their
    ///     questions go through is beside it rather than in it, since adding a profile asks through one with nobody
    ///     acted as at all (#247).
    /// </summary>
    /// <param name="Profile">Who is being acted as.</param>
    /// <param name="Arrival">
    ///     What bringing a screen up from its subject means, which is the same steps at every screen that is read —
    ///     arrived at, drilled into or refreshed (#100, #233).
    /// </param>
    /// <param name="Reach">What a screen can reach of this shell while it answers a verb of its own (#232).</param>
    /// <param name="Cache">What the arrival holds of what was read, kept where the profile is only signed in again.</param>
    /// <param name="People">Who a post can mention, gathered as this profile for the session (#318).</param>
    private sealed record Acting(
        ActiveProfile Profile,
        Arrival Arrival,
        Reach Reach,
        SubjectCache Cache,
        PeopleToMention People)
    {
        /// <summary>
        ///     The conversations <c>m</c> has been pressed on and not yet answered about, by id — so a second press
        ///     before the first lands marks nothing twice, and moves the badge once (#234).
        /// </summary>
        public HashSet<string> Marking { get; init; } = new(StringComparer.Ordinal);
    }
}
