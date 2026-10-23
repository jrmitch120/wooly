using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Fixing and rearranging what is attached to a compose (#378), asked of drafting (ADR-0029, #421): walking its rows
///     from the editor, a refusal that says why and what a retry can mend, taking one off and bringing it back, putting
///     them in another order, and a drop past the instance's limit. The pointer on the rows is
///     <see cref="ComposeAttachmentRowsTests" />'.
/// </summary>
public class DraftingAttachmentRowsTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private const int Height = 21;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     <c>↑</c> from the editor walks the rows from the bottom, then the Media header, then Warn; <c>↓</c> walks
    ///     back. The row walked to carries the selection bar against its grip, and the header its words highlighted.
    /// </summary>
    [Fact]
    public void UpFromTheEditorWalksTheRowsThenTheHeaderThenWarn()
    {
        var (drafting, compose) = Composing("one.png", "two.png");

        drafting.Press(ShellKey.Up);

        Assert.StartsWith("    ▌⠶     two.png", Row(compose, "two.png"), StringComparison.Ordinal);
        Assert.StartsWith("     ⠶     one.png", Row(compose, "one.png"), StringComparison.Ordinal);

        drafting.Press(ShellKey.Up);

        Assert.StartsWith("    ▌⠶     one.png", Row(compose, "one.png"), StringComparison.Ordinal);

        drafting.Press(ShellKey.Up);

        Assert.DoesNotContain(Texts(compose), row => row.Contains('▌', StringComparison.Ordinal));
        Assert.Equal(
            Role.SelectedText,
            Media(compose).Spans.Single(span => span.Text.StartsWith("2 of 4", StringComparison.Ordinal)).Role);

        drafting.Press(ShellKey.Up);

        Assert.Equal(ComposeField.Warning, compose.Typing);

        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);

        Assert.Equal(ComposeField.Post, compose.Typing);
        Assert.DoesNotContain(Texts(compose), row => row.Contains('▌', StringComparison.Ordinal));
    }
    /// <summary>
    ///     A dropped connection offers <c>retry (r)</c> after its reason, and <c>r</c> on its row sends the file up
    ///     again — which, once it is through, lets the post go.
    /// </summary>
    [Fact]
    public async Task RetryingADroppedConnectionThatGoesThroughUnblocksTheSend()
    {
        var (drafting, compose) = Composing("cat.png");

        drafting.Fakes.Author.Attaching.Single().Refuse(Dropped());
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  connection lost  retry (r)", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);

        await drafting.Drafting.Send();

        Assert.Empty(drafting.Fakes.Author.Published);

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.R);

        Assert.Equal(2, drafting.Fakes.Author.Attaching.Count);
        Assert.Equal(drafting.Fakes.Author.Attaching[0].Path, drafting.Fakes.Author.Attaching[1].Path);
        Assert.EndsWith("x  ░░░░░░░░   0%", Row(compose, "cat.png"), StringComparison.Ordinal);

        drafting.Fakes.Author.Attaching[1].Ready();
        drafting.Fakes.Host.Drain();
        await drafting.Drafting.Send();

        Assert.Equal("m2", Assert.Single(Assert.Single(drafting.Fakes.Author.Published).Draft.Attached).Id);
    }
    /// <summary>
    ///     A file the instance will not take is never offered a retry, which could not mend it: <c>r</c> on its row sends
    ///     nothing up, and the send stays blocked until it is taken off.
    /// </summary>
    [Fact]
    public async Task ARefusalARetryCannotMendOffersNone()
    {
        var (drafting, compose) = Composing("huge.png");

        drafting.Fakes.Author.Attaching.Single().Refuse(new AttachmentRefusedException("File is too large"));
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  File is too large", Row(compose, "huge.png", width: 120), StringComparison.Ordinal);

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.R);

        Assert.Single(drafting.Fakes.Author.Attaching);

        await drafting.Drafting.Send();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.True(drafting.NoticeIsError);
    }
    /// <summary>
    ///     Every failure trying again could mend offers <c>retry (r)</c> after its reason, as a dropped connection does:
    ///     a rate limit, which passes; an instance that failed to answer; and a call the client gave up waiting on
    ///     (review of #372).
    /// </summary>
    [Theory]
    [MemberData(nameof(Passing))]
    public void EveryFailureARetryCanMendOffersOne(Exception failure, string said)
    {
        var (drafting, compose) = Composing("cat.png");

        drafting.Fakes.Author.Attaching.Single().Refuse(failure);
        drafting.Fakes.Host.Drain();

        Assert.EndsWith($"x  {said}  retry (r)", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.R);

        Assert.Equal(2, drafting.Fakes.Author.Attaching.Count);
    }
    /// <summary>The failures a retry can mend, and what the row says of each.</summary>
    public static TheoryData<Exception, string> Passing => new()
    {
        { new RateLimitedException("mastodon.social", resetsAt: null), "rate limited" },
        { new InstanceFailedException("mastodon.social", System.Net.HttpStatusCode.BadGateway), "instance failed (502)" },
        { new TaskCanceledException("The request timed out."), "timed out" },
    };

    /// <summary>
    ///     A failure nothing here expected — a bare HTTP error, a file that could not be read — still ends the row as
    ///     refused, saying what it was, rather than leaving it going up forever; and a send waiting on it is not left
    ///     waiting, but stopped and said, as a refusal stops it (review of #372).
    /// </summary>
    [Fact]
    public async Task AFailureNothingExpectedEndsTheRowAndTheWaitingSend()
    {
        var (drafting, compose) = Composing("cat.png");

        await drafting.Drafting.Send();

        Assert.StartsWith("Will send once", drafting.Notice, StringComparison.Ordinal);

        drafting.Fakes.Author.Attaching.Single().Refuse(new IOException("The file could not be read"));
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  The file could not be read", Row(compose, "cat.png", width: 120), StringComparison.Ordinal);
        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.True(drafting.NoticeIsError);

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.R);

        Assert.Single(drafting.Fakes.Author.Attaching);
    }
    /// <summary>
    ///     At 80 columns there is not room for the reason and <c>retry (r)</c> both: the offer shortens to <c>(r)</c>,
    ///     and a reason too long for what is left is cut.
    /// </summary>
    [Fact]
    public void OnANarrowTerminalTheRetryShortensAndTheReasonIsCut()
    {
        var (drafting, compose) = Composing("cat.png");

        drafting.Fakes.Author.Attaching.Single().Refuse(Dropped());
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  connection l…  (r)", Row(compose, "cat.png"), StringComparison.Ordinal);
    }
    /// <summary>
    ///     <c>del</c> or <c>backspace</c> on a row takes it off the post that is sent, and the walk stays on the row that
    ///     took its place.
    /// </summary>
    [Theory]
    [InlineData(ShellKey.Delete)]
    [InlineData(ShellKey.Backspace)]
    public async Task RemovingTakesItOffThePostThatIsSent(ShellKey key)
    {
        var (drafting, compose) = Composing("one.png", "two.png", "three.png");

        AllReady(drafting);
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Up);
        drafting.Press(key);

        Assert.Equal(["one.png", "three.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);
        Assert.Contains("  Media  2 of 4 · ctrl-o to add · □ sensitive", Texts(compose));

        await drafting.Drafting.Send();

        Assert.Equal(["m1", "m3"], Assert.Single(drafting.Fakes.Author.Published).Draft.Attached.Select(attached => attached.Id));
    }
    /// <summary>
    ///     Taking off the last thing attached leaves the walk on the Media header, where <c>ctrl-z</c> still brings it
    ///     back.
    /// </summary>
    [Fact]
    public void RemovingTheLastOneLeavesTheWalkOnTheHeader()
    {
        var (drafting, compose) = Composing("one.png");

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Delete);

        Assert.Empty(compose.Media.Attachments);
        Assert.Equal(ComposeField.Media, compose.Typing);

        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png"], Names(compose));
    }
    /// <summary><c>s</c> on a row toggles sensitive, as it does on the header over it (#379).</summary>
    [Fact]
    public void SOnARowTogglesSensitive()
    {
        var (drafting, compose) = Composing("one.png");

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.S);

        Assert.Contains("  Media  1 of 4 · ctrl-o to add · ■ sensitive", Texts(compose));
        Assert.Equal(ComposeField.Attachment, compose.Typing);
    }
    /// <summary>
    ///     <c>ctrl-z</c> on the Media rows brings back the one last taken off, in its place and as far as it had got —
    ///     one deep — and the walk lands on it.
    /// </summary>
    [Fact]
    public void CtrlZBringsBackTheLastRemovalInItsPlace()
    {
        var (drafting, compose) = Composing("one.png", "two.png", "three.png");

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Delete);
        drafting.Press(ShellKey.Delete);

        Assert.Equal(["one.png"], Names(compose));

        // What was taken off still hears how its upload went, so that it comes back as far as it had got.
        drafting.Fakes.Author.Attaching[2].Ready();
        drafting.Fakes.Host.Drain();

        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);
        Assert.EndsWith("x  no alt text", Row(compose, "three.png"), StringComparison.Ordinal);

        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
    }
    /// <summary>
    ///     <c>ctrl-z</c> is the Media rows' only while the walk is on them: from the post it is the editor's own undo,
    ///     and brings nothing back.
    /// </summary>
    [Fact]
    public void CtrlZOffTheMediaRowsBringsNothingBack()
    {
        var (drafting, compose) = Composing("one.png", "two.png");

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Delete);
        drafting.Press(ShellKey.Down);
        drafting.Press(ShellKey.Down);

        Assert.Equal(ComposeField.Post, compose.Typing);

        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png"], Names(compose));
    }
    /// <summary>Nothing comes back onto a post that has no room for it.</summary>
    [Fact]
    public void CtrlZBringsNothingBackOntoAFullPost()
    {
        var (drafting, compose) = Composing(PostLimits.Default with { Attachments = 2 }, "one.png", "two.png");

        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.Delete);
        drafting.Drafting.Paste(_files.WriteFile("three.png"));
        drafting.Press(ShellKey.CtrlZ);

        Assert.Equal(["one.png", "three.png"], Names(compose));
    }
    /// <summary>
    ///     <c>shift-↑</c> and <c>shift-↓</c> move the picked row a place earlier or later, still picked, and the post
    ///     goes out in the order shown.
    /// </summary>
    [Fact]
    public async Task ShiftArrowsReorderAndThePostGoesOutInTheOrderShown()
    {
        var (drafting, compose) = Composing("one.png", "two.png", "three.png");

        AllReady(drafting);
        drafting.Press(ShellKey.Up);
        drafting.Press(ShellKey.ShiftUp);
        drafting.Press(ShellKey.ShiftUp);
        drafting.Press(ShellKey.ShiftUp);

        Assert.Equal(["three.png", "one.png", "two.png"], Names(compose));
        Assert.StartsWith("    ▌⠶     three.png", Row(compose, "three.png"), StringComparison.Ordinal);

        drafting.Press(ShellKey.ShiftDown);

        Assert.Equal(["one.png", "three.png", "two.png"], Names(compose));

        await drafting.Drafting.Send();

        Assert.Equal(
            ["m1", "m3", "m2"],
            Assert.Single(drafting.Fakes.Author.Published).Draft.Attached.Select(attached => attached.Id));
    }
    /// <summary>
    ///     On a row the status row offers what can be done there: moving it, taking it off, a retry only where one can
    ///     help, and bringing back what was taken off.
    /// </summary>
    [Fact]
    public void OnARowTheStatusRowOffersItsKeys()
    {
        var (drafting, _) = Composing("one.png", "two.png");

        drafting.Fakes.Author.Attaching[1].Refuse(Dropped());
        drafting.Fakes.Host.Drain();
        drafting.Press(ShellKey.Up);

        Assert.Contains(new KeyHint("r", "retry"), drafting.Stack.Top.Keys);
        Assert.Contains(new KeyHint("del", "remove"), drafting.Stack.Top.Keys);
        Assert.Contains(new KeyHint("shift-↑↓", "move"), drafting.Stack.Top.Keys);
        Assert.DoesNotContain(drafting.Stack.Top.Keys, hint => hint.Key == "ctrl-z");

        drafting.Press(ShellKey.Up);

        Assert.DoesNotContain(drafting.Stack.Top.Keys, hint => hint.Key == "r");

        drafting.Press(ShellKey.Delete);

        Assert.Contains(new KeyHint("ctrl-z", "bring back"), drafting.Stack.Top.Keys);
    }
    /// <summary>
    ///     A drop of more than the post has room for attaches up to the limit, and the status row says how many were
    ///     left out; a drop onto a full post attaches nothing and says so.
    /// </summary>
    [Fact]
    public void ADropPastTheLimitSaysHowManyWereLeftOut()
    {
        var (drafting, compose) = Composing(PostLimits.Default with { Attachments = 3 });
        var paths = new[] { "a.png", "b.png", "c.png", "d.png", "e.png" }.Select(name => _files.WriteFile(name));

        Assert.True(drafting.Drafting.Paste(string.Join(' ', paths)));

        Assert.Equal(["a.png", "b.png", "c.png"], Names(compose));
        Assert.Equal("2 left out — 3 is the most a post can carry.", drafting.Notice);

        Assert.True(drafting.Drafting.Paste(_files.WriteFile("f.png")));

        Assert.Equal(3, compose.Media.Attachments.Count);
        Assert.Equal("Nothing attached — 3 is the most a post can carry.", drafting.Notice);
    }

    /// <summary>
    ///     Drafting with a fresh compose open, <paramref name="names" /> dropped onto it, each one still going up.
    /// </summary>
    private (ADrafting Drafting, ComposeScreen Compose) Composing(params string[] names) => Composing(null, names);

    /// <summary>The same, on an instance setting <paramref name="limits" />.</summary>
    private (ADrafting Drafting, ComposeScreen Compose) Composing(PostLimits? limits, params string[] names)
    {
        var drafting = new ADrafting { Fakes = { Limits = FakeInstanceLimits.Setting(limits) } }.Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        var compose = drafting.Composing;

        foreach (var name in names)
        {
            Assert.True(drafting.Drafting.Paste(_files.WriteFile(name)));
        }

        // Drawn once, as a terminal would have before anything is pressed: the walk keeps to what was drawn.
        _ = Lines(compose);

        return (drafting, compose);
    }

    /// <summary>A connection that dropped while a file was going up, the one failure a retry can mend.</summary>
    private static TransientNetworkException Dropped() =>
        new(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException());

    /// <summary>Every attachment ready, as the instance answered them.</summary>
    private static void AllReady(ADrafting drafting)
    {
        foreach (var sending in drafting.Fakes.Author.Attaching)
        {
            sending.Ready();
        }

        drafting.Fakes.Host.Drain();
    }

    /// <summary>The row naming <paramref name="name" />.</summary>
    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        Texts(compose, width).Single(row => row.Contains(name, StringComparison.Ordinal));

    /// <summary>The names on the rows, top to bottom.</summary>
    private static IEnumerable<string> Names(ComposeScreen compose) =>
        compose.Media.Attachments.Select(attachment => attachment.Name);

    private static Line Media(ComposeScreen compose) =>
        Lines(compose).Single(line => line.Text.StartsWith("  Media", StringComparison.Ordinal));

    private static IReadOnlyList<Line> Lines(ComposeScreen compose, int width = Width) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: Height));

    private static IReadOnlyList<string> Texts(ComposeScreen compose, int width = Width) =>
        [.. Lines(compose, width).Select(line => line.Text)];
}
