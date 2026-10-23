using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pending attachments going up (ADR-0026, #375), asked of drafting, which sends each one up as it is attached and
///     feeds what the instance answers into the compose it belongs to (ADR-0029): its progress, ready, and refused.
/// </summary>
public class DraftingUploadTests : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal.</summary>
    private const int Width = 78;

    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     A file dropped on a compose is attached rather than typed, and starts going up as the profile acted as at
    ///     once (ADR-0026).
    /// </summary>
    [Fact]
    public void ADroppedFileIsAttachedAndSentUp()
    {
        var drafting = Composing();
        var cat = _files.WriteFile("cat.png");

        Assert.True(drafting.Drafting.Paste(cat));

        Assert.Equal(cat, Assert.Single(drafting.Fakes.Author.Attaching).Path);
        Assert.Equal("cat.png", Assert.Single(drafting.Composing.Media.Attachments).Name);
        Assert.Equal("personal", drafting.Fakes.Author.Attaching[0].Profile);
        Assert.Contains("token-personal", drafting.Fakes.Tokens);
    }

    /// <summary>A drop of more files than the post has room for attaches as many as fit, in the order dropped.</summary>
    [Fact]
    public void ADropPastTheLimitAttachesWhatFits()
    {
        var drafting = Composing(PostLimits.Default with { Attachments = 2 });
        var paths = new[] { "a.png", "b.png", "c.png" }.Select(name => _files.WriteFile(name)).ToList();

        Assert.True(drafting.Drafting.Paste(string.Join(' ', paths)));

        Assert.Equal(["a.png", "b.png"], drafting.Composing.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, drafting.Fakes.Author.Attaching.Count);
        Assert.Equal("1 left out — 2 is the most a post can carry.", drafting.Notice);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>
    ///     Paths come the ways terminals paste a drop: several at once, spaces escaped with a backslash, or in quotes —
    ///     and every one of them is attached, in order.
    /// </summary>
    [Fact]
    public void DroppedPathsMayBeEscapedOrQuoted()
    {
        var drafting = Composing();
        var spaced = _files.WriteFile("a cat.png");
        var quoted = _files.WriteFile("a dog.jpg");
        var plain = _files.WriteFile("bird.gif");

        Assert.True(drafting.Drafting.Paste($"{spaced.Replace(" ", "\\ ", StringComparison.Ordinal)} '{quoted}' {plain}"));

        Assert.Equal(
            ["a cat.png", "a dog.jpg", "bird.gif"],
            drafting.Composing.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(
            ["picture", "picture", "animation"],
            drafting.Composing.Media.Attachments.Select(attachment => attachment.KindWord));
    }

    /// <summary>
    ///     Anything that is not entirely existing files the instance accepts is text, left for the field to paste: a
    ///     sentence with a path in it, a file of a type the instance does not take, a path with nothing there, a name
    ///     that is not a whole path.
    /// </summary>
    [Fact]
    public void AnyOtherPasteIsText()
    {
        var drafting = Composing(PostLimits.Default with { MediaTypes = ["image/png"] });
        var cat = _files.WriteFile("cat.png");

        Assert.False(drafting.Drafting.Paste($"look at {cat}"));
        Assert.False(drafting.Drafting.Paste(_files.WriteFile("notes.txt")));
        Assert.False(drafting.Drafting.Paste(_files.WriteFile("dog.jpg")));
        Assert.False(drafting.Drafting.Paste($"{cat} {Path.Combine(_files.Path, "gone.png")}"));
        Assert.False(drafting.Drafting.Paste("cat.png"));
        Assert.False(drafting.Drafting.Paste("   "));

        Assert.Empty(drafting.Composing.Media.Attachments);
        Assert.Empty(drafting.Fakes.Author.Attaching);
    }

    /// <summary>An edit carries the post's attachments through as they were, and a drop on one is text (ADR-0008).</summary>
    [Fact]
    public void ADropOnAnEditIsText()
    {
        var drafting = new ADrafting { Bottom = ADrafting.Home(APost.With(account: "jeff@mastodon.social")) }.Build();

        drafting.Press(ShellKey.E);

        Assert.False(drafting.Drafting.Paste(_files.WriteFile("cat.png")));
        Assert.Empty(drafting.Fakes.Author.Attaching);
    }

    /// <summary>A drop on anything but a compose is text too: there is no draft in front to attach it to.</summary>
    [Fact]
    public void ADropWithNoComposeInFrontIsText()
    {
        var drafting = new ADrafting().Build();

        Assert.False(drafting.Drafting.Paste(_files.WriteFile("cat.png")));
        Assert.Empty(drafting.Fakes.Author.Attaching);
    }

    /// <summary>
    ///     A row says where its attachment has got to, in one column: the upload's gauge and share, then that the
    ///     instance is processing it, then — ready, with nothing written about it — the quiet mark.
    /// </summary>
    [Fact]
    public void ARowMovesThroughUploadingProcessingAndReady()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));

        var sending = drafting.Fakes.Author.Attaching.Single();

        sending.Progress(0.54);
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  ████░░░░  54%", Row(compose, "cat.png"), StringComparison.Ordinal);

        sending.Processing();
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  processing", Row(compose, "cat.png"), StringComparison.Ordinal);

        sending.Ready();
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  no alt text", Row(compose, "cat.png"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     An attachment the instance refused says why on its row, and a send will not go out without it — nor wait on
    ///     it.
    /// </summary>
    [Fact]
    public async Task ARefusedAttachmentSaysWhyAndStopsTheSend()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("huge.png"));
        drafting.Fakes.Author.Attaching.Single().Refuse(new AttachmentRefusedException("File is too large"));
        drafting.Fakes.Host.Drain();

        Assert.EndsWith("x  File is too large", Row(compose, "huge.png"), StringComparison.Ordinal);

        await drafting.Drafting.Send();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("Something attached was refused — take it off to send.", drafting.Notice);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>A dropped connection says so on its row, as the one failure trying again could mend.</summary>
    [Fact]
    public void ADroppedConnectionSaysSo()
    {
        var drafting = Composing();

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Fakes.Author.Attaching.Single().Refuse(
            new TransientNetworkException(new Uri("https://mastodon.social/api/v2/media"), 3, new HttpRequestException()));
        drafting.Fakes.Host.Drain();

        Assert.EndsWith(
            "x  connection lost  retry (r)",
            Row(drafting.Composing, "cat.png", width: 120),
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>ctrl-s</c> with everything ready publishes the post carrying the pending attachments by id, in the order
    ///     they were attached.
    /// </summary>
    [Fact]
    public async Task SendingPublishesTheAttachmentsInOrder()
    {
        var drafting = Composing();

        drafting.Drafting.Paste(_files.WriteFile("first.png"));
        drafting.Drafting.Paste(_files.WriteFile("second.mp4"));
        drafting.Composing.Rewrite("Two things");

        drafting.Fakes.Author.Attaching[1].Ready(MediaKind.Video);
        drafting.Fakes.Author.Attaching[0].Ready();
        drafting.Fakes.Host.Drain();

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        var draft = Assert.Single(drafting.Fakes.Author.Published).Draft;

        Assert.Equal(["m1", "m2"], draft.Attached.Select(attached => attached.Id));
        Assert.Equal("Two things", draft.Text);
        Assert.IsNotType<ComposeScreen>(drafting.Stack.Top);
    }

    /// <summary>A post made of nothing but what is attached sends, as Mastodon allows.</summary>
    [Fact]
    public async Task APostWithOnlyAttachmentsSends()
    {
        var drafting = Composing();

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Fakes.Host.Drain();

        await drafting.Drafting.Send();

        var draft = Assert.Single(drafting.Fakes.Author.Published).Draft;

        Assert.Equal(string.Empty, draft.Text);
        Assert.Equal("m1", Assert.Single(draft.Attached).Id);
    }

    /// <summary>A compose taken off the stack calls off whatever it is still sending up (#375).</summary>
    [Fact]
    public void AComposeLeavingLetsItsUploadsGo()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Stack.TakeOff(compose);

        Assert.True(drafting.Fakes.Author.Attaching.Single().CalledOff);
    }

    /// <summary>A screen over the compose leaving lets nothing go: the compose is still on the stack, uploading.</summary>
    [Fact]
    public void AScreenOverTheComposeLeavingLetsNothingGo()
    {
        var drafting = Composing();
        var over = ADrafting.Home();

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Stack.Push(over);
        drafting.Stack.TakeOff(over);

        Assert.False(drafting.Fakes.Author.Attaching.Single().CalledOff);
    }

    /// <summary>
    ///     An upload's report already on its way as its compose leaves the stack is ignored: the compose that has gone is
    ///     left as it was, nothing is said and nothing is redrawn (#425).
    /// </summary>
    [Fact]
    public void AReportForAComposeThatHasLeftIsIgnored()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Stack.TakeOff(compose);
        drafting.Notice = "untouched";

        var changes = drafting.Changes;

        drafting.Fakes.Host.Drain();

        Assert.IsType<AttachmentState.Sending>(compose.Media.Attachments.Single().State);
        Assert.Equal(changes, drafting.Changes);
        Assert.Equal("untouched", drafting.Notice);
    }

    /// <summary>
    ///     A report for a compose still on the stack under a screen opened over it lands as ever: the compose is not
    ///     in front, but it has not gone (#425).
    /// </summary>
    [Fact]
    public void AReportForAComposeUnderAScreenStillLands()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("cat.png"));
        drafting.Stack.Push(ADrafting.Home());
        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Fakes.Host.Drain();

        Assert.IsType<AttachmentState.Ready>(compose.Media.Attachments.Single().State);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>Drafting with a compose open over Home, its instance setting <paramref name="limits" />.</summary>
    private static ADrafting Composing(PostLimits? limits = null)
    {
        var drafting = new ADrafting { Fakes = { Limits = FakeInstanceLimits.Setting(limits) } }.Build();

        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        return drafting;
    }

    /// <summary>The row naming <paramref name="name" />, drawn <paramref name="width" /> wide.</summary>
    private static string Row(ComposeScreen compose, string name, int width = Width) =>
        compose.Lines(new Drawing(width, AShell.Now, Height: 21))
            .Select(line => line.Text)
            .Single(row => row.Contains(name, StringComparison.Ordinal));
}
