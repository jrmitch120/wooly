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
            It = new Enquiry(Host, Clock, TimeSpan.FromSeconds(1));

            It.Said += (notice, isError) =>
            {
                Notice = notice;
                NoticeIsError = isError;
            };

            It.Changed += () => Changes++;
        }

        public FakeShellHost Host { get; } = new();

        public MovableTimeProvider Clock { get; } = new(Now);

        public Enquiry It { get; }

        /// <summary>The last thing it said, which is what the shell would be drawing.</summary>
        public string? Notice { get; private set; }

        public bool NoticeIsError { get; private set; }

        /// <summary>How many times it said something on screen had changed.</summary>
        public int Changes { get; private set; }
    }
}
