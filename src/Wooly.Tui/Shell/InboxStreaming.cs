using Wooly.Core.Inbox;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Keeps the Inbox live for the profile acted as, from the moment it is made until it is let go of: holds the user
///     stream open, polling every <see cref="ShellTiming.PollEvery" /> for as long as the stream is down, and says which
///     Inbox counts to read again and when (ADR-0027, #398, #401).
/// </summary>
/// <remarks>
///     <para>
///         The stream is connected at once, and where the connection drops or will not come up it is tried again after
///         a wait that doubles each time it fails and starts over once it has come up. The poll runs from the start
///         until the stream says it is up — one that never comes up is one the badges are kept current without — and
///         starts again when it drops. Coming up again after the first try reads all three counts again for whatever
///         arrived while it was down; the first time it comes up reads nothing, the opening counts being fresh.
///     </para>
///     <para>
///         What the stream says is gathered for <see cref="ShellTiming.GatherFor" /> from the first of it and read
///         again as one: a notification re-reads Notifications, and Requests too where somebody asked to follow; a
///         conversation re-reads Messages. A direct message comes over it as a mention and a conversation, so
///         gathering is what makes it one re-read — and one bell.
///     </para>
///     <para>
///         How a count is read, and as whom, is the shell's. Everything here is handed to the drawing thread, as every
///         answer the shell draws is, and nothing is said once it has been let go of — so events still arriving from
///         an old profile's stream after a switch are never applied. Waits are off the shell's own clock, so a test
///         moves them by hand. None of it is drawn: a passing blip is no business of the reader's.
///     </para>
/// </remarks>
internal sealed class InboxStreaming : IDisposable
{
    private readonly IInboxStream _stream;
    private readonly Func<ActiveProfile?> _profile;
    private readonly TimeProvider _clock;
    private readonly ShellTiming _timing;
    private readonly IShellHost _host;
    private readonly Action<IReadOnlyList<DestinationKind>> _reread;
    private readonly CancellationTokenSource _closing;

    /// <summary>How long the next wait before trying again is.</summary>
    private TimeSpan _wait;

    /// <summary>The wait before trying again that is running, or none.</summary>
    private ITimer? _again;

    /// <summary>What has been heard since the gathering began, told together when it ends.</summary>
    private readonly List<InboxEvent> _gathered = [];

    /// <summary>The gathering running, or none.</summary>
    private ITimer? _gathering;

    /// <summary>The poll, which runs while the stream is down; none while it is up.</summary>
    private InboxPoll? _poll;

    /// <summary>How many times it has connected, or tried to.</summary>
    private int _tries;

    /// <param name="stream">The user stream.</param>
    /// <param name="profile">
    ///     Who to connect as, asked each time it connects rather than once, so that a profile signed in again (#248) is
    ///     connected with its new token. Nobody stops it trying.
    /// </param>
    /// <param name="clock">What it waits on.</param>
    /// <param name="timing">
    ///     How long it waits: <see cref="ShellTiming.PollEvery" /> between polls, <see cref="ShellTiming.GatherFor" />
    ///     to gather, and <see cref="ShellTiming.ReconnectAfter" />, doubling to
    ///     <see cref="ShellTiming.ReconnectAtMost" />, before trying the stream again.
    /// </param>
    /// <param name="host">Whose drawing thread what it says is run on.</param>
    /// <param name="abandoned">Cancelled where the session switches profile, which closes the connection with everything else.</param>
    /// <param name="reread">
    ///     Told which Inbox counts to read again, as one re-read, in the Inbox's own order (<see cref="InboxCount.Kinds" />).
    /// </param>
    public InboxStreaming(
        IInboxStream stream,
        Func<ActiveProfile?> profile,
        TimeProvider clock,
        ShellTiming timing,
        IShellHost host,
        CancellationToken abandoned,
        Action<IReadOnlyList<DestinationKind>> reread)
    {
        _stream = stream;
        _profile = profile;
        _clock = clock;
        _timing = timing;
        _host = host;
        _reread = reread;
        _wait = timing.ReconnectAfter;
        _closing = CancellationTokenSource.CreateLinkedTokenSource(abandoned);

        Poll();

        _ = Connect();
    }

    /// <summary>
    ///     Closes the connection, stops the poll, and calls off trying again. Nothing it hears from here on is told.
    /// </summary>
    public void Dispose()
    {
        _poll?.Dispose();
        _poll = null;
        _again?.Dispose();
        _again = null;
        _gathering?.Dispose();
        _gathering = null;
        _closing.Cancel();
        _closing.Dispose();
    }

    private bool Closed => _closing.IsCancellationRequested;

    /// <summary>Starts the poll, unless it is already running — starting it again would put the next poll off.</summary>
    private void Poll() =>
        _poll ??= new InboxPoll(
            _clock,
            _timing.PollEvery,
            _host,
            () =>
            {
                if (!Closed)
                {
                    _reread(InboxCount.Kinds);
                }
            });

    private async Task Connect()
    {
        if (Closed || _profile() is not { } profile)
        {
            return;
        }

        var again = ++_tries > 1;
        var closing = _closing.Token;

        try
        {
            await _stream.Listen(profile, heard => _host.OnUiThread(() => Hear(heard, again)), closing);
        }
        catch (Exception)
        {
            // However it ended, it dropped: a stream that broke has nothing about it worth saying (ADR-0027), and is
            // tried again — unless it was let go of, which leaves nobody to tell.
        }

        _host.OnUiThread(Dropped);
    }

    private void Hear(InboxEvent heard, bool again)
    {
        if (Closed)
        {
            return;
        }

        if (heard is InboxEvent.Connected)
        {
            _wait = _timing.ReconnectAfter;
            _poll?.Dispose();
            _poll = null;

            if (again)
            {
                _reread(InboxCount.Kinds);
            }

            return;
        }

        _gathered.Add(heard);

        _gathering ??= _clock.CreateTimer(
            _ => _host.OnUiThread(Gathered),
            state: null,
            _timing.GatherFor,
            Timeout.InfiniteTimeSpan);
    }

    private void Gathered()
    {
        _gathering?.Dispose();
        _gathering = null;

        if (Closed)
        {
            return;
        }

        var touched = _gathered.SelectMany(Touches).ToHashSet();

        _gathered.Clear();

        if (touched.Count > 0)
        {
            _reread([.. InboxCount.Kinds.Where(touched.Contains)]);
        }
    }

    /// <summary>
    ///     The counts <paramref name="said" /> touches: a notification the Notifications count, and the Requests count
    ///     too where somebody asked to follow; a conversation the Messages count.
    /// </summary>
    private static IEnumerable<DestinationKind> Touches(InboxEvent said) => said switch
    {
        InboxEvent.Notified { Kind.AsksToFollow: true } => [DestinationKind.Notifications, DestinationKind.Requests],
        InboxEvent.Notified => [DestinationKind.Notifications],
        InboxEvent.ConversationChanged => [DestinationKind.Messages],
        _ => [],
    };

    private void Dropped()
    {
        if (Closed)
        {
            return;
        }

        Poll();

        _again?.Dispose();
        _again = _clock.CreateTimer(
            _ => _host.OnUiThread(() => _ = Connect()),
            state: null,
            _wait,
            Timeout.InfiniteTimeSpan);

        _wait = _wait * 2 < _timing.ReconnectAtMost ? _wait * 2 : _timing.ReconnectAtMost;
    }
}
