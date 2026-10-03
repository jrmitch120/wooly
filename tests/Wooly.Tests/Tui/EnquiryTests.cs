using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A question put to an instance for a reader who may walk away before it answers (CONTEXT.md): the rate limit
///     waited out where they can watch it count down, the failure said out loud rather than thrown, and the answer
///     dropped where the screen it was asked from is no longer in front of them (ADR-0014, #233).
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
    ///     Walking off the screen a question was asked from makes it moot: what was done on the instance either way
    ///     still lands, and what only makes sense for a reader who stayed does not.
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

        enquiry.WalkAway();
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

        // The reader walks off between the two calls, so the second one's answer is nobody's business either.
        enquiry.WalkAway();
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
        var failure = new UneditablePostException("110");

        await enquiry.It.Put(
            ask => ask.Of<string>(_ => Task.FromException<string>(failure)),
            eitherWay: _ => ran.Add("either way"),
            ifStillHere: _ => ran.Add("still here"));

        enquiry.Host.Drain();

        Assert.Equal(failure.Message, enquiry.Notice);
        Assert.True(enquiry.NoticeIsError);
        Assert.Empty(ran);
        Assert.False(enquiry.It.Fetching);
    }

    /// <summary>
    ///     A failure is said even where the reader has moved on, because what is on the breadcrumb is the shell's own
    ///     trouble rather than an answer to the question they have stopped asking.
    /// </summary>
    [Fact]
    public async Task Put_SaysAFailureEvenWhereTheReaderHasWalkedOffTheScreen()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));

        var failure = new UneditablePostException("110");

        enquiry.WalkAway();
        held.SetException(failure);

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(failure.Message, enquiry.Notice);
        Assert.True(enquiry.NoticeIsError);
    }

    /// <summary>
    ///     A refused token is handed to whoever listens for one rather than said, because what to say about it — which
    ///     key fixes it — is the shell's, not the enquiry's (#248). Neither callback runs.
    /// </summary>
    [Fact]
    public async Task Put_HandsARefusedTokenOnRatherThanSayingIt()
    {
        var enquiry = new AnEnquiry();
        var refused = new List<AuthenticationException>();
        var ran = new List<string>();
        var failure = new AuthenticationException("mastodon.social refused the access token.");

        enquiry.It.Refused += refused.Add;

        await enquiry.It.Put(
            ask => ask.Of<string>(_ => Task.FromException<string>(failure)),
            eitherWay: _ => ran.Add("either way"),
            ifStillHere: _ => ran.Add("still here"));

        enquiry.Host.Drain();

        Assert.Same(failure, Assert.Single(refused));
        Assert.Null(enquiry.Notice);
        Assert.Empty(ran);
        Assert.False(enquiry.It.Fetching);
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
    ///     A fetch not yet a tick old has no frame, so the breadcrumb draws nothing for it: one that lands inside the
    ///     first tick is never announced at all, and a cached destination never flashes a mark (#217).
    /// </summary>
    [Fact]
    public async Task Put_HasNoFrameUntilTheFirstTick()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.SpinnerFrame);

        held.SetResult("quick");

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(0, enquiry.It.SpinnerFrame);
        Assert.Equal(0, enquiry.Host.Waiting);
    }

    /// <summary>
    ///     The spinner moves a frame a tick up to the last and starts over at the first — never at none, which would
    ///     take it off the edge for a beat of every cycle and read as finished (#217, #281).
    /// </summary>
    [Fact]
    public async Task Put_TurnsTheSpinnerAFrameATickAndStartsOverAtTheFirst()
    {
        var enquiry = new AnEnquiry();
        var held = new TaskCompletionSource<string>();

        var putting = enquiry.It.Put(ask => ask.Of(_ => held.Task));
        var frames = new List<int>();

        enquiry.Host.Drain();

        for (var tick = 0; tick < ChromeLines.SpinnerFrames + 1; tick++)
        {
            enquiry.Host.Settle();
            frames.Add(enquiry.It.SpinnerFrame);
        }

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1], frames);
        Assert.All(enquiry.Host.Delays, delay => Assert.Equal(AnEnquiry.MarkStep, delay));

        held.SetResult("done");

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(0, enquiry.It.SpinnerFrame);
    }

    /// <summary>
    ///     A fetch that starts after the last one landed starts the spinner from its first frame, rather than wherever
    ///     the last one left it (#281).
    /// </summary>
    [Fact]
    public async Task Put_StartsANewFetchFromTheFirstFrame()
    {
        var enquiry = new AnEnquiry();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();

        var one = enquiry.It.Put(ask => ask.Of(_ => first.Task));

        enquiry.Host.Drain();
        enquiry.Host.Settle();
        enquiry.Host.Settle();
        enquiry.Host.Settle();

        Assert.Equal(3, enquiry.It.SpinnerFrame);

        first.SetResult("one");

        await one;

        enquiry.Host.Drain();

        var two = enquiry.It.Put(ask => ask.Of(_ => second.Task));

        enquiry.Host.Drain();

        Assert.Equal(0, enquiry.It.SpinnerFrame);

        enquiry.Host.Settle();

        Assert.Equal(1, enquiry.It.SpinnerFrame);

        second.SetResult("two");

        await two;
    }

    /// <summary>
    ///     A tick changes one row, and says so on an event of its own: <see cref="Enquiry.Changed" /> redraws the whole
    ///     window, pictures and all, which two and a half times a second is not what a spinner frame is worth (#217).
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
    ///     until the second lands, and the first landing neither says otherwise nor starts the spinner over. A flag
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

        Assert.Equal(2, enquiry.It.SpinnerFrame);

        var changes = enquiry.Changes;

        first.SetResult("one");

        await one;

        enquiry.Host.Drain();

        Assert.True(enquiry.It.Fetching);
        Assert.Equal(changes, enquiry.Changes);
        Assert.Equal(2, enquiry.It.SpinnerFrame);
        Assert.Equal(1, enquiry.Host.Waiting);

        enquiry.Host.Settle();

        Assert.Equal(3, enquiry.It.SpinnerFrame);

        second.SetResult("two");

        await two;

        enquiry.Host.Drain();

        Assert.False(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.SpinnerFrame);
        Assert.Equal(0, enquiry.Host.Waiting);
    }

    /// <summary>
    ///     A rate limit waited out is a question still in flight, so the spinner goes on turning on the breadcrumb
    ///     while the countdown counts on the status row — two rhythms on two rows.
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
        Assert.Equal(1, enquiry.It.SpinnerFrame);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.Settle();

        Assert.Contains("1s", enquiry.Notice);
        Assert.Equal(2, enquiry.It.SpinnerFrame);

        enquiry.Clock.Advance(TimeSpan.FromSeconds(1));
        enquiry.Host.SettleAll();

        await putting;

        enquiry.Host.Drain();

        Assert.Equal(2, attempts);
        Assert.False(enquiry.It.Fetching);
        Assert.Equal(0, enquiry.It.SpinnerFrame);
    }

    /// <summary>
    ///     What the shell runs at holds each spinner frame for 400ms, which is the number <c>docs/tui-shell.md</c>'s
    ///     table gives as the mark step — beside the countdown's second rather than the same as it (#213).
    /// </summary>
    [Fact]
    public void ShellTiming_HoldsEachFrameOfTheMarkFor400ms()
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

    /// <summary>And is dropped by the same rule, on the same half of it.</summary>
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

        enquiry.WalkAway();
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
            It = new Enquiry(Host, Clock, TimeSpan.FromSeconds(1), MarkStep, () => InFront);

            It.Said += (notice, isError) =>
            {
                Notice = notice;
                NoticeIsError = isError;
            };

            It.Changed += () => Changes++;
            It.Ticked += () => Ticks++;
        }

        /// <summary>
        ///     How long one frame of the fetch mark is held — the shell's own, since nothing here shortens it.
        /// </summary>
        public static readonly TimeSpan MarkStep = ShellTiming.Default.MarkStep;

        public FakeShellHost Host { get; } = new();

        public MovableTimeProvider Clock { get; } = new(Now);

        public Enquiry It { get; }

        /// <summary>The screen in front of the reader, which is what every question here is asked from.</summary>
        public Screen InFront { get; private set; } = new NoticeScreen("Here", "Where the question was asked.");

        /// <summary>Puts another screen in front of the reader: any move at all, the rule being one.</summary>
        public void WalkAway() => InFront = new NoticeScreen("Elsewhere", "Where the reader went.");

        /// <summary>The last thing it said, which is what the shell would be drawing.</summary>
        public string? Notice { get; private set; }

        public bool NoticeIsError { get; private set; }

        /// <summary>How many times it said something on screen had changed.</summary>
        public int Changes { get; private set; }

        /// <summary>How many times the fetch mark turned a frame.</summary>
        public int Ticks { get; private set; }
    }
}
