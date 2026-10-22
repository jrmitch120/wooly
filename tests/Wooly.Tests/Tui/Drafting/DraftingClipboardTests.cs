using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Drafting;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>ctrl-v</c> on a compose screen (#380), asked of drafting (ADR-0029, #421): a picture or files copied on this
///     machine are attached as a drop is, and anything else is declined, which leaves it to the field's own paste.
/// </summary>
public class DraftingClipboardTests : IDisposable
{
    /// <summary>A small PNG's worth of bytes — what a screenshot on the clipboard reads as.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static readonly Post Somebody = APost.With(id: "220", account: "ben@hachyderm.io");

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>
    ///     A picture on the clipboard is written to a file of its own, named for the paste, and attached from there — going
    ///     up to the instance at once, as anything attached does (ADR-0026).
    /// </summary>
    [Theory]
    [InlineData(ShellKey.C)]
    [InlineData(ShellKey.R)]
    public void APictureIsAttachedAsPastedOne(ShellKey opening)
    {
        var drafting = Composing(opening);

        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        var sent = Assert.Single(drafting.Fakes.Author.Attaching);

        Assert.Equal("pasted-1.png", Path.GetFileName(sent.Path));
        Assert.Equal(Png, File.ReadAllBytes(sent.Path));
        Assert.Equal("pasted-1.png", Assert.Single(drafting.Composing.Media.Attachments).Name);
        Assert.Contains(Texts(drafting.Composing), row => row.Contains("pasted-1.png", StringComparison.Ordinal));
    }

    /// <summary>Each picture pasted is named for its turn, so that two rows never read the same.</summary>
    [Fact]
    public void PicturesAreNumberedInTurn()
    {
        var drafting = Composing();

        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);

        drafting.Press(ShellKey.CtrlV);
        drafting.Press(ShellKey.AltV);

        Assert.Equal(
            ["pasted-1.png", "pasted-2.png"],
            drafting.Composing.Media.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>
    ///     Files copied in a file manager are attached as if dropped: in the order copied, the ones the instance does
    ///     not take left behind, and no more than the post has room for.
    /// </summary>
    [Fact]
    public void CopiedFilesAttachAsADropDoes()
    {
        var drafting = Composing(limits: PostLimits.Default with { Attachments = 2, MediaTypes = ["image/png", "image/gif"] });

        drafting.Fakes.Clipboard.Holding = new Clipped.Files(
        [
            _files.WriteFile("a.png"), _files.WriteFile("notes.txt"), _files.WriteFile("b.gif"),
            _files.WriteFile("c.png"),
        ]);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        Assert.Equal(["a.png", "b.gif"], drafting.Composing.Media.Attachments.Select(attachment => attachment.Name));
        Assert.Equal(2, drafting.Fakes.Author.Attaching.Count);
    }

    /// <summary>
    ///     Copied files the instance takes none of are said so on the status row, and nothing is attached — nor is
    ///     anything pasted for them.
    /// </summary>
    [Fact]
    public void CopiedFilesOfNoAcceptedTypeAreSaidSo()
    {
        var drafting = Composing();

        drafting.Fakes.Clipboard.Holding = new Clipped.Files([_files.WriteFile("notes.txt")]);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        Assert.Empty(drafting.Composing.Media.Attachments);
        Assert.Equal("None of the copied files is a type this instance takes.", drafting.Notice);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>Text on the clipboard — or nothing — is declined, which leaves the paste to the field as it was.</summary>
    [Fact]
    public void TextIsDeclined()
    {
        var drafting = Composing();

        Assert.Equal(Drafted.Declined, drafting.Press(ShellKey.CtrlV));

        Assert.Equal(1, drafting.Fakes.Clipboard.Reads);
        Assert.Empty(drafting.Composing.Media.Attachments);
        Assert.Null(drafting.Notice);
    }

    /// <summary>
    ///     With nothing to read the clipboard with, the status row says what picture paste needs — once a session, not
    ///     at every paste, and not once per draft — and the paste is declined, so text still pastes.
    /// </summary>
    [Fact]
    public void AMissingToolIsSaidOnceASession()
    {
        var drafting = Composing();
        const string why = "Pasting a picture or files needs wl-paste or xclip installed.";

        drafting.Fakes.Clipboard.Holding = new Clipped.NoTool(why);

        Assert.Equal(Drafted.Declined, drafting.Press(ShellKey.CtrlV));
        drafting.Fakes.Host.Drain();
        Assert.Equal(why, drafting.Notice);
        Assert.False(drafting.NoticeIsError);

        drafting.Stack.TakeOff(drafting.Composing);
        drafting.Press(ShellKey.C);

        Assert.Equal(Drafted.Declined, drafting.Press(ShellKey.CtrlV));
        drafting.Fakes.Host.Drain();
        Assert.Null(drafting.Notice);
    }

    /// <summary>
    ///     The edit public profile form's picture paste shares compose's clipboard and what it was told about it: a
    ///     missing tool said over a compose is not said again over the form's avatar, nor one said there over a compose
    ///     opened afterwards — once a session, whichever draft it was pasted into (#422).
    /// </summary>
    [Fact]
    public void AMissingToolIsSaidOnceASessionAcrossBothDrafts()
    {
        var drafting = Composing();
        const string why = "Pasting a picture or files needs wl-paste or xclip installed.";

        drafting.Fakes.Clipboard.Holding = new Clipped.NoTool(why);

        drafting.Press(ShellKey.CtrlV);
        drafting.Fakes.Host.Drain();
        Assert.Equal(why, drafting.Notice);

        drafting.Stack.TakeOff(drafting.Composing);
        OnTheAvatar(drafting);

        drafting.Press(ShellKey.CtrlV);
        drafting.Fakes.Host.Drain();
        Assert.Null(drafting.Notice);
    }

    /// <summary>And the other way about: said first over the form's avatar, it is not said again over a compose.</summary>
    [Fact]
    public void AMissingToolSaidOverTheFormIsNotSaidAgainOverACompose()
    {
        var drafting = new ADrafting().Build();
        const string why = "Pasting a picture or files needs wl-paste or xclip installed.";

        drafting.Fakes.Clipboard.Holding = new Clipped.NoTool(why);

        var form = OnTheAvatar(drafting);

        drafting.Press(ShellKey.CtrlV);
        drafting.Fakes.Host.Drain();
        Assert.Equal(why, drafting.Notice);

        drafting.Stack.TakeOff(form);
        drafting.Press(ShellKey.C);

        Assert.Equal(Drafted.Declined, drafting.Press(ShellKey.CtrlV));
        drafting.Fakes.Host.Drain();
        Assert.Null(drafting.Notice);
    }

    /// <summary>The edit public profile form pushed over what is showing, its walk on the avatar.</summary>
    private static PublicProfileScreen OnTheAvatar(ADrafting drafting)
    {
        var form = new PublicProfileScreen(APublicProfile.With());

        drafting.Stack.Push(form);
        form.TypeInto(new PublicProfileStop(PublicProfilePart.Avatar));

        return form;
    }

    /// <summary>
    ///     The missing tool is said once the field has pasted — after the press, on the drawing thread — since the paste
    ///     is an edit and an edit spends a notice (#364).
    /// </summary>
    [Fact]
    public void AMissingToolIsSaidAfterTheFieldsOwnPaste()
    {
        var drafting = Composing();

        drafting.Fakes.Clipboard.Holding = new Clipped.NoTool("Needs xclip.");

        drafting.Press(ShellKey.CtrlV);

        Assert.Null(drafting.Notice);

        drafting.Fakes.Host.Drain();

        Assert.Equal("Needs xclip.", drafting.Notice);
    }

    /// <summary>
    ///     An edit carries its attachments through as they were (ADR-0008), so <c>ctrl-v</c> on one is declined for the
    ///     field's paste, and the clipboard is not even read.
    /// </summary>
    [Fact]
    public void OnAnEditTheClipboardIsNotRead()
    {
        var drafting = new ADrafting
        {
            Bottom = ADrafting.Home(APost.With(account: "jeff@mastodon.social")),
            Fakes = { Pasted = new PastedPictures(_files.Path) },
        }.Build();

        drafting.Press(ShellKey.E);
        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.Equal(Drafted.Declined, drafting.Press(ShellKey.CtrlV));
        Assert.Equal(0, drafting.Fakes.Clipboard.Reads);
        Assert.Empty(drafting.Fakes.Author.Attaching);
    }

    /// <summary>
    ///     On the attachments screen a short terminal lists the rows on, the paste attaches to the compose under it, as
    ///     <c>ctrl-o</c> there does (story 58).
    /// </summary>
    [Fact]
    public void OnTheAttachmentsScreenAPictureIsAttachedToTheComposeUnderIt()
    {
        var drafting = Composing();
        var compose = drafting.Composing;

        drafting.Drafting.Paste(_files.WriteFile("one.png"));
        drafting.Stack.Push(new AttachmentsScreen(compose));
        drafting.Fakes.Clipboard.Holding = new Clipped.Picture(Png);

        Assert.Equal(Drafted.Used, drafting.Press(ShellKey.CtrlV));

        Assert.IsType<AttachmentsScreen>(drafting.Stack.Top);
        Assert.Equal(["one.png", "pasted-1.png"], compose.Media.Attachments.Select(attachment => attachment.Name));
    }

    /// <summary>Drafting with a compose open over somebody's post — by <paramref name="opening" /> — pasting into a folder of the test's.</summary>
    private ADrafting Composing(ShellKey opening = ShellKey.C, PostLimits? limits = null)
    {
        var drafting = new ADrafting
        {
            Bottom = ADrafting.Home(Somebody),
            Fakes = { Pasted = new PastedPictures(_files.Path), Limits = FakeInstanceLimits.Setting(limits) },
        }.Build();

        drafting.Press(opening);
        drafting.Fakes.Host.Drain();

        return drafting;
    }

    private static IReadOnlyList<string> Texts(ComposeScreen compose) =>
        [.. compose.Lines(new Drawing(78, AShell.Now, Height: 21)).Select(line => line.Text)];
}
