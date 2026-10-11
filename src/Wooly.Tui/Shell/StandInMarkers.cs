using System.Collections.Concurrent;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Where this session counts unread notifications from for a profile whose account has no <b>Read marker</b>: the
///     newest notification it had when its count was first read (ADR-0027, story 8 of #395).
/// </summary>
/// <remarks>
///     No marker means nothing unread this client can prove — but what arrives while the shell runs has plainly not
///     been read, so it counts. Kept here and nowhere else: never sent to the instance, which would be reading on the
///     reader's behalf what they have not looked at, and gone with the session. Kept per profile, so switching away and
///     back counts from where that profile started rather than starting again. Set once; the account getting a marker
///     of its own — arriving at Notifications sets one — makes it unused.
/// </remarks>
public sealed class StandInMarkers
{
    /// <summary>What an account with no notifications at all counts from: below every id an instance hands out.</summary>
    public const string BeforeEverything = "0";

    private readonly ConcurrentDictionary<string, string> _from = new(StringComparer.Ordinal);

    /// <summary>Where <paramref name="profile" /> counts from, or <see langword="null" /> where it has not started.</summary>
    public string? For(ActiveProfile profile) => _from.TryGetValue(profile.Name, out var from) ? from : null;

    /// <summary>
    ///     Starts counting <paramref name="profile" /> from <paramref name="newest" />, unless it has already started —
    ///     the first read's newest is the one that stands.
    /// </summary>
    public void Start(ActiveProfile profile, string newest) => _from.TryAdd(profile.Name, newest);
}
