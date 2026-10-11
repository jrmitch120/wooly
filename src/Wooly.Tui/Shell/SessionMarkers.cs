using System.Collections.Concurrent;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Where this session counts unread notifications from for each profile whose account has no <b>Read marker</b>:
///     its <see cref="SessionMarker" />, set where its count was first read (CONTEXT.md, ADR-0027, story 8 of #395).
/// </summary>
/// <remarks>
///     No marker means nothing unread this client can prove — but what arrives while the shell runs has plainly not
///     been read, so it counts. Kept here and nowhere else: never sent to the instance, which would be reading on the
///     reader's behalf what they have not looked at, and gone with the session. Kept per profile, so switching away and
///     back counts from where that profile started rather than starting again. Set once; the account getting a marker
///     of its own — arriving at Notifications sets one — makes it unused.
/// </remarks>
public sealed class SessionMarkers
{
    private readonly ConcurrentDictionary<string, SessionMarker> _markers = new(StringComparer.Ordinal);

    /// <summary>Where <paramref name="profile" /> counts from, or <see langword="null" /> where it has not started.</summary>
    public SessionMarker? For(ActiveProfile profile) => _markers.TryGetValue(profile.Name, out var marker) ? marker : null;

    /// <summary>
    ///     Starts counting <paramref name="profile" /> from <paramref name="marker" />, unless it has already started —
    ///     the first read's is the one that stands.
    /// </summary>
    public void Start(ActiveProfile profile, SessionMarker marker) => _markers.TryAdd(profile.Name, marker);
}

/// <summary>
///     Where a session counts an account with no read marker from: past the newest notification it had when it was
///     first counted, or — where it had none at all — from the beginning, everything that arrives counting.
/// </summary>
/// <param name="After">
///     The notification counting starts past, by its own id; <see langword="null" /> for an account that had none,
///     which counts every notification it has.
/// </param>
public sealed record SessionMarker(string? After)
{
    /// <summary>Counting from the beginning: the account had no notifications when it was first counted.</summary>
    public static SessionMarker FromTheBeginning { get; } = new((string?)null);
}
