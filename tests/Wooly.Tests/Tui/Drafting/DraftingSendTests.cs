using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>ctrl-s</c> on a compose screen, which the window carries to drafting (ADR-0029): the checks before anything
///     goes out, publishing a post or a reply and saving an edit, and the compose taken off once it has gone.
/// </summary>
public class DraftingSendTests
{
    private static readonly Post Mine = APost.With(id: "110", account: "jeff@mastodon.social");

    private static readonly Post Somebody = APost.With(id: "220", account: "ben@hachyderm.io");

    /// <summary>Compose opens an editor with nothing in it, and sending publishes what was written.</summary>
    [Fact]
    public async Task SendingPublishesWhatWasComposed()
    {
        var drafting = Over(Somebody);

        drafting.Press(ShellKey.C);

        var compose = drafting.Composing;

        compose.Text = "Hello from the rail";

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        var published = Assert.Single(drafting.Fakes.Author.Published);

        Assert.Equal("Hello from the rail", published.Draft.Text);
        Assert.Null(published.Draft.InReplyTo);
        Assert.Equal("personal", published.Profile);

        // The editor is closed once what was in it has gone out, and the reader told.
        Assert.IsType<FeedScreen>(drafting.Stack.Top);
        Assert.Equal("Sent.", drafting.Notice);
    }

    /// <summary>
    ///     A post the instance turns down is said, in the instance's words, over the draft — which stays where it is,
    ///     every word of it, for the writer to cut down and send again.
    /// </summary>
    [Fact]
    public async Task SendingSaysWhyTheInstanceRefusedThePostAndKeepsTheDraft()
    {
        var drafting = new ADrafting
        {
            Fakes = { Author = FakePostAuthor.Refusing(new PostRefusedException("Text character limit of 500 exceeded")) },
        }.Build();

        drafting.Press(ShellKey.C);

        var compose = drafting.Composing;

        compose.Text = "Far too long";

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Contains("character limit of 500 exceeded", drafting.Notice, StringComparison.Ordinal);
        Assert.True(drafting.NoticeIsError);
        Assert.Same(compose, drafting.Stack.Top);
        Assert.Equal("Far too long", compose.Text);
    }

    /// <summary>A reply is a draft that names what it answers, which is what the port already takes.</summary>
    [Fact]
    public async Task SendingAReplyNamesThePickedPost()
    {
        var drafting = Over(Somebody);

        drafting.Press(ShellKey.R);
        drafting.Composing.Text = "Answering you";

        await drafting.Drafting.Send();

        Assert.Equal("220", Assert.Single(drafting.Fakes.Author.Published).Draft.InReplyTo);
    }

    /// <summary>
    ///     A reply that is nothing but the mention it opened with says nothing the reader wrote, so it is refused —
    ///     which covers a public reply too, since a public reply opens with one.
    /// </summary>
    [Fact]
    public async Task SendingRefusesAPublicReplyWithNothingButTheMentionItOpenedWith()
    {
        var drafting = Over(Somebody);

        drafting.Press(ShellKey.R);

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>An edit of the profile's own post is saved rather than published, and said so.</summary>
    [Fact]
    public async Task SendingSavesAnEditOfTheProfilesOwnPost()
    {
        var drafting = Over(Mine);

        drafting.Press(ShellKey.E);

        var compose = drafting.Composing;

        Assert.Equal("Hello world", compose.Text);

        compose.Text = "Hello world, fixed";

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        var edited = Assert.Single(drafting.Fakes.Author.Edits);

        Assert.Equal("110", edited.PostId);
        Assert.Equal("Hello world, fixed", edited.Edit.Text);
        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.IsType<FeedScreen>(drafting.Stack.Top);
        Assert.Equal("Saved.", drafting.Notice);
    }

    /// <summary>
    ///     Saving an edit of a post with attachments sends the change it always did — the text and the warning, nothing
    ///     about attachments — so the post's own carry through as ADR-0008 has them, and nothing is sent up or published
    ///     afresh.
    /// </summary>
    [Fact]
    public async Task SavingAnEditCarriesItsAttachmentsThroughUnchanged()
    {
        var drafting = Over(APost.With(
            id: "110",
            account: "jeff@mastodon.social",
            media: [APost.APicture("m1", "A cartoon sheep"), APost.Attached(MediaKind.Video, "m2", description: null)]));

        drafting.Press(ShellKey.E);
        drafting.Composing.Rewrite("Hello again");

        await drafting.Drafting.Send();
        drafting.Fakes.Host.Drain();

        var changed = Assert.Single(drafting.Fakes.Author.Edits);

        Assert.Equal("110", changed.PostId);
        Assert.Equal("Hello again", changed.Edit.Text);
        Assert.Empty(drafting.Fakes.Author.Attaching);
        Assert.Empty(drafting.Fakes.Author.Published);
    }

    /// <summary>Nothing written is nothing to send, and saying so beats an instance refusing an empty post.</summary>
    [Fact]
    public async Task SendingRefusesAnEmptyPost()
    {
        var drafting = Over(Somebody);

        drafting.Press(ShellKey.C);

        await drafting.Drafting.Send();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.IsType<ComposeScreen>(drafting.Stack.Top);
        Assert.Equal("There is nothing written to send.", drafting.Notice);
        Assert.True(drafting.NoticeIsError);
    }

    /// <summary>A send with no compose in front sends nothing and says nothing.</summary>
    [Fact]
    public async Task SendingWithNoComposeInFrontDoesNothing()
    {
        var drafting = Over(Somebody);

        await drafting.Drafting.Send();

        Assert.Empty(drafting.Fakes.Author.Published);
        Assert.Null(drafting.Notice);
    }

    /// <summary>Drafting over Home holding <paramref name="post" />, picked.</summary>
    private static ADrafting Over(Post post) => new ADrafting { Bottom = ADrafting.Home(post) }.Build();
}
