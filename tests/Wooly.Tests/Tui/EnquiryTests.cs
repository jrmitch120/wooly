using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A question put to an instance for a reader who may walk away before it answers (CONTEXT.md): the rate limit
///     waited out where they can watch it count down, the failure said out loud rather than thrown, and the answer
///     dropped where they have arrived somewhere else since (ADR-0014).
/// </summary>
/// <remarks>
///     Held here rather than thirteen times over in <see cref="Shell" />'s own tests, which is the whole point of the
///     module: a fourteenth call site gets the rule by using it, not by remembering it.
/// </remarks>
public class EnquiryTests
{
    /// <summary>Both callbacks run, in order, where nobody has gone anywhere since the question was put.</summary>
    [Fact]
    public async Task Put_RunsBothCallbacksOnAnAnswerNothingHasOvertaken()
    {
        var enquiry = new AnEnquiry();
        var ran = new List<string>();

        await enquiry.It.Put(
            ask => ask.Of(_ => Task.FromResult("answered")),
            eitherWay: answer => ran.Add($"either way: {answer}"),
            ifStillHere: answer => ran.Add($"still here: {answer}"));

        // Neither has run yet: both callbacks are put on the drawing thread, which is a queue and not this one.
        Assert.Empty(ran);

        enquiry.Host.Drain();

        Assert.Equal(["either way: answered", "still here: answered"], ran);
    }

    /// <summary>
    ///     Arriving somewhere makes every question in flight moot: what was done on the instance either way still
    ///     lands, and what only makes sense for a reader who stayed does not.
    /// </summary>
    [Fact]
    public async Task Put_DropsOnlyWhatDependsOnTheReaderStillBeingHere()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();
        var ran = new List<string>();

        var putting = enquiry.It.Put(
            ask => ask.Of(_ => held.Task),
            eitherWay: _ => ran.Add("either way"),
            ifStillHere: _ => ran.Add("still here"));

        enquiry.It.Arrived();
        held.SetResult("late");

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(["either way"], ran);
    }

    /// <summary>
    ///     One enquiry may put several calls, and is overtaken or not as a whole — checked once at the end rather than
    ///     after each, because what matters is whether the reader is still where they were, not how far the answer got.
    /// </summary>
    [Fact]
    public async Task Put_TakesOneTokenForAsManyCallsAsAreMade()
    {
        var enquiry = new AnEnquiry();
        var first = new TaskCompletionSource<string>();
        string? landed = null;

        var putting = enquiry.It.Put(
            async ask =>
            {
                var one = await ask.Of(_ => first.Task);
                var two = await ask.Of(_ => Task.FromResult("two"));

                return $"{one} {two}";
            },
            ifStillHere: answer => landed = answer);

        // Somewhere else is arrived at between the two calls, so the second one's answer is nobody's business either.
        enquiry.It.Arrived();
        first.SetResult("one");

        await putting;

        Assert.Null(landed);
    }

    /// <summary>The same two calls land as one answer where the reader stayed.</summary>
    [Fact]
    public async Task Put_AnswersWithWhatEveryCallInItFound()
    {
        var enquiry = new AnEnquiry();
        string? landed = null;

        await enquiry.It.Put(
            async ask =>
            {
                var one = await ask.Of(_ => Task.FromResult("one"));
                var two = await ask.Of(_ => Task.FromResult("two"));

                return $"{one} {two}";
            },
            ifStillHere: answer => landed = answer);

        enquiry.Host.Drain();

        Assert.Equal("one two", landed);
    }

    /// <summary>
    ///     A rate limit is waited out with a countdown rather than failed on (story 53), and the call is made again
    ///     once the window the instance named has rolled over.
    /// </summary>
    [Fact]
    public async Task Put_WaitsOutARateLimitWhereTheReaderCanWatchItCountDown()
    {
        var attempts = 0;
        var enquiry = new AnEnquiry();

        var putting = enquiry.It.Put(
            ask => ask.Of(_ =>
            {
                attempts++;

                return attempts == 1
                    ? Task.FromException<string>(
                        new RateLimitedException("mastodon.social", AnEnquiry.Now + TimeSpan.FromSeconds(3)))
                    : Task.FromResult("answered");
            }));

        enquiry.Host.Drain();

        Assert.Contains("Rate limited by mastodon.social", enquiry.Notice);
        Assert.Contains("3s", enquiry.Notice);
        Assert.False(enquiry.NoticeIsError);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(2));
        enquiry.Host.Settle();

        Assert.Contains("1s", enquiry.Notice);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.SettleAll();

        await putting;

        Assert.Equal(2, attempts);
        Assert.Null(enquiry.Notice);
    }

    /// <summary>
    ///     Anything a wait cannot mend is said out loud in the role that says it is a failure, and neither callback
    ///     runs — there is no answer for them to be about.
    /// </summary>
    [Fact]
    public async Task Put_SaysAFailureOutLoudAndRunsNeitherCallback()
    {
        var enquiry = new AnEnquiry();
        var ran = new List<string>();

        await enquiry.It.Put(
            ask => ask.Of<string>(_ => Task.FromException<string>(
                new AuthenticationException("That token has been revoked."))),
            eitherWay: _ => ran.Add("either way"),
            ifStillHere: _ => ran.Add("still here"));

        enquiry.Host.Drain();

        Assert.Equal("That token has been revoked.", enquiry.Notice);
        Assert.True(enquiry.NoticeIsError);
        Assert.Empty(ran);
        Assert.False(enquiry.It.Fetching);
    }

    /// <summary>
    ///     A failure is said even where the reader has moved on, because what is on the breadcrumb is the shell's own
    ///     trouble rather than an answer to the question they have stopped asking.
    /// </summary>
    [Fact]
    public async Task Put_SaysAFailureEvenWhereTheReaderHasArrivedSomewhereElse()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));

        enquiry.It.Arrived();
        held.SetException(new AuthenticationException("No."));

        await putting;

        enquiry.Host.Drain();

        Assert.Equal("No.", enquiry.Notice);
        Assert.True(enquiry.NoticeIsError);
    }

    /// <summary>
    ///     A fetch is in flight for as long as the enquiry is, including between the calls of one that puts several —
    ///     which is what the breadcrumb says once and the rail never does.
    /// </summary>
    [Fact]
    public async Task Put_IsFetchingForAsLongAsTheEnquiryLasts()
    {
        var enquiry = new AnEnquiry();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();

        Assert.False(enquiry.It.Fetching);

        var putting = enquiry.It.Put(async ask =>
        {
            await ask.Of(_ => first.Task);

            return await ask.Of(_ => second.Task);
        });

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);

        first.SetResult("one");

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);

        second.SetResult("two");

        await putting;

        enquiry.Host.Drain();

        Assert.False(enquiry.It.Fetching);
        Assert.True(enquiry.Changes > 0);
    }

    /// <summary>
    ///     A fetch not yet a tick old has no dots, so the breadcrumb draws nothing for it: one that lands inside the
    ///     first tick is never announced at all, and a cached destination never flashes a mark (#217).
    /// </summary>
    [Fact]
    public async Task Put_HasNoDotsUntilTheFirstTick()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.Dots);

        held.SetResult("quick");

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(0, enquiry.It.Dots);
        Assert.Equal(0, enquiry.Host.Waiting);
    }

    /// <summary>
    ///     The mark grows a dot a tick up to three and starts over at one — never at none, which would put the bare
    ///     word on screen for a quarter of every cycle and read as finished (#217).
    /// </summary>
    [Fact]
    public async Task Put_GrowsTheMarkADotATickAndStartsOverAtOne()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));
        var dots = new List<int>();

        enquiry.Host.Drain();

        for (var tick = 0; tick < 4; tick++)
        {
            enquiry.Host.Settle();
            dots.Add(enquiry.It.Dots);
        }

        Assert.Equal([1, 2, 3, 1], dots);
        Assert.All(enquiry.Host.Delays, delay => Assert.Equal(AnEnquiry.MarkStep, delay));

        held.SetResult("done");

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(0, enquiry.It.Dots);
    }

    /// <summary>
    ///     A tick changes one row, and says so on an event of its own: <see cref="Enquiry.Changed" /> redraws the
    ///     whole window, pictures and all, which two and a half times a second is not what a dot is worth (#217).
    /// </summary>
    [Fact]
    public async Task Put_TicksOnItsOwnEventRatherThanOnChanged()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));

        enquiry.Host.Drain();

        var changes = enquiry.Changes;

        enquiry.Host.Settle();
        enquiry.Host.Settle();

        Assert.Equal(2, enquiry.Ticks);
        Assert.Equal(changes, enquiry.Changes);

        held.SetResult("done");

        await putting;
    }

    /// <summary>
    ///     Two questions in flight at once — a boost sent while a timeline is still loading — are a fetch in flight
    ///     until the second lands, and the first landing neither says otherwise nor starts the dots over. A flag
    ///     written by both did both, and would have restarted the first tick's wait each time (#217).
    /// </summary>
    [Fact]
    public async Task Put_IsFetchingUntilTheLastOfTwoOverlappingQuestionsLands()
    {
        var enquiry = new AnEnquiry();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();

        var one = enquiry.It.Put(ask => ask.Of(_ => first.Task));
        var two = enquiry.It.Put(ask => ask.Of(_ => second.Task));

        enquiry.Host.Drain();
        enquiry.Host.Settle();
        enquiry.Host.Settle();

        Assert.Equal(2, enquiry.It.Dots);

        first.SetResult("one");

        await one;

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);
        Assert.Equal(2, enquiry.It.Dots);
        Assert.Equal(1, enquiry.Host.Waiting);

        enquiry.Host.Settle();

        Assert.Equal(3, enquiry.It.Dots);

        second.SetResult("two");

        await two;

        enquiry.Host.Drain();

        Assert.False(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.Dots);
        Assert.Equal(0, enquiry.Host.Waiting);
    }

    /// <summary>
    ///     A rate limit waited out is a question still in flight, so the dots go on arriving on the breadcrumb while
    ///     the countdown counts on the status row — two rhythms on two rows.
    /// </summary>
    [Fact]
    public async Task Put_GoesOnTickingWhileARateLimitIsWaitedOut()
    {
        var attempts = 0;
        var enquiry = new AnEnquiry();

        var putting = enquiry.It.Put(
            ask => ask.Of(_ => ++attempts == 1
                ? Task.FromException<string>(
                    new RateLimitedException("mastodon.social", AnEnquiry.Now + TimeSpan.FromSeconds(3)))
                : Task.FromResult("answered")));

        enquiry.Host.Drain();

        Assert.Contains("3s", enquiry.Notice);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.Settle();

        Assert.Contains("2s", enquiry.Notice);
        Assert.Equal(1, enquiry.It.Dots);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.Settle();

        Assert.Contains("1s", enquiry.Notice);
        Assert.Equal(2, enquiry.It.Dots);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.SettleAll();

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(2, attempts);
        Assert.False(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.Dots);
    }

    /// <summary>
    ///     What the shell runs at holds each dot for 400ms, which is the number <c>docs/tui-shell.md</c>'s table gives
    ///     as the mark step — beside the countdown's second rather than the same as it (#213).
    /// </summary>
    [Fact]
    public void ShellTiming_HoldsEachDotOfTheMarkFor400ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(400), ShellTiming.Default.MarkStep);
        Assert.NotEqual(ShellTiming.Default.CountdownStep, ShellTiming.Default.MarkStep);
    }

    /// <summary>A call that answers with nothing is put the same way, and splits its callbacks the same way.</summary>
    [Fact]
    public async Task Put_ServesACallThatAnswersWithNothing()
    {
        var enquiry = new AnEnquiry();
        var made = false;
        var ran = new List<string>();

        await enquiry.It.Put(
            ask => ask.Of(_ =>
            {
                made = true;

                return Task.CompletedTask;
            }),
            eitherWay: () => ran.Add("either way"),
            ifStillHere: () => ran.Add("still here"));

        enquiry.Host.Drain();

        Assert.True(made);
        Assert.Equal(["either way", "still here"], ran);
    }

    /// <summary>And is dropped on arrival by the same rule, on the same half of it.</summary>
    [Fact]
    public async Task Put_DropsWhatOnlyASittingReaderWantsFromACallThatAnswersWithNothing()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource();
        var ran = new List<string>();

        var putting = enquiry.It.Put(
            ask => ask.Of(_ => held.Task),
            eitherWay: () => ran.Add("either way"),
            ifStillHere: () => ran.Add("still here"));

        enquiry.It.Arrived();
        held.SetResult();

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(["either way"], ran);
    }

    /// <summary>An enquiry built over the fake terminal, with whatever it said kept where a test can read it.</summary>
    private sealed class AnEnquiry
    {
        /// <summary>The moment the clock starts at, so that a reset a test names is a length rather than a date.</summary>
        public static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

        public AnEnquiry()
        {
            It = new Enquiry(Host, Clock, TimeSpan.FromSeconds(1), MarkStep);

            It.Said += (notice, isError) =>
            {
                Notice = notice;
                NoticeIsError = isError;
            };

            It.Changed += () => Changes++;
            It.Ticked += () => Ticks++;
        }

        /// <summary>How long one dot of the fetch mark is held — the shell's own, since nothing here shortens it.</summary>
        public static readonly TimeSpan MarkStep = ShellTiming.Default.MarkStep;

        public FakeShellHost Host { get; } = new();

        public MovableTimeProvider Clock { get; } = new(Now);

        public Enquiry It { get; }

        /// <summary>The last thing it said, which is what the shell would be drawing.</summary>
        public string? Notice { get; private set; }

        public bool NoticeIsError { get; private set; }

        /// <summary>How many times it said something on screen had changed.</summary>
        public int Changes { get; private set; }

        /// <summary>How many times the fetch mark gained a dot.</summary>
        public int Ticks { get; private set; }
    }
}
