using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     Bringing a screen up from its <see cref="Subject" />, which is one algorithm however many kinds of screen are
///     read: put up what stands for it at once, draw what is still fresh or ask for it, keep what came back, put the
///     screen where the move says, and move the badge (#100, #233). And the other side of that: what a change made on
///     the instance makes stale, which is one table here rather than a guess at every verb (#234).
/// </summary>
/// <remarks>
///     Three moves bring a screen up — arriving from the rail, drilling in, and refreshing in place — and they differ
///     only in what they do to the stack and whether a destination's placeholder goes up first. Everything a subject
///     differs in, it says about itself: what it reads, what that becomes, whether it is held and whether it reads on. A
///     seventh kind of screen is a seventh subject rather than a seventh chance to state the sequence slightly
///     differently.
///     <para>
///         What lands leaves through <see cref="Arrives" />, <see cref="Drills" />, <see cref="Refreshes" />,
///         <see cref="Filled" /> and <see cref="Counts" /> rather than being done here: the stack and the rail are the
///         shell's, and an arrival is what settles what goes on them.
///     </para>
///     <para>
///         The stale rule is <see cref="Enquiry" />'s, and is the same here as everywhere: what is read lands only
///         while the screen it was asked from is in front. Which is why a placeholder goes up before its question is
///         put — the question is asked from it — and why nothing here counts arrivals.
///     </para>
///     <para>
///         A badge is set outright by every read of its destination, counted as <see cref="InboxCount" /> says, and
///         moved — or read again — by the changes between reads (<see cref="Apply" />) — so an answer landing after
///         the reader has gone elsewhere still reaches the badge, rather than a count taken off a list nobody is
///         holding any more.
///     </para>
/// </remarks>
/// <param name="profile">Whose instance is being asked.</param>
/// <param name="ports">Everything a subject is read through.</param>
/// <param name="enquiry">What every question is put under.</param>
/// <param name="cache">What each cached subject last held, which is what makes walking the rail back free.</param>
/// <param name="showing">Which destination the rail is showing, which a post sent there is on.</param>
/// <param name="sessionMarkers">
///     Where an account with no read marker counts its notifications from this session, which arriving at
///     Notifications counts against (story 8).
/// </param>
public sealed class Arrival(
    ActiveProfile profile,
    ShellPorts ports,
    Enquiry enquiry,
    SubjectCache cache,
    Func<DestinationKind> showing,
    SessionMarkers sessionMarkers)
{
    /// <summary>
    ///     How many posts a screen asks for. A timeline's page, which is the most an instance serves in one call — so
    ///     arriving at a destination is one call rather than several, which matters most for the mechanism that can
    ///     spend the rate-limit budget by accident.
    /// </summary>
    public const int PostsWanted = 40;

    /// <summary>
    ///     How many notifications, conversations, requests or suggestions are asked for: what a screen lists — and,
    ///     where the destination carries a badge, what its count counts up to before it stops counting.
    /// </summary>
    /// <remarks>
    ///     Discover asks for this many and carries no badge, which is the pairing coming apart rather than a fifth
    ///     number: a screen showing forty of forty has nothing left to page through, and nothing on it is waiting for
    ///     anybody (#181).
    /// </remarks>
    public const int CountedAtMost = 40;

    /// <summary>Raised with a screen arrived at from the rail, which is what the stack is put back to.</summary>
    public event Action<Screen>? Arrives;

    /// <summary>Raised with a screen drilled into, which goes on top of what is showing.</summary>
    public event Action<Screen>? Drills;

    /// <summary>Raised with a fresher copy of what is showing, which stands in its place.</summary>
    public event Action<Screen>? Refreshes;

    /// <summary>Raised where an answer was read into the screen already showing rather than into a new one.</summary>
    public event Action? Filled;

    /// <summary>
    ///     Raised with what a destination's badge says, for the destinations that carry one: how many, and whether the
    ///     read that counted them came back full (<see cref="Found.Badge.Full" />).
    /// </summary>
    internal event Action<Found.Badge>? Counts;

    /// <summary>Raised with how far a destination's badge moves, for a change made between reads of it.</summary>
    public event Action<DestinationKind, int>? Moves;

    /// <summary>
    ///     Raised with a destination whose badge a change has made unknown, for it to be read again the way the shell
    ///     reads it on opening (<see cref="InboxCount" />) — where what changed cannot say how far the count moved.
    /// </summary>
    public event Action<DestinationKind>? Rereads;

    /// <summary>Raised with a change, for every screen on the stack to hear (<see cref="Screen.Heard" />).</summary>
    public event Action<Change>? Heard;

    /// <summary>
    ///     Raised, on the drawing thread, with a destination whose own list is being read — arrived at or refreshed,
    ///     and not paged — as the read is put (<see cref="ListReads" />).
    /// </summary>
    internal event Action<DestinationKind>? ListReading;

    /// <summary>
    ///     Raised with that destination once its list read is over, landed or not — from whichever thread the read
    ///     finished on, after its answer has been handed to the drawing thread.
    /// </summary>
    internal event Action<DestinationKind>? ListRead;

    /// <summary>
    ///     What a list that came back with little or nothing on it is told. A rate limit that stopped the read part
    ///     way through is said out loud rather than drawn as an empty list, which is the whole reason a fetch reports
    ///     what stopped it (ADR-0007): a reader told "nothing is waiting" would believe it.
    /// </summary>
    /// <param name="howMany">How many came back.</param>
    /// <param name="whenEmpty">What a reader is told where none did.</param>
    /// <param name="of">
    ///     What the list is of, where the destination has a name worth saying — a timeline, which is one of several a
    ///     reader walks between. An inbox names nothing, because there is only ever the one.
    /// </param>
    /// <param name="stoppedBy">The rate limit that cut the read short, or <see langword="null" /> where none did.</param>
    public static string? Emptiness(int howMany, string whenEmpty, string? of, RateLimitedException? stoppedBy)
    {
        if (stoppedBy is not null)
        {
            return of is null
                ? "Rate limited part way through — this is what arrived."
                : $"Rate limited part way through — this is what arrived of {of}.";
        }

        return howMany == 0 ? whenEmpty : null;
    }

    /// <summary>
    ///     What a timeline with nothing on it is told, wherever one is drawn: the rail's own four, and a hashtag
    ///     walked to from a search or a reference.
    /// </summary>
    public static string NothingOn(Timeline timeline) => $"Nothing on {timeline.Description} yet.";

    /// <summary>
    ///     Arriving at a destination, which is what moving the rail's selection means. Every destination that reads a
    ///     list is a <see cref="Subject.Destination" />, and the profile's own account an
    ///     <see cref="Subject.Account" />; the other two read nothing, and are the screen they stand on.
    /// </summary>
    /// <remarks>
    ///     Walking to a destination is arriving somewhere, so whatever was drilled into from the last one is left
    ///     behind: the stack is where you went from here, and this is a different here. Every arrival puts a screen
    ///     up at once, the ones that read nothing included — which is what drops whatever the last one asked for,
    ///     since that was asked from a screen no longer in front.
    /// </remarks>
    public Task At(Destination destination)
    {
        switch (destination)
        {
            // A prompt, which asks the instance for nothing until something has been typed into it.
            case { Kind: DestinationKind.Search }:
                Arrives?.Invoke(new SearchScreen());

                return Task.CompletedTask;

            // A rail entry for a hashtag nobody has named has nothing to ask about, so what stands here is the line
            // that would name one rather than an empty timeline.
            case { Kind: DestinationKind.Hashtag, Timeline: null }:
                Arrives?.Invoke(new NoticeScreen(
                    "Hashtag",
                    "No hashtag is set for the rail.",
                    """Put hashtag = "cats" under [preferences] in your config file to keep one here."""));

                return Task.CompletedTask;

            // The profile's own account, which is one account rather than a list of anything — standing on an empty
            // screen under the destination's own name until it arrives, as every arrival does.
            case { Kind: DestinationKind.Profile }:
                var blank = new FeedScreen(destination, []);

                if (profile.Account is not { } account)
                {
                    Arrives?.Invoke(blank);

                    return Task.CompletedTask;
                }

                var own = new Subject.Account(AccountAddress.Parse(account), WithReplies: false);

                return Bring(own, Move.Arrive, blank);

            default:
                return Bring(new Subject.Destination(destination, sessionMarkers), Move.Arrive);
        }
    }

    /// <summary>Drills into <paramref name="subject" />, on top of whatever is showing.</summary>
    /// <param name="subject">What is opened.</param>
    /// <param name="beneath">
    ///     What is done to the screen showing just as <paramref name="subject" /> goes up over it, and not before — so
    ///     that what <c>esc</c> will come back to changes in the same frame the subject arrives in, rather than being
    ///     drawn on its own while the subject is read. Never done where the subject never arrives.
    /// </param>
    public Task Open(Subject subject, Action? beneath = null) => Bring(subject, Move.Drill, beneath: beneath);

    /// <summary>
    ///     Stands <paramref name="subject" /> in place of what is showing — the other side of a follow list, the other
    ///     run of an account — drawing what it last held where that is still fresh.
    /// </summary>
    public Task Swap(Subject subject) => Bring(subject, Move.Refresh);

    /// <summary>
    ///     Asking the subject the reader is already on for what is there now: what it last held forgotten, and the
    ///     same read it was brought up by stood in its place (#84).
    /// </summary>
    /// <remarks>
    ///     Answers with nothing, and deliberately: the screen lands inside a callback the host runs on the drawing
    ///     thread, which is after the task this hands back has already completed. Whether a screen went up is a fact
    ///     about the drawing thread, and it is said there — by the event that puts it up — rather than carried back
    ///     across the await to a caller that would read it too early.
    /// </remarks>
    /// <param name="subject">What is read again.</param>
    /// <param name="landed">
    ///     What is done once the fresh copy is up, on the drawing thread — which is how something said about why it was
    ///     read again outlasts the fresh copy taking down whatever was said over the old one (#406). Never done where
    ///     it never lands.
    /// </param>
    public Task Again(Subject subject, Action? landed = null)
    {
        cache.Forget(subject);

        return Bring(subject, Move.Refresh, landed: landed);
    }

    /// <summary>
    ///     <b>Reads on</b> the list <paramref name="showing" /> holds, which the page has come within a screenful of the
    ///     end of (#469): the instance asked for what stands behind the last thing held, and that put under it.
    /// </summary>
    /// <remarks>
    ///     Only on a screen whose list reads on and is not whole, and never while a read on is already out for it — one
    ///     at a time per screen, however much the reader walks about near the end. The answer lands on the screen it
    ///     was asked from and is added to it rather than put up as a new one, and only while that screen is in front:
    ///     the stale rule every enquiry keeps. A read dropped by it costs nothing, since where to read on from has not
    ///     moved, and the page still near the end on coming back asks again. A read that fails is the same (#462): the
    ///     list is left as it was, the foot says it couldn't read on and the enquiry's notice says why, and the next
    ///     approach to the end asks again from the same place — the reader's walking, never a timer.
    ///     <para>
    ///         What lands is added to what the cache holds for the subject too, with where to read on from next, so a
    ///         list arrived at again holds everything read on rather than only its first read (#463).
    ///     </para>
    /// </remarks>
    public Task ReadOn(Screen showing)
    {
        if (showing is not { Subject: { } subject, ReadingOn: { } reading } || reading.Begin() is not { } from)
        {
            return Task.CompletedTask;
        }

        return enquiry.Put(
            ask => subject.ReadOn(ask, ports, profile, showing, from),
            ifStillHere: found =>
            {
                Land(subject, Move.Page, showing, found);

                if (subject.Cached)
                {
                    cache.Add(subject, found);
                }
            },
            ended: reading.Ended,
            failed: reading.Refused);
    }

    /// <summary>
    ///     What <paramref name="change" /> makes stale: the cached lists it is no longer true of, the badge it moves,
    ///     and every screen on the stack, which hears it (#234).
    /// </summary>
    /// <remarks>
    ///     The cache is asked what it holds rather than told which destination was showing, because a post is on
    ///     whichever lists it is on: one marked on Home is the same post on a Local list held a minute ago. A sent
    ///     post is the exception, being on no list yet — so it is Home, which is the profile's own following, and the
    ///     destination it was sent from. Everything else is its own destination's.
    ///     <para>
    ///         A badge moves by what changed and never below nothing, and is set outright again by the next read of
    ///         its destination — so what the rail says and the list under it are the same fact however they arrived.
    ///     </para>
    /// </remarks>
    public void Apply(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                cache.Forget(held => held.HoldsPost(post.Id));

                break;

            case Change.PostGone(var id):
                cache.Forget(held => held.HoldsPost(id));

                break;

            case Change.PostSent:
                Forget(DestinationKind.Home);
                Forget(showing());

                break;

            // Home is the profile's own following, so a follow or a block changes what belongs on it — and a mute
            // changes what belongs on all of them. Discover too, wherever the tie was made: an instance never suggests
            // somebody already followed or blocked (#181).
            case Change.Tied(var account):
                Forget(DestinationKind.Home);
                Forget(DestinationKind.Discover);
                cache.Forget(held => held.HoldsAccount(account.Id));

                break;

            // Read again rather than moved: whether a dismissed notification was waiting is where it stood against
            // the read marker, which only the instance knows (ADR-0027).
            case Change.NotificationGroupGone:
                Forget(DestinationKind.Notifications);
                Rereads?.Invoke(DestinationKind.Notifications);

                break;

            case Change.AllNotificationsGone:
                Forget(DestinationKind.Notifications);
                Counts?.Invoke(new Found.Badge(DestinationKind.Notifications, 0));

                break;

            case Change.RequestAnswered:
                Forget(DestinationKind.Requests);
                Moves?.Invoke(DestinationKind.Requests, -1);

                break;

            case Change.SuggestionDismissed:
                Forget(DestinationKind.Discover);

                break;

            case Change.ConversationMarked:
                Forget(DestinationKind.Messages);
                Moves?.Invoke(DestinationKind.Messages, -1);

                break;

            // Wherever the own account is held — a follow list it is on, a list carrying a post it wrote or boosted, whose
            // byline says its name and draws its avatar — it is held as it was before the save (#406).
            case Change.PublicProfileSaved(var account):
                cache.Forget(held => held.HoldsAccount(account.Id) || held.HoldsPostsBy(account.Address));

                break;
        }

        Heard?.Invoke(change);

        void Forget(DestinationKind kind) => cache.Forget(new Subject.Destination(kind));
    }

    /// <summary>The steps, over whatever <paramref name="subject" /> says it is.</summary>
    /// <remarks>
    ///     A subject held recently enough draws at once and asks for nothing, which is what makes walking out along
    ///     the rail and back one fetch per destination rather than one per arrival (ADR-0014).
    /// </remarks>
    /// <param name="subject">What is being brought up.</param>
    /// <param name="move">How, which settles where it goes.</param>
    /// <param name="blank">What stands for it where the subject has no placeholder of its own and one is owed.</param>
    /// <param name="beneath">What is done to the screen showing as the first screen for the subject goes up.</param>
    /// <param name="landed">What is done once what was read is up (<see cref="Again" />).</param>
    private Task Bring(
        Subject subject,
        Move move,
        Screen? blank = null,
        Action? beneath = null,
        Action? landed = null)
    {
        var standing = subject.Placeholder(move, profile) ?? blank;

        if (standing is not null)
        {
            beneath?.Invoke();
            beneath = null;
            Up(standing, subject, move);
        }

        if (subject.Cached && cache.Fresh(subject) is { } held)
        {
            beneath?.Invoke();
            Land(subject, move, standing, held);
            landed?.Invoke();

            return Task.CompletedTask;
        }

        return Read(subject, move, standing, beneath, landed);
    }

    /// <summary>Asks for it, and lands what came back while the screen it was asked from is still in front.</summary>
    private async Task Read(
        Subject subject,
        Move move,
        Screen? standing,
        Action? beneath = null,
        Action? landed = null)
    {
        DestinationKind? listing = move != Move.Page && subject is Subject.Destination { Kind: var kind } ? kind : null;

        if (listing is { } reading)
        {
            ListReading?.Invoke(reading);
        }

        try
        {
            await enquiry.Put(
                ask => subject.Read(ask, ports, profile, standing),
                ifStillHere: found =>
                {
                    beneath?.Invoke();

                    var screen = Land(subject, move, standing, found);

                    if (subject.Cached && found.Held(screen) is { } held)
                    {
                        cache.Keep(subject, held);
                    }

                    landed?.Invoke();

                    if (found.ReadsOn(screen))
                    {
                        _ = Read(subject, Move.Page, screen);
                    }
                });
        }
        finally
        {
            if (listing is { } read)
            {
                ListRead?.Invoke(read);
            }
        }
    }

    /// <summary>
    ///     What every answer ends with: the screen where the move puts it, and the badge beside it read off the same
    ///     answer — so the rail cannot say four over a list of three.
    /// </summary>
    private Screen Land(Subject subject, Move move, Screen? standing, Found found)
    {
        var screen = found.Becomes(standing);

        if (ReferenceEquals(screen, standing))
        {
            Filled?.Invoke();
        }
        else
        {
            Up(screen, subject, move);
        }

        if (found.Counted is { } badge)
        {
            Counts?.Invoke(badge);
        }

        return screen;
    }

    /// <summary>Puts <paramref name="screen" /> up, as the screen <paramref name="subject" /> is read into.</summary>
    private void Up(Screen screen, Subject subject, Move move)
    {
        screen.Subject = subject;

        (move switch
        {
            Move.Arrive => Arrives,
            Move.Drill => Drills,
            _ => Refreshes,
        })?.Invoke(screen);
    }
}
