using Wooly.Core.Profiles;

namespace Wooly.Core.Inbox;

/// <summary>
///     The <b>Inbox stream</b>: the instance's user stream for a profile, told as what it means for the Inbox — a
///     notification arrived, a conversation arrived or changed, the connection came up — and nothing else it carries
///     (ADR-0027, #401). The narrow port ADR-0005 asks for over Mastonet's streaming; front ends depend on this, and
///     their tests fake it.
/// </summary>
/// <remarks>
///     One connection per call, which ends when the connection does: there is no reconnecting in here, because how
///     long to wait before trying again, and what to do meanwhile, is the front end's.
/// </remarks>
public interface IInboxStream
{
    /// <summary>
    ///     Connects to <paramref name="profile" />'s user stream and tells <paramref name="heard" /> what comes over it,
    ///     until the connection drops.
    /// </summary>
    /// <param name="profile">Whose stream — which instance to connect to, and the token to connect with.</param>
    /// <param name="heard">
    ///     Told <see cref="InboxEvent.Connected" /> once the connection is up, and then each event as it comes. Called
    ///     on whatever thread the connection is read on, which need not be the caller's.
    /// </param>
    /// <param name="cancellationToken">Closes the connection, or calls off connecting.</param>
    /// <returns>
    ///     A task that ends when the connection has dropped — closed by the instance, or broken — or never came up,
    ///     faulted where there was something to say about why. A connection that came up and then dropped and one that
    ///     never came up differ only in whether <see cref="InboxEvent.Connected" /> was heard first.
    /// </returns>
    Task Listen(ActiveProfile profile, Action<InboxEvent> heard, CancellationToken cancellationToken);
}
