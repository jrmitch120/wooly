using Wooly.Tests.Fakes;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The status row as a module of its own beside the shell (#436): what it shows, the confirmation that takes every
///     press while it is open, and the wait that lets a remark about a key just pressed go. Driven directly, with a fake
///     host's <c>After</c> for the wait, so no shell is built to know what the row does.
/// </summary>
public class StatusRowTests
{
    private static readonly TimeSpan RemarkFor = TimeSpan.FromSeconds(2);

    private readonly FakeShellHost _host = new();

    private int _changes;

    private StatusRow Row() => new(_host.After, RemarkFor, () => _changes++);

    [Fact]
    public void A_remark_said_for_a_moment_is_let_go_of_once_its_wait_passes()
    {
        var row = Row();

        row.SayForAMoment("Width 90%");

        Assert.Equal(new Shown.Notice("Width 90%", IsError: false), row.Shown);
        Assert.Equal([RemarkFor], _host.Delays);

        _host.Settle();

        Assert.Equal(Shown.Keys, row.Shown);
    }

    [Fact]
    public void Something_said_in_the_meantime_calls_the_wait_off_and_stands()
    {
        var row = Row();

        row.SayForAMoment("Width 90%");
        row.Say("Could not save the width.", isError: true);

        Assert.Equal(0, _host.Waiting);

        _host.Settle();

        Assert.Equal(new Shown.Notice("Could not save the width.", IsError: true), row.Shown);
    }

    [Fact]
    public void Clearing_in_the_meantime_calls_the_wait_off()
    {
        var row = Row();

        row.SayForAMoment("Width 90%");
        row.Clear();

        Assert.Equal(0, _host.Waiting);
        Assert.Equal(Shown.Keys, row.Shown);
    }

    [Fact]
    public void A_confirmation_is_shown_ahead_of_a_notice()
    {
        var row = Row();
        var asked = new Confirmation("Delete this post?", Pressing.Nothing);

        row.Say("Marked as read.", isError: false);
        row.Ask(asked);

        Assert.Equal(new Shown.Asking(asked), row.Shown);
    }

    [Theory]
    [InlineData(ShellKey.Y)]
    [InlineData(ShellKey.D)]
    [InlineData(null)]
    public void A_notice_said_while_a_confirmation_is_open_is_shown_once_it_is_answered(ShellKey? press)
    {
        var row = Row();

        row.Ask(new Confirmation("Delete this post?", Pressing.Nothing));
        row.Say("Marked as read.", isError: false);

        row.Answered(press);

        Assert.Equal(new Shown.Notice("Marked as read.", IsError: false), row.Shown);
    }

    [Fact]
    public void Y_agrees_and_runs_what_the_confirmation_carries()
    {
        var row = Row();
        var agreed = 0;

        row.Ask(new Confirmation("Delete this post?", () => Task.FromResult(agreed++)));

        Assert.True(row.Answered(ShellKey.Y));
        Assert.Equal(1, agreed);
        Assert.Equal(Shown.Keys, row.Shown);
    }

    [Theory]
    [InlineData(ShellKey.Escape)]
    [InlineData(ShellKey.D)]
    [InlineData(null)]
    public void Any_other_press_or_a_click_keeps(ShellKey? press)
    {
        var row = Row();
        var agreed = 0;

        row.Ask(new Confirmation("Delete this post?", () => Task.FromResult(agreed++)));

        Assert.True(row.Answered(press));
        Assert.Equal(0, agreed);
        Assert.Equal(Shown.Keys, row.Shown);
    }

    [Fact]
    public void Asking_and_answering_each_draw_the_row_again()
    {
        var row = Row();

        row.Ask(new Confirmation("Delete this post?", Pressing.Nothing));
        row.Answered(ShellKey.Escape);

        Assert.Equal(2, _changes);
    }

    [Theory]
    [InlineData(ShellKey.Y)]
    [InlineData(null)]
    public void With_no_confirmation_open_a_press_is_not_answered_and_nothing_changes(ShellKey? press)
    {
        var row = Row();

        row.Say("Marked as read.", isError: false);

        var said = _changes;

        Assert.False(row.Answered(press));
        Assert.Equal(said, _changes);
        Assert.Equal(new Shown.Notice("Marked as read.", IsError: false), row.Shown);
    }
}
