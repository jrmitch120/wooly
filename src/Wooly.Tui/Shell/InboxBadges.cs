using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     The <b>Inbox</b>'s three badges on the rail, kept live for whoever is acted as: read again by the poll and the
///     stream, put on the rail, and the bell rung where one rose (GLOSSARY.md, ADR-0027, #398, #399, #401).
/// </summary>
/// <remarks>
///     Everything about a badge that is not the screen it sits beside: the last count of each, which the next is told
///     to have risen against; the session markers notifications are counted from; and the streaming that says when to
///     read them again. What a re-read found goes on the screen in front through the shell, whose screen it is — told
///     through <c>landed</c>, and nothing here touches the stack. Only ever touched on the drawing thread, but for the
///     reads themselves.
/// </remarks>
internal sealed class InboxBadges
{
    private readonly ShellPorts _ports;
    private readonly Rail _rail;
    private readonly IShellHost _host;
    private readonly TimeProvider _clock;
    private readonly ShellTiming _timing;

    /// <summary>Whether a rising badge rings the terminal's bell: the config file's <c>bell</c> (#399).</summary>
    private readonly bool _ringing;

    private readonly Landing _landing;

    /// <summary>Whether a destination's screen is in front, for a re-read to read what that screen draws too (#458).</summary>
    private readonly Func<DestinationKind, bool> _showing;

    /// <summary>
    ///     What keeps the Inbox live for the profile acted as — the user stream, and the poll while it is down — from
    ///     the moment the shell opens onto it until it is let go of (#398, #401). Nothing, with nobody to act as.
    /// </summary>
    private InboxStreaming? _streaming;

    /// <summary>
    ///     The last count of each Inbox badge for the profile acted as, since the shell opened onto it — what the next
    ///     count is told to have risen against (#399). The instance's, as it last said, or as it would say now after a
    ///     change made here that moved the badge by a known amount; never the rail's guess under <c>40+</c>. Only a
    ///     badge already counted can rise, which is what keeps the first counts — at launch, and after a switch —
    ///     quiet.
    /// </summary>
    private readonly Dictionary<DestinationKind, Found.Badge> _last = [];

    /// <summary>
    ///     How many counts have been asked for or set, across every badge — each count's place in line, taken as it is
    ///     asked for. Taken off whichever thread a re-read is on, so only ever moved atomically.
    /// </summary>
    private long _asked;

    /// <summary>
    ///     The place in line of the newest count of each badge to have landed. A re-read asked for before it is out of
    ///     date by the time it lands — the poll, a stream gathering, a list read and a change can each have one of the
    ///     same badge in flight — and is dropped rather than putting an older count back for the next re-read to rise
    ///     from and ring for again (#399, #401).
    /// </summary>
    private readonly Dictionary<DestinationKind, long> _newestLanded = [];

    /// <param name="ports">What a count is read through.</param>
    /// <param name="rail">Where the badges are drawn.</param>
    /// <param name="host">The terminal: its drawing thread, and its bell.</param>
    /// <param name="clock">What the poll and the stream's waits run on.</param>
    /// <param name="timing">How long those waits are.</param>
    /// <param name="ringing">Whether a rising badge rings the bell: the config file's <c>bell</c> (#399).</param>
    /// <param name="landed">
    ///     Where what a re-read found lands: the shell, which puts it on the screen in front where that is the
    ///     destination's (#400).
    /// </param>
    /// <param name="showing">
    ///     Whether a destination's screen is in front with no list read of its own in flight to fill it — asked as a
    ///     re-read is asked for, on the drawing thread — so that a re-read of Notifications reads its groups too where
    ///     they are drawn (#458).
    /// </param>
    public InboxBadges(
        ShellPorts ports,
        Rail rail,
        IShellHost host,
        TimeProvider clock,
        ShellTiming timing,
        bool ringing,
        Landing landed,
        Func<DestinationKind, bool> showing)
    {
        _ports = ports;
        _rail = rail;
        _host = host;
        _clock = clock;
        _timing = timing;
        _ringing = ringing;
        _landing = landed;
        _showing = showing;
    }

    /// <summary>
    ///     Puts what a re-read of <paramref name="badge" />'s destination found on the screen in front, where that is
    ///     the destination's (#400).
    /// </summary>
    /// <param name="badge">What the re-read counted.</param>
    /// <param name="found">What it found waiting there.</param>
    /// <param name="rose">Whether the count is higher than the last one.</param>
    /// <returns>
    ///     The newest notification on screen, where something was put on top of Notifications — for the read marker
    ///     to be moved past it — or <see langword="null" />.
    /// </returns>
    public delegate string? Landing(Found.Badge badge, InboxList found, bool rose);

    /// <summary>Where each profile whose account has no read marker counts unread notifications from (story 8).</summary>
    public SessionMarkers SessionMarkers { get; } = new();

    /// <summary>
    ///     Keeps the Inbox live for whoever is acted as, until a switch abandons <paramref name="abandoned" /> and lets
    ///     it go with everything else the old profile had: the user stream, polling while it is down (#398, #401).
    /// </summary>
    /// <param name="acting">
    ///     Who is acted as when a count is read again, rather than when this was started — so that a profile signed in
    ///     again (#248), the same enquiry with a new token, is read with its new token.
    /// </param>
    /// <param name="abandoned">Cancelled where the session switches profile.</param>
    public void KeepLive(Func<ActiveProfile?> acting, CancellationToken abandoned)
    {
        _streaming?.Dispose();
        _streaming = new InboxStreaming(
            _ports.Stream,
            acting,
            _clock,
            _timing,
            _host,
            abandoned,
            reread: kinds =>
            {
                // Never told once the switch has abandoned it: the streaming is let go of with the profile.
                if (acting() is { } profile)
                {
                    _ = Reread(profile, abandoned, kinds);
                }
            });
    }

    /// <summary>
    ///     Lets go of the profile acted as: its stream and poll stop, and its counts are forgotten, so that the next
    ///     profile's first counts are quiet (#399).
    /// </summary>
    public void LetGo()
    {
        _streaming?.Dispose();
        _streaming = null;
        _last.Clear();
        _newestLanded.Clear();
    }

    /// <summary>
    ///     Reads the Inbox badges <paramref name="kinds" /> again as one re-read: every badge it reads tells the same
    ///     <see cref="BellOnce" /> where it rose, so the bell rings at most once however many did (#399). What the
    ///     opening counts, the poll, a stream event and a change that leaves a count unknown all read through (#398,
    ///     #401). None of them is worth failing the shell over.
    /// </summary>
    /// <param name="profile">Who the counts are read as.</param>
    /// <param name="abandoned">
    ///     Cancelled where the session switches profile, after which none of them is read or drawn (#243).
    /// </param>
    /// <param name="kinds">Which badges, in the Inbox's order.</param>
    public async Task Reread(
        ActiveProfile profile,
        CancellationToken abandoned,
        IReadOnlyList<DestinationKind> kinds)
    {
        var once = new BellOnce(Ring);

        foreach (var kind in kinds)
        {
            await Count(profile, kind, abandoned, once);
        }
    }

    /// <summary>
    ///     A count arriving at the destination read, put on the rail — and the bell rung where it rose. Newer than any
    ///     re-read still in flight, which its list and count were read after or alongside.
    /// </summary>
    public void Arrived(Found.Badge badge)
    {
        Lands(badge.Kind, Ask());
        Take(badge, new BellOnce(Ring));
    }

    /// <summary>
    ///     Moves a count on the rail by <paramref name="by" />, for a change made here — and never below nothing
    ///     (#234). The count moved is what the instance would now say, so the next count rises against it.
    /// </summary>
    /// <remarks>
    ///     Not under <c>40+</c>, the only badge shape that says "at least": forty or more, less one, is no number the
    ///     badge can draw — <c>39+</c> is not a shape it has. It goes on saying <c>40+</c>, and the count is read again
    ///     for what it is now. Nothing newer has been seen under it, so that re-read cannot ring: what was newest may
    ///     well be what was just answered, and the one past the forty moving up into its place is nothing arriving.
    /// </remarks>
    /// <param name="profile">Who a count read again is read as.</param>
    /// <param name="abandoned">Cancelled where the session switches profile.</param>
    /// <param name="kind">Which badge.</param>
    /// <param name="by">How far.</param>
    public void Moved(ActiveProfile profile, CancellationToken abandoned, DestinationKind kind, int by)
    {
        if (Badged(kind).Full)
        {
            if (_last.TryGetValue(kind, out var last))
            {
                _last[kind] = last with { Newest = null };
            }

            _ = Reread(profile, abandoned, [kind]);

            return;
        }

        var moved = new Found.Badge(kind, Math.Max(0, Badged(kind).Unread + by));

        // Newer than a re-read still in flight, which may have been read before the change was made.
        Lands(kind, Ask());

        if (_last.ContainsKey(kind))
        {
            _last[kind] = moved;
        }

        Counted(moved);
    }

    /// <summary>
    ///     Reads one Inbox badge the way <see cref="InboxCount" /> counts it — the same way arriving at the
    ///     destination counts it, so the two cannot disagree (#396) — and puts it on the rail.
    /// </summary>
    /// <param name="once">The bell of the re-read this count is part of, told where the badge rose.</param>
    private async Task Count(ActiveProfile profile, DestinationKind kind, CancellationToken abandoned, BellOnce once)
    {
        if (abandoned.IsCancellationRequested)
        {
            return;
        }

        var place = Ask();
        var showing = _showing(kind);

        try
        {
            var (badge, found) = await new InboxCount(_ports, profile, SessionMarkers, abandoned)
                .Read(kind, withGroups: showing);

            _host.OnUiThread(() =>
            {
                if (abandoned.IsCancellationRequested || !Lands(kind, place))
                {
                    return;
                }

                var rose = Take(badge, once);

                // One that came back full has more unread past what it read, which are on no screen: the marker is
                // left where it is rather than moved past them unseen, and the badge goes on saying at least that
                // many. One that read exactly as many as are counted put everything unread on screen, and moves it.
                if (_landing(badge, found, rose) is { } newest && !badge.Full)
                {
                    _ = MarkerPast(profile, newest, abandoned);
                }

                // Begun before Notifications came in front, so it read no groups — and what is unread is never made
                // into groups of one here, keys an instance that groups does not have (#458). Read again now it is in
                // front, groups and all, for what arrived to join the group it belongs in.
                if (found is InboxList.Notifications { Groups: null, Unread.Count: > 0 } && _showing(kind))
                {
                    _ = Reread(profile, abandoned, [kind]);
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
    ///     Moves the read marker to <paramref name="newest" />, which the reader has in front of them, and clears the
    ///     badge where the instance took the move — nothing is newer than what is on top of the screen. A move not
    ///     taken leaves the badge saying what was waiting, which is still true (#397).
    /// </summary>
    private async Task MarkerPast(ActiveProfile profile, string newest, CancellationToken abandoned)
    {
        try
        {
            if (await _ports.Notifications.MoveMarker(profile, newest, abandoned))
            {
                _host.OnUiThread(() =>
                {
                    if (!abandoned.IsCancellationRequested)
                    {
                        // The instance's count now, the move taken: the next notification to arrive rises from it.
                        var none = new Found.Badge(DestinationKind.Notifications, 0);

                        Lands(DestinationKind.Notifications, Ask());
                        _last[DestinationKind.Notifications] = none;
                        Counted(none);
                    }
                });
            }
        }
        catch (OperationCanceledException) when (abandoned.IsCancellationRequested)
        {
            // Called off by a switch, and nobody left to tell.
        }
    }

    /// <summary>
    ///     Takes a count the instance gave: puts it on the rail, and tells <paramref name="once" /> where it is higher
    ///     than the last count of the same badge for this profile (<see cref="_last" />) — not the rail's, which a
    ///     change made here may have moved. The first count of each badge since the profile was opened onto is never
    ///     higher than anything: it is what there was, not what arrived (#399).
    /// </summary>
    /// <remarks>
    ///     Whatever read it — the open, a poll, arriving at the destination, a stream event — rather than which one,
    ///     so a poll that happens to land before a slow opening count is the first, and stays quiet.
    ///     <para>
    ///         A badge already full that comes back full again cannot count higher, so it has risen where what is
    ///         newest has changed: something arrived above what the last count saw (story 27 of #395).
    ///     </para>
    /// </remarks>
    /// <param name="badge">The count.</param>
    /// <param name="once">The bell of the re-read the count is part of.</param>
    /// <returns>
    ///     Whether it is higher than the badge was — against the rail, for a first count — which makes what the
    ///     destination held out of date.
    /// </returns>
    private bool Take(Found.Badge badge, BellOnce once)
    {
        var counted = _last.TryGetValue(badge.Kind, out var last);
        var was = last ?? new Found.Badge(badge.Kind, Badged(badge.Kind).Unread);
        var newer = was.Full
                    && badge.Full
                    && badge.Newest is { } newest
                    && was.Newest is { } before
                    && !string.Equals(newest, before, StringComparison.Ordinal);
        var rose = badge.Unread > was.Unread || newer;

        _last[badge.Kind] = badge;

        if (counted && rose)
        {
            once.Rose();
        }

        Counted(badge);

        return rose;
    }

    /// <summary>A place in line for a count about to be asked for, or set (<see cref="_asked" />).</summary>
    private long Ask() => Interlocked.Increment(ref _asked);

    /// <summary>
    ///     Lands a count of <paramref name="kind" /> asked for at <paramref name="place" />, unless one asked for after it
    ///     has landed already — which makes it out of date.
    /// </summary>
    /// <returns>Whether it landed.</returns>
    private bool Lands(DestinationKind kind, long place)
    {
        if (_newestLanded.GetValueOrDefault(kind) > place)
        {
            return false;
        }

        _newestLanded[kind] = place;

        return true;
    }

    /// <summary>The terminal's bell, where the preference leaves it on (#399).</summary>
    private void Ring()
    {
        if (_ringing)
        {
            _host.Ring();
        }
    }

    /// <summary>
    ///     Puts a count on the rail. Said in one place, because the badge is written from three: a re-read, arriving
    ///     at the destination itself, and clearing something off it — and a badge that disagreed with the list under it
    ///     would be the shell arguing with itself.
    /// </summary>
    private void Counted(Found.Badge badge) =>
        _rail.Update(Badged(badge.Kind) with { Unread = badge.Unread, Full = badge.Full && badge.Unread > 0 });

    /// <summary>The rail's entry for <paramref name="kind" />, badge and all.</summary>
    private Destination Badged(DestinationKind kind) =>
        _rail.Destinations.First(destination => destination.Kind == kind);
}
