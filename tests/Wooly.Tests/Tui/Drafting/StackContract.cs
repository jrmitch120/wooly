using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The facts every stack drafting reaches holds to (ADR-0029, #427): run against the shell's own and the list the
///     tests use, so the one in the tests cannot come to differ from the one in a session.
/// </summary>
/// <remarks>
///     What drafting is told as screens leave and are uncovered is asserted on what it does with being told (#420): a
///     compose taken off lets its uploads go, and a compose uncovered with a send waiting on it says so again — after
///     the notice is cleared, or the clearing would take it straight back off.
/// </remarks>
public abstract class StackContract : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    [Fact]
    public async Task PushingClearsTheNotice()
    {
        var rig = await Built();

        rig.SaySomething();
        Assert.NotNull(rig.Notice());

        rig.Stack.Push(Screen("Pushed"));

        Assert.Equal("Pushed", rig.Stack.Top.Crumb);
        Assert.Null(rig.Notice());
    }

    [Fact]
    public async Task TakingOffANamedScreenTakesItAndEverythingAboveIt()
    {
        var rig = await Built();
        var bottom = rig.Stack.Top;
        var first = Screen("First");

        rig.Stack.Push(first);
        rig.Stack.Push(Screen("Second"));
        rig.Stack.Push(Screen("Third"));

        rig.Stack.TakeOff(first);

        Assert.Equal([bottom], rig.Stack.Screens);
        Assert.Same(bottom, rig.Stack.Top);
    }

    [Fact]
    public async Task EveryComposeTakenOffIsToldAsLeavingAndLetsItsUploadsGo()
    {
        var rig = await Built();
        var bottom = rig.Stack.Top;

        var under = Composing(rig, "under.png");
        _ = Composing(rig, "over.png");

        rig.Stack.Push(Screen("Over both"));
        rig.Stack.TakeOff(under);

        Assert.Equal([bottom], rig.Stack.Screens);
        Assert.Equal(2, rig.Fakes.Author.Attaching.Count);
        Assert.All(rig.Fakes.Author.Attaching, sending => Assert.True(sending.CalledOff));
    }

    [Fact]
    public async Task TheScreenUncoveredIsToldAfterTheNoticeIsCleared()
    {
        var rig = await Built();
        var compose = Composing(rig, "clip.mp4");
        var over = Screen("Over");

        compose.Rewrite("Watch this");
        await rig.Drafting.Send();
        rig.Stack.Push(over);

        Assert.Null(rig.Notice());

        rig.Stack.TakeOff(over);

        Assert.Same(compose, rig.Stack.Top);
        Assert.Equal("Will send once 1 attachment finishes — esc to stop.", rig.Notice());
        Assert.False(rig.Fakes.Author.Attaching.Single().CalledOff);
    }

    [Fact]
    public async Task TakingOffAScreenAlreadyGoneDoesNothing()
    {
        var rig = await Built();
        var gone = Screen("Gone");

        rig.Stack.Push(gone);
        rig.Stack.TakeOff(gone);

        var compose = Composing(rig, "cat.png");

        rig.SaySomething();

        rig.Stack.TakeOff(gone);

        Assert.Same(compose, rig.Stack.Top);
        Assert.Equal(2, rig.Stack.Screens.Count);
        Assert.NotNull(rig.Notice());
        Assert.False(rig.Fakes.Author.Attaching.Single().CalledOff);
    }

    [Fact]
    public async Task TheBottomScreenIsNeverTakenOff()
    {
        var rig = await Built();
        var bottom = rig.Stack.Top;

        rig.SaySomething();
        rig.Stack.TakeOff(bottom);

        Assert.Equal([bottom], rig.Stack.Screens);
        Assert.NotNull(rig.Notice());
    }

    public void Dispose() => _files.Dispose();

    /// <summary>A stack holding one screen at the bottom, with the drafting it tells and a status row to read.</summary>
    private protected abstract Task<Rig> Built();

    /// <summary>A screen that is about nothing, called <paramref name="crumb" />.</summary>
    private static FeedScreen Screen(string crumb) => new(new Destination(DestinationKind.Hashtag, crumb), []);

    /// <summary>A compose opened through drafting, with <paramref name="file" /> dropped on it and going up.</summary>
    private ComposeScreen Composing(Rig rig, string file)
    {
        Assert.Equal(Drafted.Used, rig.Drafting.Do(Verb.Compose));

        var compose = Assert.IsType<ComposeScreen>(rig.Stack.Top);

        Assert.True(rig.Drafting.Paste(_files.WriteFile(file)));

        return compose;
    }

    /// <summary>
    ///     The stack under test, the drafting it tells, the fakes drafting reaches the instance through, what the status
    ///     row says and a way to make it say something.
    /// </summary>
    private protected sealed record Rig(
        IStack Stack,
        Wooly.Tui.Drafting.Drafting Drafting,
        AShell Fakes,
        Func<string?> Notice,
        Action SaySomething);
}

/// <summary>The list the tests build drafting over, held to <see cref="StackContract" />.</summary>
public class ListStackContractTests : StackContract
{
    private protected override Task<Rig> Built()
    {
        var built = new ADrafting().Build();

        return Task.FromResult(new Rig(
            built.Stack,
            built.Drafting,
            built.Fakes,
            () => built.Notice,
            () => built.Notice = "Something said."));
    }
}

/// <summary>The shell's own stack, as drafting reaches it, held to <see cref="StackContract" />.</summary>
public class ShellStackContractTests : StackContract
{
    private protected override async Task<Rig> Built()
    {
        var fakes = new AShell();
        var shell = await fakes.Opened();

        // Already at the whole terminal, which + refuses on the status row, whatever is in front.
        return new Rig(shell.Stack, shell.Drafting, fakes, () => shell.Notice, shell.Widen);
    }
}
