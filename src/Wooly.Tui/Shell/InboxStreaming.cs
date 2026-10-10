using Wooly.Core.Inbox;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Holds the user stream open for the profile acted as, from the moment it is made until it is let go of: connects,
///     and where the connection drops or will not come up, tries again after a wait that doubles each time it fails and
///     starts over once it has come up (ADR-0027, #401).
/// </summary>
/// <remarks>
///     It decides only when to connect; what an event means for the badges, and what to do while the stream is down,
///     are the shell's. Everything it tells is handed to the drawing thread, as every answer the shell draws is, and
///     nothing is told once it has been let go of — so events still arriving from an old profile's stream after a
///     switch are never applied. Waits are off the shell's own clock, so a test moves them by hand.
/// </remarks>
internal sealed class InboxStreaming : IDisposable
{
    private readonly IInboxStream _stream;
    private readonly Func<ActiveProfile?> _profile;
    private readonly TimeProvider _clock;
    private readonly ShellTiming _timing;
    private readonly IShellHost _host;
    private readonly Action<InboxEvent> _heard;
    private readonly Action<bool> _up;
    private readonly Action _down;
    private readonly CancellationTokenSource _closing;

    /// <summary>How long the next wait before trying again is.</summary>
    private TimeSpan _wait;

    /// <summary>The wait before trying again that is running, or none.</summary>
    private ITimer? _again;

    /// <summary>How many times it has connected, or tried to.</summary>
    private int _tries;

    /// <param name="stream">The user stream.</param>
    /// <param name="profile">
    ///     Who to connect as, asked each time it connects rather than once, so that a profile signed in again (#248) is
    ///     connected with its new token. Nobody stops it trying.
    /// </param>
    /// <param name="clock">What it waits on.</param>
    /// <param name="timing">How long it waits: <see cref="ShellTiming.ReconnectAfter" />, doubling to <see cref="ShellTiming.ReconnectAtMost" />.</param>
    /// <param name="host">Whose drawing thread what it tells is run on.</param>
    /// <param name="abandoned">Cancelled where the session switches profile, which closes the connection with everything else.</param>
    /// <param name="heard">Told each event that came over the stream, the connection coming up apart.</param>
    /// <param name="up">
    ///     Told the connection came up: <see langword="true" /> where it came up again after the first try — after a
    ///     drop, or after refusing — so that whatever arrived while it was down can be read.
    /// </param>
    /// <param name="down">Told the connection dropped, or would not come up; it will be tried again.</param>
    public InboxStreaming(
        IInboxStream stream,
        Func<ActiveProfile?> profile,
        TimeProvider clock,
        ShellTiming timing,
        IShellHost host,
        CancellationToken abandoned,
        Action<InboxEvent> heard,
        Action<bool> up,
        Action down)
    {
        _stream = stream;
        _profile = profile;
        _clock = clock;
        _timing = timing;
        _host = host;
        _heard = heard;
        _up = up;
        _down = down;
        _wait = timing.ReconnectAfter;
        _closing = CancellationTokenSource.CreateLinkedTokenSource(abandoned);

        _ = Connect();
    }

    /// <summary>Closes the connection, and calls off trying again. Nothing it hears from here on is told.</summary>
    public void Dispose()
    {
        _again?.Dispose();
        _again = null;
        _closing.Cancel();
        _closing.Dispose();
    }

    private bool Closed => _closing.IsCancellationRequested;

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
            _up(again);

            return;
        }

        _heard(heard);
    }

    private void Dropped()
    {
        if (Closed)
        {
            return;
        }

        _down();

        _again?.Dispose();
        _again = _clock.CreateTimer(
            _ => _host.OnUiThread(() => _ = Connect()),
            state: null,
            _wait,
            Timeout.InfiniteTimeSpan);

        _wait = _wait * 2 < _timing.ReconnectAtMost ? _wait * 2 : _timing.ReconnectAtMost;
    }
}
