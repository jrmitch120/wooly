using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The facts every stack drafting reaches holds to (ADR-0029, #427): run against the shell's own and the list the
///     tests use, so the one in the tests cannot come to differ from the one in a session.
/// </summary>
public abstract class StackContract
{
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
    public async Task EachScreenIsToldAsLeavingBeforeItIsOffAndTheUncoveredAfter()
    {
        var rig = await Built();
        var bottom = rig.Stack.Top;
        var first = Screen("First");
        var second = Screen("Second");
        var told = new List<string>();

        rig.Stack.Push(first);
        rig.Stack.Push(second);
        rig.SaySomething();

        rig.Drafting.Leaving += screen =>
            told.Add($"{screen.Crumb} leaving, on the stack: {rig.Stack.Screens.Contains(screen)}");
        rig.Drafting.Uncovering += () =>
            told.Add($"{rig.Stack.Top.Crumb} uncovered, notice: {rig.Notice() ?? "none"}");

        rig.Stack.TakeOff(first);

        Assert.Equal(
            [
                "Second leaving, on the stack: True",
                "First leaving, on the stack: True",
                $"{bottom.Crumb} uncovered, notice: none",
            ],
            told);
    }

    [Fact]
    public async Task TakingOffAScreenAlreadyGoneDoesNothing()
    {
        var rig = await Built();
        var gone = Screen("Gone");
        var told = 0;

        rig.Stack.Push(gone);
        rig.Stack.TakeOff(gone);
        rig.Stack.Push(Screen("Standing"));
        rig.SaySomething();

        rig.Drafting.Leaving += _ => told++;
        rig.Drafting.Uncovering += () => told++;

        rig.Stack.TakeOff(gone);

        Assert.Equal("Standing", rig.Stack.Top.Crumb);
        Assert.Equal(2, rig.Stack.Screens.Count);
        Assert.NotNull(rig.Notice());
        Assert.Equal(0, told);
    }

    /// <summary>A stack holding one screen at the bottom, with the drafting it tells and a status row to read.</summary>
    private protected abstract Task<Rig> Built();

    /// <summary>A screen that is about nothing, called <paramref name="crumb" />.</summary>
    private static FeedScreen Screen(string crumb) => new(new Destination(DestinationKind.Hashtag, crumb), []);

    /// <summary>The stack under test, the drafting it tells, what the status row says and a way to make it say something.</summary>
    private protected sealed record Rig(
        IStack Stack,
        Wooly.Tui.Drafting.Drafting Drafting,
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
            () => built.Notice,
            () => built.Notice = "Something said."));
    }
}

/// <summary>The shell's own stack, as drafting reaches it, held to <see cref="StackContract" />.</summary>
public class ShellStackContractTests : StackContract
{
    private protected override async Task<Rig> Built()
    {
        var shell = await new AShell().Opened();

        // Already at the whole terminal, which + refuses on the status row, whatever is in front.
        return new Rig(shell.Stack, shell.Drafting, () => shell.Notice, shell.Widen);
    }
}
