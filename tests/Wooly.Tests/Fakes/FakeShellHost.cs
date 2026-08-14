using Wooly.Tui.Shell;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A terminal's two services without a terminal: waiting, and getting back onto the drawing thread. A test says
///     when time passes, which is what makes the settle window and the rate-limit countdown assertable rather than
///     slept through.
/// </summary>
internal sealed class FakeShellHost : IShellHost
{
    private readonly List<Wait> _waiting = [];
    private readonly List<Action> _queued = [];

    /// <summary>How many waits are outstanding — where a test proves a run of presses left exactly one.</summary>
    public int Waiting => _waiting.Count(wait => !wait.CalledOff);

    /// <summary>How many waits were ever scheduled, called off or not.</summary>
    public int Scheduled { get; private set; }

    /// <inheritdoc />
    /// <remarks>Queued, because that is what the terminal does — see <see cref="Drain" />.</remarks>
    public void OnUiThread(Action work) => _queued.Add(work);

    /// <summary>
    ///     Lets every piece of queued work run, in the order it was asked for, including anything it queues in turn.
    /// </summary>
    public void Drain()
    {
        for (var rounds = 0; rounds < 1000 && _queued.Count > 0; rounds++)
        {
            var due = _queued.ToList();

            _queued.Clear();

            due.ForEach(work => work());
        }
    }

    /// <inheritdoc />
    public IDisposable After(TimeSpan delay, Action work)
    {
        Scheduled++;

        var wait = new Wait(work);

        _waiting.Add(wait);

        return wait;
    }

    /// <summary>
    ///     Lets every wait that is still outstanding happen, in the order they were asked for. Waits scheduled by that
    ///     work are left for the next call, so that a countdown is stepped rather than run to its end.
    /// </summary>
    public void Settle()
    {
        Drain();

        var due = _waiting.ToList();

        _waiting.Clear();

        foreach (var wait in due.Where(wait => !wait.CalledOff))
        {
            wait.Happen();
        }

        Drain();
    }

    /// <summary>Lets waits happen until none is left, for a test that wants the end of a countdown rather than a step of it.</summary>
    public void SettleAll()
    {
        // Bounded, because a shell that schedules a wait from inside a wait for ever is a defect to fail a test with
        // rather than one to hang it with.
        for (var rounds = 0; rounds < 1000 && Waiting > 0; rounds++)
        {
            Settle();
        }
    }

    private sealed class Wait(Action work) : IDisposable
    {
        public bool CalledOff { get; private set; }

        public void Dispose() => CalledOff = true;

        public void Happen()
        {
            if (!CalledOff)
            {
                work();
            }
        }
    }
}
