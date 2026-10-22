using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Descriptions on pending attachments (#377), asked of drafting (ADR-0029): the description editor it opens over a
///     row, what is written into it, and the description going up to the instance — as the editor is left, once the
///     attachment is there to put it on, and again whenever it changes — before the post that names it is published.
/// </summary>
public class DraftingDescriptionTests : IDisposable
{
    private readonly TemporaryDirectory _files = new();

    /// <summary>
    ///     <c>enter</c> on a row opens the description editor over the draft, on the attachment the row is.
    /// </summary>
    [Fact]
    public void EnterOnARowOpensTheDescriptionEditor()
    {
        var (drafting, compose) = Attached();

        OntoTheRow(compose);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.Enter));

        var describing = Assert.IsType<DescriptionScreen>(drafting.Stack.Top);

        Assert.Same(compose, describing.Compose);
        Assert.Same(compose.Media.Attachments[0], describing.Attachment);
    }

    /// <summary>What is written into the editor is the attachment's description, and an edit of the draft.</summary>
    [Fact]
    public void WritingIntoTheEditorDescribesTheAttachment()
    {
        var (drafting, compose) = Attached();

        OntoTheRow(compose);
        drafting.Press(ShellKey.Enter);
        drafting.Notice = "Something said.";

        Assert.Equal(DraftChange.Edited, drafting.Drafting.Write("A cat"));

        Assert.Equal("A cat", compose.Media.Attachments[0].Description);
        Assert.Null(drafting.Notice);
    }

    /// <summary>Writing reaches nothing where no description editor is in front.</summary>
    [Fact]
    public void WritingWithNoEditorInFrontChangesNothing()
    {
        var (drafting, compose) = Attached();

        Assert.Equal(DraftChange.None, drafting.Drafting.Write("A cat"));
        Assert.Empty(compose.Media.Attachments[0].Description);
    }

    /// <summary>
    ///     A description written while the file is still going up waits for the attachment to be there, then goes to
    ///     the instance — and the post published names an attachment that carries it.
    /// </summary>
    [Fact]
    public async Task ADescriptionWrittenDuringTheUploadGoesUpOnceItIsReady()
    {
        var (drafting, compose) = Attached();

        Describe(drafting, compose, "A cat");
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Descriptions);

        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Fakes.Host.Drain();

        var described = Assert.Single(drafting.Fakes.Author.Descriptions);

        Assert.Equal(("personal", "m1", "A cat"), (described.Profile, described.Id, described.Description));

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Equal("m1", Assert.Single(Assert.Single(drafting.Fakes.Author.Published).Draft.Attached).Id);
    }

    /// <summary>A description written once the attachment is ready goes up as the editor is left.</summary>
    [Fact]
    public void ADescriptionWrittenOnceReadyGoesUpWhenTheEditorIsLeft()
    {
        var (drafting, compose) = Ready();

        OntoTheRow(compose);
        drafting.Press(ShellKey.Enter);
        drafting.Drafting.Write("A cat");

        Assert.Empty(drafting.Fakes.Author.Descriptions);

        drafting.Stack.TakeOff(drafting.Stack.Top);
        drafting.Fakes.Host.Drain();

        Assert.Equal("A cat", Assert.Single(drafting.Fakes.Author.Descriptions).Description);
    }

    /// <summary>
    ///     A description changed after it went up goes up again, before the post is published — and one left as it was
    ///     is not sent twice.
    /// </summary>
    [Fact]
    public async Task AChangedDescriptionIsSentAgainBeforeThePost()
    {
        var (drafting, compose) = Ready();

        Describe(drafting, compose, "A cat");
        Describe(drafting, compose, "A cat");
        Describe(drafting, compose, "A grey cat");

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Equal(["A cat", "A grey cat"], drafting.Fakes.Author.Descriptions.Select(described => described.Description));
        Assert.Single(drafting.Fakes.Author.Published);
    }

    /// <summary>
    ///     <c>ctrl-s</c> while a description is still on its way waits for it, as it waits for an upload, and then
    ///     sends.
    /// </summary>
    [Fact]
    public async Task SendingWaitsForADescriptionOnItsWay()
    {
        var (drafting, compose) = Ready(holdingDescriptions: true);

        Describe(drafting, compose, "A cat");

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Same(compose, drafting.Stack.Top);
        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Equal("Will send once 1 attachment finishes — esc to stop.", drafting.Notice);

        drafting.Fakes.Author.Descriptions.Single().Land();
        drafting.Fakes.Host.Drain();

        Assert.Single(drafting.Fakes.Author.Published);
        Assert.Equal("Sent.", drafting.Notice);
    }

    /// <summary>
    ///     A description the instance refused is said on the status row, and stops a waiting send rather than letting
    ///     the post go out without it; the next <c>ctrl-s</c> sends it again.
    /// </summary>
    [Fact]
    public async Task ARefusedDescriptionIsSaidAndStopsTheSend()
    {
        var (drafting, compose) = Ready(holdingDescriptions: true);

        Describe(drafting, compose, "A cat");

        await drafting.Drafting.Send();
        drafting.Fakes.Author.Descriptions.Single().Refuse(new AttachmentRefusedException("Description is too long"));
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Same(compose, drafting.Stack.Top);
        Assert.True(drafting.NoticeIsError);
        Assert.Equal("The description of cat.png was refused: Description is too long", drafting.Notice);

        drafting.Fakes.Author.HoldingDescriptions = false;

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Equal(2, drafting.Fakes.Author.Descriptions.Count);
        Assert.Single(drafting.Fakes.Author.Published);
    }

    /// <summary>
    ///     A description that failed for a reason nothing here expected — a bare HTTP error — is said as a refusal is, and
    ///     stops a waiting send rather than leaving it waiting on a description that will never arrive (review of #372).
    /// </summary>
    [Fact]
    public async Task ADescriptionThatFailedAnyWayIsSaidAndStopsTheSend()
    {
        var (drafting, compose) = Ready(holdingDescriptions: true);

        Describe(drafting, compose, "A cat");

        await drafting.Drafting.Send();
        drafting.Fakes.Author.Descriptions.Single().Refuse(
            new HttpRequestException("Response status code does not indicate success"));
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Same(compose, drafting.Stack.Top);
        Assert.True(drafting.NoticeIsError);
        Assert.Equal(
            "The description of cat.png was refused: Response status code does not indicate success",
            drafting.Notice);

        drafting.Fakes.Author.HoldingDescriptions = false;

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Single(drafting.Fakes.Author.Published);
    }

    /// <summary>A post with no descriptions sends with no description sent.</summary>
    [Fact]
    public async Task SendingWithoutDescriptionsSendsNone()
    {
        var (drafting, _) = Ready();

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Single(drafting.Fakes.Author.Published);
        Assert.Empty(drafting.Fakes.Author.Descriptions);
    }

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     Takes the compose's walk up onto its only row, once it has been drawn, as a terminal would have drawn it
    ///     before anything is pressed: the walk keeps to the rows that were drawn (#378).
    /// </summary>
    private static void OntoTheRow(ComposeScreen compose)
    {
        _ = compose.Lines(new Drawing(78, AShell.Now, Height: 21));

        compose.Walk(-1);
    }

    /// <summary>
    ///     Opens the description editor on the only attachment, writes <paramref name="description" /> and leaves it, as
    ///     the shell's <c>esc</c> does, back on the post.
    /// </summary>
    private static void Describe(ADrafting drafting, ComposeScreen compose, string description)
    {
        OntoTheRow(compose);
        drafting.Press(ShellKey.Enter);
        drafting.Drafting.Write(description);
        drafting.Stack.TakeOff(drafting.Stack.Top);
        compose.Walk(1);
    }

    /// <summary>Drafting with a compose open over Home and cat.png attached, still going up.</summary>
    private (ADrafting Drafting, ComposeScreen Compose) Attached(bool holdingDescriptions = false)
    {
        var drafting = new ADrafting().Build();

        drafting.Fakes.Author.HoldingDescriptions = holdingDescriptions;
        drafting.Press(ShellKey.C);
        drafting.Fakes.Host.Drain();

        var compose = drafting.Composing;

        Assert.True(drafting.Drafting.Paste(_files.WriteFile("cat.png")));

        return (drafting, compose);
    }

    /// <summary>As <see cref="Attached" />, with cat.png gone up and ready.</summary>
    private (ADrafting Drafting, ComposeScreen Compose) Ready(bool holdingDescriptions = false)
    {
        var (drafting, compose) = Attached(holdingDescriptions);

        drafting.Fakes.Author.Attaching.Single().Ready();
        drafting.Fakes.Host.Drain();

        return (drafting, compose);
    }
}
