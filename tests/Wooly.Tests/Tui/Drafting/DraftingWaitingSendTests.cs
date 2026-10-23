using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A send waiting on what is still going up (ADR-0026, #375), held by drafting in its one slot (ADR-0029): said, sent
///     once everything is done while the compose is in front, said again when the compose is uncovered, and called off
///     by <c>esc</c>.
/// </summary>
public class DraftingWaitingSendTests : IDisposable
{
    private const string Waiting = "Will send once 1 attachment finishes — esc to stop.";

    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     <c>ctrl-s</c> with something still going up keeps the screen up and says it will send once it has finished —
    ///     sends nothing meanwhile — and then sends.
    /// </summary>
    [Fact]
    public async Task SendingWaitsForWhatIsStillGoingUp()
    {
        var (drafting, compose) = await WaitingOnAClip();

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Equal(Waiting, drafting.Notice);
        Assert.False(drafting.NoticeIsError);

        drafting.Fakes.Author.Attaching.Single().Processing();
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);

        drafting.Fakes.Author.Attaching.Single().Ready(MediaKind.Video);
        drafting.Fakes.Host.Drain();

        Assert.Equal("m1", Assert.Single(Assert.Single(drafting.Fakes.Author.Published).Draft.Attached).Id);
        Assert.Equal("Sent.", drafting.Notice);
    }

    /// <summary>Two still going up are counted as two.</summary>
    [Fact]
    public async Task TheWaitCountsWhatIsStillGoingUp()
    {
        var drafting = Composing();

        drafting.Drafting.Paste(_files.WriteFile("one.png"));
        drafting.Drafting.Paste(_files.WriteFile("two.png"));

        await drafting.Drafting.Send();

        Assert.Equal("Will send once 2 attachments finish — esc to stop.", drafting.Notice);
    }

    /// <summary>
    ///     A send waiting on an upload stays asked for while a screen is open over compose. Where the upload finishes
    ///     meanwhile, the compose uncovered sends; where it has not, the compose uncovered says again that the post will
    ///     send, and it sends once the upload finishes. Never dropped without a word (#375).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AWaitingSendOutlastsAScreenOpenedOverCompose(bool finishesWhileAway)
    {
        var (drafting, compose) = await WaitingOnAClip();
        var over = ADrafting.Home();

        drafting.Stack.Push(over);

        if (finishesWhileAway)
        {
            drafting.Fakes.Author.Attaching.Single().Ready(MediaKind.Video);
            drafting.Fakes.Host.Drain();

            Assert.Empty(drafting.Fakes.Author.Published);
        }

        drafting.Stack.TakeOff(over);
        drafting.Fakes.Host.Drain();

        if (!finishesWhileAway)
        {
            Assert.Same(compose, drafting.Stack.Top);
            Assert.Empty(drafting.Fakes.Author.Published);
            Assert.Equal(Waiting, drafting.Notice);

            drafting.Fakes.Author.Attaching.Single().Ready(MediaKind.Video);
            drafting.Fakes.Host.Drain();
        }

        Assert.Equal("m1", Assert.Single(Assert.Single(drafting.Fakes.Author.Published).Draft.Attached).Id);
        Assert.Equal("Sent.", drafting.Notice);
    }

    /// <summary>
    ///     <c>esc</c> while a send waits calls the send off and leaves the draft as it was, on screen — and nothing goes
    ///     out once the attachment is ready.
    /// </summary>
    [Fact]
    public async Task EscCallsAWaitingSendOff()
    {
        var (drafting, compose) = await WaitingOnAClip();

        Assert.True(drafting.Drafting.Back());

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("Not sent — the draft is as it was.", drafting.Notice);

        drafting.Fakes.Author.Attaching.Single().Ready(MediaKind.Video);
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("Watch this", compose.Text);
        Assert.Single(compose.Media.Attachments);
    }

    /// <summary>
    ///     <c>esc</c> with no send waiting is not drafting's, and is left to the shell; nor is it ever a draft verb, which
    ///     would reach drafting before the shell's open question (ADR-0029).
    /// </summary>
    [Fact]
    public void EscWithNothingWaitingIsTheShells()
    {
        var drafting = Composing();

        Assert.False(drafting.Drafting.Back());
        Assert.Equal(Drafted.NotMine, drafting.Press(ShellKey.Escape));
        Assert.Null(drafting.Notice);
    }

    /// <summary>
    ///     A compose with a send waiting, taken off the stack, takes the wait with it: nothing goes out once what it
    ///     waited on is ready.
    /// </summary>
    [Fact]
    public async Task AComposeLeavingTakesItsWaitingSendWithIt()
    {
        var (drafting, compose) = await WaitingOnAClip();

        drafting.Stack.TakeOff(compose);
        drafting.Fakes.Author.Attaching.Single().Ready(MediaKind.Video);
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>A compose with a clip going up and words written, whose <c>ctrl-s</c> is waiting on the clip.</summary>
    private async Task<(ADrafting Drafting, ComposeScreen Compose)> WaitingOnAClip()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("clip.mp4"));
        compose.Rewrite("Watch this");

        await drafting.Drafting.Send();

        return (drafting, compose);
    }

    /// <summary>Drafting with a compose open over Home.</summary>
    private static ADrafting Composing()
    {
        var drafting = new ADrafting().Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        return drafting;
    }
}
