using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     Arriving at a destination, which is one algorithm however many destinations there are: overtake what is in
///     flight, put an empty screen up at once, draw what is still fresh or ask for it, keep what came back, put the
///     stack back to one screen, and move the badge (#100).
/// </summary>
/// <remarks>
///     Destinations differ in four things and nothing else — what a destination reads, what that becomes on screen,
///     what an empty one is told, and what it counts — so each of them says those four and this says the rest. A tenth
///     destination is four values rather than a tenth chance to state the sequence slightly differently, and a
///     destination that carries no badge says so rather than being the call site that left <c>Counted</c> out.
///     <para>
///         What lands leaves through <see cref="Shows" /> and <see cref="Counts" /> rather than being done here: the
///         stack and the rail are the shell's, and an arrival is what settles what goes on them.
///     </para>
/// </remarks>
/// <param name="profile">Whose instance is being asked.</param>
/// <param name="ports">Everything a destination is read through.</param>
/// <param name="enquiry">What every question is put under, and what an arrival overtakes the last one through.</param>
/// <param name="cache">What each destination last held, which is what makes walking the rail back free.</param>
/// <param name="host">The terminal's two services; only the hop back onto the drawing thread is wanted here.</param>
public sealed class Arrival(
    ActiveProfile profile,
    ShellPorts ports,
    Enquiry enquiry,
    DestinationCache cache,
    IShellHost host)
{
    /// <summary>
    ///     How many posts a screen asks for. A timeline's page, which is the most an instance serves in one call — so
    ///     arriving at a destination is one call rather than several, which matters most for the mechanism that can
    ///     spend the rate-limit budget by accident.
    /// </summary>
    public const int PostsWanted = 40;

    /// <summary>
    ///     How many notifications, conversations or requests are asked for: what a screen lists, and what a count
    ///     counts up to before it stops counting.
    /// </summary>
    public const int CountedAtMost = 40;

    /// <summary>Raised with the screen an arrival has become, which is what the stack is put back to.</summary>
    public event Action<Screen>? Shows;

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
    ///     Arriving somewhere that reads no list — the profile's own account, the search prompt, a hashtag nobody has
    ///     named. The overtake and the screen it stands on, which is what every arrival begins with and the whole of
    ///     these.
    /// </summary>
    public void At(Screen standing)
    {
        // Said before anything else, and by every arrival rather than only the ones that fetch. A destination that
        // asks the instance for nothing still overtakes what the last one asked for — otherwise a timeline still in
        // flight lands on top of the notice screen the reader has since walked to.
        enquiry.Arrived();

        Apply(() => Shows?.Invoke(standing));
    }

    /// <summary>Arriving at a destination that reads a list, which is the six steps and what each of them is here.</summary>
    /// <param name="destination">Where the reader is arriving.</param>
    /// <param name="resuming">
    ///     Where they were standing on the screen this one replaces, which only a refresh has any of: the same
    ///     destination asked again is somewhere they never left, so the pick follows the post it was on rather than
    ///     going back to the top (#84). An arrival from anywhere else carries no place, and a screen resuming one that
    ///     names nothing opens where it would have anyway.
    /// </param>
    public Task At(Destination destination, Place resuming = default)
    {
        // The four timeline destinations are one arrival with a different timeline in it, and which timeline that is
        // the destination already says — so there is one arm here rather than four saying the same thing about a
        // different scope.
        if (destination.Timeline is { } timeline)
        {
            return Arrive(
                destination,
                resuming,
                new Arriving<Post>(
                    Reads: token => ports.Timelines.Read(profile, timeline, PostsWanted, token),
                    // Refreshed, because this is the timeline as a destination arrived at: a tag walked to from a
                    // search is the same screen and is not one, so which it is comes from who built it (#84).
                    Becomes: (posts, notice) => new FeedScreen(destination, posts, notice, refreshes: true),
                    WhenEmpty: NothingOn(timeline),

                    // A timeline carries no badge, which is something this destination says rather than a step its
                    // arrival is missing.
                    Counting: null));
        }

        return destination.Kind switch
        {
            DestinationKind.Notifications => Arrive(
                destination,
                resuming,
                new Arriving<Notification>(
                    Reads: token => ports.Notifications.Read(profile, CountedAtMost, token),
                    Becomes: (waiting, notice) => new NotificationsScreen(waiting, notice),
                    WhenEmpty: "Nothing is waiting for you.",
                    Counting: waiting => waiting.Count)),

            DestinationKind.Requests => Arrive(
                destination,
                resuming,
                new Arriving<Account>(
                    Reads: token => ports.Accounts.PendingRequests(profile, CountedAtMost, token),
                    Becomes: (asking, notice) => new FollowRequestsScreen(asking, notice),
                    WhenEmpty: "Nobody is waiting to follow you.",
                    Counting: asking => asking.Count)),

            DestinationKind.Messages => Arrive(
                destination,
                resuming,
                new Arriving<Conversation>(
                    Reads: token => ports.Messages.List(profile, CountedAtMost, token),
                    Becomes: (written, notice) => new DirectMessagesScreen(written, notice),
                    WhenEmpty: "No direct conversations yet.",

                    // The badge counts the conversations with something unread in them, and counts them off the list
                    // it is drawn beside — so the rail cannot say two over a list of one.
                    Counting: written => written.Count(conversation => conversation.Unread))),

            // Said out loud rather than quietly doing nothing, which would be the trap this module was built to close:
            // a destination that reads no list — the profile's own account, the prompt, the hashtag nobody has named —
            // arrives through the overload above, and a tenth that reads none belongs there too. Landing here is a
            // destination nobody said what to do with, and a shell that swallowed it would draw the last screen again.
            _ => throw new ArgumentOutOfRangeException(
                nameof(destination),
                destination.Kind,
                "Not a destination that reads a list."),
        };
    }

    /// <summary>The six steps, over whatever <paramref name="arriving" /> says this destination is.</summary>
    /// <remarks>
    ///     A destination fetched recently enough draws at once and asks for nothing, which is what makes walking out
    ///     along the rail and back one fetch per destination rather than one per arrival (ADR-0014).
    /// </remarks>
    private async Task Arrive<T>(Destination destination, Place resuming, Arriving<T> arriving)
    {
        At(arriving.Becomes([], null));

        if (cache.Fresh<T>(destination.Kind) is { } held)
        {
            // What was held is an answer that cost nothing, and nothing cut it short.
            Apply(() => Landed(destination, arriving, Fetch<T>.Complete(held), resuming));

            return;
        }

        await enquiry.Put(
            ask => ask.Of(arriving.Reads),
            ifStillHere: fetch =>
            {
                cache.Keep(destination.Kind, fetch.Items);
                Landed(destination, arriving, fetch, resuming);
            });
    }

    /// <summary>
    ///     What both halves of an arrival end with: the screen on the stack, and the badge beside it read off the same
    ///     answer — so the rail cannot say four over a list of three.
    /// </summary>
    private void Landed<T>(Destination destination, Arriving<T> arriving, Fetch<T> answer, Place resuming)
    {
        // What the list is of is the destination's own to say, and only a timeline has a name worth putting in the
        // sentence — so it is read off the destination rather than being a fifth thing an arrival states about itself.
        var notice = Emptiness(
            answer.Items.Count,
            arriving.WhenEmpty,
            destination.Timeline?.Description,
            answer.StoppedBy);

        // Where the reader was standing is put on the screen before it goes up, so that the stack is never briefly
        // holding a screen opened at the top of a list the reader was half way down (#84).
        var screen = arriving.Becomes(answer.Items, notice);

        screen.Resume(resuming);

        Shows?.Invoke(screen);

        if (arriving.Counting is { } counting)
        {
            Counts?.Invoke(destination.Kind, counting(answer.Items));
        }
    }

    private void Apply(Action work) => host.OnUiThread(work);

    /// <summary>
    ///     What arriving at one destination means, as the four things that differ between them and nothing else.
    /// </summary>
    /// <param name="Reads">What the instance is asked for.</param>
    /// <param name="Becomes">What the answer is on screen, given what came back and what there is to say about it.</param>
    /// <param name="WhenEmpty">What a reader is told where nothing came back.</param>
    /// <param name="Counting">
    ///     What this destination's badge counts off the answer, or <see langword="null" /> where it carries no badge —
    ///     which a timeline says here rather than leaving the count out at its own call site.
    /// </param>
    private sealed record Arriving<T>(
        Func<CancellationToken, Task<Fetch<T>>> Reads,
        Func<IReadOnlyList<T>, string?, Screen> Becomes,
        string WhenEmpty,
        Func<IReadOnlyList<T>, int>? Counting);
}
