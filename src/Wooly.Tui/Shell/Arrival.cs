using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     Bringing a screen up from its <see cref="Subject" />, which is one algorithm however many kinds of screen are
///     read: put up what stands for it at once, draw what is still fresh or ask for it, keep what came back, put the
///     screen where the move says, and move the badge (#100, #233).
/// </summary>
/// <remarks>
///     Three moves bring a screen up — arriving from the rail, drilling in, and refreshing in place — and they differ
///     only in what they do to the stack and whether a destination's placeholder goes up first. Everything a subject
///     differs in, it says about itself: what it reads, what that becomes, whether it is held and whether it pages. A
///     seventh kind of screen is a seventh subject rather than a seventh chance to state the sequence slightly
///     differently.
///     <para>
///         What lands leaves through <see cref="Arrives" />, <see cref="Drills" />, <see cref="Refreshes" />,
///         <see cref="Filled" /> and <see cref="Counts" /> rather than being done here: the stack and the rail are the shell's, and an arrival is what settles what goes on them.
///     </para>
///     <para>
///         The stale rule is <see cref="Enquiry" />'s, and is the same here as everywhere: what is read lands only
///         while the screen it was asked from is in front. Which is why a placeholder goes up before its question is
///         put — the question is asked from it — and why nothing here counts arrivals.
///     </para>
/// </remarks>
/// <param name="profile">Whose instance is being asked.</param>
/// <param name="ports">Everything a subject is read through.</param>
/// <param name="enquiry">What every question is put under.</param>
/// <param name="cache">What each cached subject last held, which is what makes walking the rail back free.</param>
public sealed class Arrival(ActiveProfile profile, ShellPorts ports, Enquiry enquiry, SubjectCache cache)
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

    /// <summary>Raised with what a destination's badge says, for the destinations that carry one.</summary>
    public event Action<DestinationKind, int>? Counts;

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

                return Bring(new Subject.Account(AccountAddress.Parse(account), WithReplies: false), Move.Arrive, blank);

            default:
                return Bring(new Subject.Destination(destination), Move.Arrive);
        }
    }

    /// <summary>Drills into <paramref name="subject" />, on top of whatever is showing.</summary>
    public Task Open(Subject subject) => Bring(subject, Move.Drill);

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
    ///     about the drawing thread, and it is said there — by the event that puts it up — rather than carried back across
    ///     the await to a caller that would read it too early.
    /// </remarks>
    public Task Again(Subject subject)
    {
        cache.Forget(subject);

        return Bring(subject, Move.Refresh);
    }

    /// <summary>
    ///     Reads the next page where the reader has walked onto the end of what <paramref name="showing" /> has, and
    ///     there is more of it to be had (#180).
    /// </summary>
    /// <remarks>
    ///     Asked after every walk rather than bound to a key, because the walk is what settles it: the screen answers
    ///     whether it is standing at the end of what it has with more to be had. Nothing while anything is in flight,
    ///     which on a list is the page already asked for.
    /// </remarks>
    public Task More(Screen showing) =>
        showing is { WantsMore: true, Subject: { Pages: true } subject } && !enquiry.Fetching
            ? Read(subject, Move.Page, showing)
            : Task.CompletedTask;

    /// <summary>The steps, over whatever <paramref name="subject" /> says it is.</summary>
    /// <remarks>
    ///     A subject held recently enough draws at once and asks for nothing, which is what makes walking out along
    ///     the rail and back one fetch per destination rather than one per arrival (ADR-0014).
    /// </remarks>
    /// <param name="subject">What is being brought up.</param>
    /// <param name="move">How, which settles where it goes.</param>
    /// <param name="blank">What stands for it where the subject has no placeholder of its own and one is owed.</param>
    private Task Bring(Subject subject, Move move, Screen? blank = null)
    {
        var standing = subject.Placeholder(move, profile) ?? blank;

        if (standing is not null)
        {
            Up(standing, subject, move);
        }

        if (subject.Cached && cache.Fresh(subject) is { } held)
        {
            Land(subject, move, standing, held);

            return Task.CompletedTask;
        }

        return Read(subject, move, standing);
    }

    /// <summary>Asks for it, and lands what came back while the screen it was asked from is still in front.</summary>
    private Task Read(Subject subject, Move move, Screen? standing) =>
        enquiry.Put(
            ask => subject.Read(ask, ports, profile, standing),
            ifStillHere: found =>
            {
                var screen = Land(subject, move, standing, found);

                if (subject.Cached && found.Held(screen) is { } held)
                {
                    cache.Keep(subject, held);
                }

                if (found.ReadsOn(screen))
                {
                    _ = Read(subject, Move.Page, screen);
                }
            });

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
            Counts?.Invoke(badge.Kind, badge.Unread);
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
