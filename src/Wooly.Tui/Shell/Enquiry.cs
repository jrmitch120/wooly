using Wooly.Core.Errors;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>What an enquiry has for the reader to see, which is a notice and the role it is drawn in.</summary>
/// <remarks>
///     A delegate of its own rather than an <c>Action</c> of two, so that the flag is named where it is raised — this
///     project says <c>isError:</c> at every other place it passes one.
/// </remarks>
public delegate void Says(string? notice, bool isError);

/// <summary>
///     A question put to an instance on a reader's behalf, which survives neither their patience nor their attention:
///     it waits out a rate limit where they can watch it count down, turns a failure into a notice rather than an
///     exception, and is dropped unread if the screen it was asked from is no longer in front of them (ADR-0014, #233).
/// </summary>
/// <remarks>
///     A scope rather than a call, because the three rules only work together. <see cref="Put{T}" /> hands out an
///     <see cref="Ask" /> to use as often as the question needs, notes the screen in front once at the top, and runs
///     the guard once at the end — so a question put in two calls is overtaken, or not, as a whole. A call that failed
///     leaves the scope with the notice already said, which is why neither callback has a failure to check for.
///     <para>
///         Whether a fetch is in flight and the hop back onto the drawing thread live here too. The screen in front
///         is read where a question is put and again where its answer lands, both on the drawing thread and nowhere
///         else; a call site left holding the hop could run the guard and its callbacks apart, which would centralise
///         the rule and copy the ceremony.
///     </para>
///     <para>
///         One rule for every way of leaving: arriving somewhere on the rail, drilling in, <c>esc</c>, a refresh
///         standing a fresher copy in place. Each of them changes which screen is in front, so none of them has to
///         remember to say so (#233).
///     </para>
/// </remarks>
/// <param name="host">The terminal's two services: waiting, and getting back onto the thread that draws.</param>
/// <param name="clock">What the countdown is measured against, so that a test can move it by hand.</param>
/// <param name="countdownStep">
///     How often the rate-limit countdown is redrawn while it waits. A second, because that is the unit it counts in
///     (<see cref="ShellTiming" />).
/// </param>
/// <param name="markStep">
///     How long one dot of the breadcrumb's fetch mark is held, and how long a fetch runs before it has one at all
///     (<see cref="ShellTiming" />).
/// </param>
/// <param name="inFront">
///     The screen in front of the reader, which is what a question is asked from. A reader two destinations further
///     along, or back out of the screen they asked from, must not have a stale answer appear in front of them
///     (ADR-0014), and this is what tells one apart.
/// </param>
public sealed class Enquiry(
    IShellHost host,
    TimeProvider clock,
    TimeSpan countdownStep,
    TimeSpan markStep,
    Func<Screen> inFront)
{
    /// <summary>What a call that answers with nothing answers with, so that one scope serves both kinds.</summary>
    private static readonly object Nothing = new();

    /// <summary>
    ///     How many questions are in flight. A count rather than a flag, because two overlap readily — a boost sent
    ///     while a timeline is still loading — and a flag written by both is cleared by whichever lands first (#217).
    /// </summary>
    private int _inFlight;

    /// <summary>The wait for the fetch mark's next dot, held for as long as anything is in flight.</summary>
    private IDisposable? _tick;

    /// <summary>Raised when the enquiry has something for the reader to see: a countdown, a failure, or silence.</summary>
    public event Says? Said;

    /// <summary>Raised when whether a fetch is in flight has changed. Always on the drawing thread.</summary>
    public event Action? Changed;

    /// <summary>
    ///     Raised when the fetch mark has gained a dot, and nothing else has changed. Always on the drawing thread.
    /// </summary>
    /// <remarks>
    ///     Not <see cref="Changed" />, which redraws the whole window — rail, content and every picture placed on it —
    ///     where a tick changes one row, two and a half times a second for as long as anything is in flight (#217).
    /// </remarks>
    public event Action? Ticked;

    /// <summary>Whether a fetch is in flight, which the breadcrumb says once and the rail never does.</summary>
    public bool Fetching => _inFlight > 0;

    /// <summary>
    ///     How many dots the breadcrumb's fetch mark has: none until a fetch has been in flight for a whole tick, then
    ///     one, two and three and one again. Decided here, beside the count it depends on, so that it is decided with
    ///     no terminal (ADR-0005).
    /// </summary>
    public int Dots { get; private set; }

    /// <summary>
    ///     Puts a question to the instance, and does something about the answer where one arrives.
    /// </summary>
    /// <param name="question">
    ///     What to ask, given an <see cref="Ask" /> to put as many calls through as it takes. What it answers with is
    ///     what both callbacks are handed.
    /// </param>
    /// <param name="eitherWay">
    ///     What lands whether or not the reader is still here — the effect that happened on the instance, which no
    ///     amount of walking away undoes.
    /// </param>
    /// <param name="ifStillHere">
    ///     What lands only while the screen it was asked from is still the one in front: a screen, a notice about it.
    /// </param>
    public async Task Put<T>(Func<Ask, Task<T>> question, Action<T>? eitherWay = null, Action<T>? ifStillHere = null)
    {
        // Noted where the question is put, with nothing awaited before it: whatever puts a screen up first — a
        // placeholder, a follow list standing empty — has put up the screen the question is asked from.
        var from = inFront();

        InFlight(true);

        T answer;

        try
        {
            answer = await question(new Ask(this));
        }
        catch (WoolyException failure)
        {
            // Said whether or not the reader is still here: this is the shell's own trouble rather than an answer.
            Apply(() => Said?.Invoke(failure.Message, isError: true));

            return;
        }
        finally
        {
            InFlight(false);
        }

        Apply(() =>
        {
            eitherWay?.Invoke(answer);

            if (ReferenceEquals(from, inFront()))
            {
                ifStillHere?.Invoke(answer);
            }
        });
    }

    /// <summary>The same, for a question whose calls answer with nothing — a dismiss, a clear, a delete.</summary>
    public Task Put(Func<Ask, Task> question, Action? eitherWay = null, Action? ifStillHere = null) =>
        Put(
            async ask =>
            {
                await question(ask);

                return Nothing;
            },
            eitherWay is null ? null : _ => eitherWay(),
            ifStillHere is null ? null : _ => ifStillHere());

    /// <summary>
    ///     Makes one call, waiting out a rate limit with a visible countdown rather than failing on it (story 53) —
    ///     the opposite of the CLI's fail-fast, which is right there because a script cannot be told to wait and wrong
    ///     here because a person can see that it is (ADR-0006).
    /// </summary>
    private async Task<T> Call<T>(Func<CancellationToken, Task<T>> call)
    {
        while (true)
        {
            try
            {
                return await call(CancellationToken.None);
            }
            catch (RateLimitedException limit)
            {
                await WaitOut(limit);
            }
        }
    }

    /// <summary>Counts a rate limit down where the reader can see it, then lets the call be made again.</summary>
    private async Task WaitOut(RateLimitedException limit)
    {
        // An instance that named no reset is waited on for as long as it usually takes one to roll a window over,
        // rather than given up on: the reader asked for something, and "try again yourself" is the CLI's answer.
        var until = limit.ResetsAt ?? clock.GetUtcNow() + TimeSpan.FromMinutes(5);

        while (clock.GetUtcNow() < until)
        {
            var left = (int)Math.Ceiling((until - clock.GetUtcNow()).TotalSeconds);

            Apply(() => Said?.Invoke($"Rate limited by {limit.Instance}. Trying again in {left}s.", isError: false));

            await Wait(countdownStep);
        }

        Apply(() => Said?.Invoke(null, isError: false));
    }

    private Task Wait(TimeSpan howLong)
    {
        var waited = new TaskCompletionSource();

        host.After(howLong, () => waited.TrySetResult());

        return waited.Task;
    }

    /// <summary>
    ///     Counts a question in or out, starting the fetch mark's ticks where the first goes out and stopping them
    ///     where the last lands. Only those two say anything has changed, since only they change what is drawn.
    /// </summary>
    private void InFlight(bool fetching) => Apply(() =>
    {
        var was = Fetching;

        _inFlight += fetching ? 1 : -1;

        if (was == Fetching)
        {
            return;
        }

        Dots = 0;
        _tick?.Dispose();
        _tick = Fetching ? host.After(markStep, Tick) : null;

        Changed?.Invoke();
    });

    /// <summary>
    ///     One dot more, or one again after the most — never none, which would put the bare word on screen for a
    ///     beat of every cycle and read as finished. Re-armed a tick at a time, as the countdown is, rather than asking
    ///     the host for a timer that repeats.
    /// </summary>
    private void Tick()
    {
        Dots = Dots % ChromeLines.MostDots + 1;
        _tick = host.After(markStep, Tick);

        Ticked?.Invoke();
    }

    private void Apply(Action work) => host.OnUiThread(work);

    /// <summary>
    ///     What one enquiry puts its calls through. Handed to the question rather than taken by it, so that every call
    ///     made under one question is made the same way and none of them is the one that forgot.
    /// </summary>
    public sealed class Ask(Enquiry enquiry)
    {
        /// <summary>Puts one call, and answers with what it said.</summary>
        /// <remarks>A failure leaves the enquiry rather than coming back as a value to be checked for.</remarks>
        public Task<T> Of<T>(Func<CancellationToken, Task<T>> call) => enquiry.Call(call);

        /// <summary>The same, for a call that answers with nothing.</summary>
        public Task Of(Func<CancellationToken, Task> call) =>
            enquiry.Call(async token =>
            {
                await call(token);

                return Nothing;
            });
    }
}
