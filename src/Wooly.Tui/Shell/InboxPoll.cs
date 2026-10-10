namespace Wooly.Tui.Shell;

/// <summary>
///     Reads the Inbox badges again every so often, off the shell's own clock, for as long as it is not let go of — so
///     that something arriving shows on the rail without the reader going to look (ADR-0027, #398).
/// </summary>
/// <remarks>
///     It decides only when; what a poll reads, and as whom, is the shell's. Each poll is handed to the drawing thread
///     rather than run where the clock went off, the way every answer the shell draws is. Running from the moment it
///     is made until it is disposed, so stopping it is letting it go and starting it again is making another — which
///     is what a profile switch does, and what the user stream will do as it comes and goes (#401).
/// </remarks>
internal sealed class InboxPoll : IDisposable
{
    private readonly ITimer _timer;

    /// <param name="clock">What it waits on, so that a test can move it by hand.</param>
    /// <param name="every">How long between one poll and the next, the first included.</param>
    /// <param name="host">Whose drawing thread a poll is run on.</param>
    /// <param name="poll">What a poll does.</param>
    public InboxPoll(TimeProvider clock, TimeSpan every, IShellHost host, Action poll) =>
        _timer = clock.CreateTimer(_ => host.OnUiThread(poll), state: null, every, every);

    /// <summary>Stops polling. Nothing it has set going is called off; a poll already handed over still runs.</summary>
    public void Dispose() => _timer.Dispose();
}
