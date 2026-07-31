using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Conversations;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Direct messages driven the way a user drives them: whole commands through the real command app, over a real
///     config file and token store in a scratch directory, with the instance faked at <see cref="IDirectMessages" /> and
///     <see cref="IPostAuthor" /> — ADR-0005's primary seams, which is what a command test is meant to fake.
///     <para>
///         Sending is faked at <see cref="IPostAuthor" /> rather than at a port of its own because that is the claim
///         ADR-0013 makes: a direct message is a post that went out direct, so <c>dm send</c> composes one through the
///         same call <c>post create</c> does. Half of what is proved here is that it really is the same call.
///     </para>
/// </summary>
public class DirectMessageCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeDirectMessages _messages = FakeDirectMessages.Holding(AConversation.With());

    private readonly FakePostAuthor _posts = FakePostAuthor.Answering(
        APost.With(id: "111", visibility: PostVisibility.Direct));

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void List_ShowsTheConversationsTheProfileIsIn()
    {
        AddProfile();

        var run = Run(["dm", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("Hello world", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        var listing = Assert.Single(_messages.Listings);
        Assert.Equal("personal", listing.Profile);
        Assert.Equal(20, listing.Limit);
    }

    /// <summary>
    ///     The id is what <c>dm show</c> and <c>dm read</c> take, so a listing that did not show it would leave a user
    ///     with no way to name the conversation they just read about.
    /// </summary>
    [Fact]
    public void List_ShowsTheIdEachConversationIsNamedBy()
    {
        AddProfile();
        _messages = FakeDirectMessages.Holding(AConversation.With(id: "7"));

        var run = Run(["dm", "list"]);

        Assert.Contains("7", run.Output);
    }

    /// <summary>The unread indicator #27 asks for, and its absence on one that has been read.</summary>
    [Fact]
    public void List_SaysWhichConversationsAreUnread()
    {
        AddProfile();
        _messages = FakeDirectMessages.Holding(
            AConversation.With(id: "7", unread: true),
            AConversation.With(id: "8", unread: false));

        var run = Run(["dm", "list"]);

        // Said once, for the one it is true of. A word printed against both would be no indicator at all.
        Assert.Equal(2, run.Output.Split("unread").Length);
    }

    [Fact]
    public void List_SaysSoWhenThereAreNoConversations()
    {
        AddProfile();
        _messages = FakeDirectMessages.Holding();

        var run = Run(["dm", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No direct conversations", run.Output);
    }

    /// <summary>A conversation whose posts have all gone is still one the account is in, and still one it can act on.</summary>
    [Fact]
    public void List_ShowsAConversationWithNothingLeftInIt()
    {
        AddProfile();
        _messages = FakeDirectMessages.Holding(AConversation.Emptied(id: "7"));

        var run = Run(["dm", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("7", run.Output);
        Assert.Contains("Nothing left", run.Output);
    }

    [Fact]
    public void List_WritesTheConversationsAsJsonForAnotherProgram()
    {
        AddProfile();

        var run = Run(["dm", "list", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        using var document = JsonDocument.Parse(run.Output);
        var root = document.RootElement;

        Assert.True(root.GetProperty("complete").GetBoolean());

        var conversation = Assert.Single(root.GetProperty("conversations").EnumerateArray());
        Assert.Equal("7", conversation.GetProperty("id").GetString());
        Assert.True(conversation.GetProperty("unread").GetBoolean());
        Assert.Equal(
            ["alice@hachyderm.io"],
            conversation.GetProperty("with").EnumerateArray().Select(account => account.GetString()));

        // The last post is the same PostDocument every other command writes a post as.
        Assert.Equal("110", conversation.GetProperty("latest").GetProperty("id").GetString());
        Assert.Equal("direct", conversation.GetProperty("latest").GetProperty("visibility").GetString());
    }

    /// <summary>
    ///     ADR-0007's second decision at the front end: what arrived is written before the limit that stopped the rest
    ///     is reported, and the exit code is what a script branches on.
    /// </summary>
    [Fact]
    public void List_WritesWhatArrivedAndThenReportsTheRateLimitThatStoppedTheRest()
    {
        AddProfile();
        _messages = FakeDirectMessages.RateLimitedAfter(AConversation.With(id: "7"));

        var run = Run(["dm", "list"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("7", run.Output);
        Assert.Contains("error:", run.ErrorOutput);
    }

    [Fact]
    public void List_TurnsDownALimitOfNoConversations()
    {
        AddProfile();

        var run = Run(["dm", "list", "--limit", "0"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("conversation", run.ErrorOutput);
        Assert.Empty(_messages.Listings);
    }

    [Fact]
    public void List_ActsAsTheProfileNamedByTheOverride()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["dm", "list", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_messages.Listings).Profile);
    }

    /// <summary>The thread, oldest first, which is the only order a conversation reads in.</summary>
    [Fact]
    public void Show_ReadsOneConversationInFull()
    {
        AddProfile();
        _messages = FakeDirectMessages.Threading(AConversation.Thread(
            AConversation.With(id: "7"),
            AConversation.DirectPost("108", "Are you coming?"),
            AConversation.DirectPost("110", "On my way")));

        var run = Run(["dm", "show", "7"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("7", Assert.Single(_messages.Shown).ConversationId);
        Assert.True(run.Output.IndexOf("Are you coming?", StringComparison.Ordinal)
                    < run.Output.IndexOf("On my way", StringComparison.Ordinal));
    }

    [Fact]
    public void Show_WritesTheThreadAsJsonForAnotherProgram()
    {
        AddProfile();
        _messages = FakeDirectMessages.Threading(AConversation.Thread(
            AConversation.With(id: "7"),
            AConversation.DirectPost("108"),
            AConversation.DirectPost("110")));

        var run = Run(["dm", "show", "7", "--json"]);

        using var document = JsonDocument.Parse(run.Output);
        var root = document.RootElement;

        Assert.Equal("7", root.GetProperty("id").GetString());
        Assert.Equal(["108", "110"], root.GetProperty("posts").EnumerateArray().Select(post => post.GetProperty("id").GetString()));
    }

    /// <summary>
    ///     An id no conversation carries is a value on the command line that is wrong, not a client that could not do
    ///     its job — so it exits the way a bad argument does.
    /// </summary>
    [Fact]
    public void Show_ReportsAnIdNoConversationCarriesAsSomethingTheUserTyped()
    {
        AddProfile();
        _messages = FakeDirectMessages.Refusing(new UnknownConversationException("9", 200));

        var run = Run(["dm", "show", "9"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("9", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    [Fact]
    public void Read_ClearsTheUnreadMarkOnTheConversationItWasNamed()
    {
        AddProfile();

        var run = Run(["dm", "read", "7"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var marked = Assert.Single(_messages.MarkedRead);
        Assert.Equal("personal", marked.Profile);
        Assert.Equal("7", marked.ConversationId);
        Assert.Contains("7", run.Output);
        Assert.Contains("read", run.Output);
    }

    [Fact]
    public void Read_WritesTheConversationAsJsonForAnotherProgram()
    {
        AddProfile();

        var run = Run(["dm", "read", "7", "--json"]);

        using var document = JsonDocument.Parse(run.Output);

        Assert.Equal("7", document.RootElement.GetProperty("id").GetString());
        Assert.False(document.RootElement.GetProperty("unread").GetBoolean());
    }

    /// <summary>Reading a conversation is not marking it read — the two are separate acts on purpose.</summary>
    [Fact]
    public void Show_DoesNotMarkTheConversationRead()
    {
        AddProfile();

        Run(["dm", "show", "7"]);

        Assert.Empty(_messages.MarkedRead);
    }

    /// <summary>
    ///     #27's point, and ADR-0013's: sending is publishing a post, through the very call <c>post create</c> uses.
    ///     The user sets neither the visibility nor the mention.
    /// </summary>
    [Fact]
    public void Send_PublishesADirectPostNamingTheAccountItIsFor()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@hachyderm.io", "Are you coming?"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(run.ErrorOutput.Trim());

        var composed = Assert.Single(_posts.Published);
        Assert.Equal("personal", composed.Profile);
        Assert.Equal("@alice@hachyderm.io Are you coming?", composed.Draft.Text);
        Assert.Equal(PostVisibility.Direct, composed.Draft.Visibility);
        Assert.True(composed.Draft.VisibilityChosen);
        Assert.Null(composed.Draft.InReplyTo);
    }

    /// <summary>A bare username is somebody on the profile's own instance, and is mentioned as the user wrote it.</summary>
    [Fact]
    public void Send_WritesToABareUsernameOnTheProfilesOwnInstance()
    {
        AddProfile();

        var run = Run(["dm", "send", "bob", "Morning"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("@bob Morning", Assert.Single(_posts.Published).Draft.Text);
    }

    /// <summary>The recipient is what a sender wants confirmed; the visibility is the premise of the command.</summary>
    [Fact]
    public void Send_ReportsWhoTheMessageWentToAndTheIdItWasGiven()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@hachyderm.io", "Are you coming?"]);

        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("111", run.Output);
    }

    /// <summary>
    ///     Composing is inherited whole, so a direct message can carry everything any other post can. This is the part
    ///     that would rot first if <c>dm send</c> had a compose path of its own.
    /// </summary>
    [Fact]
    public void Send_ComposesEverythingAnyOtherPostCanCarry()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@hachyderm.io", "Look at this", "--cw", "spoilers"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var composed = Assert.Single(_posts.Published);
        Assert.Equal("spoilers", composed.Draft.ContentWarning);
        Assert.Equal(PostVisibility.Direct, composed.Draft.Visibility);
    }

    /// <summary>
    ///     An option offered where only one value is possible is one somebody will pass another value to, so
    ///     <c>dm send</c> does not offer <c>--visibility</c> at all — and strict parsing answers anyone who tries.
    /// </summary>
    [Fact]
    public void Send_OffersNoVisibilityToSetBecauseADirectMessageIsDirect()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@hachyderm.io", "Hello", "--visibility", "public"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_posts.Published);
    }

    /// <summary>
    ///     Sent direct whatever the profile prefers, because the preference is about posts an author is choosing an
    ///     audience for, and this one has none to choose.
    /// </summary>
    [Fact]
    public void Send_IgnoresTheProfilesPreferredVisibility()
    {
        AddProfile();
        PreferVisibility("public");

        var run = Run(["dm", "send", "alice@hachyderm.io", "Hello"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(PostVisibility.Direct, Assert.Single(_posts.Published).Draft.Visibility);
    }

    [Fact]
    public void Send_TurnsDownSomethingThatIsNotAnAccount()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@bad address", "Hello"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("not an account", run.ErrorOutput);
        Assert.Empty(_posts.Published);
    }

    [Fact]
    public void Send_WritesThePublishedMessageAsJsonForAnotherProgram()
    {
        AddProfile();

        var run = Run(["dm", "send", "alice@hachyderm.io", "Hello", "--json"]);

        using var document = JsonDocument.Parse(run.Output);

        Assert.Equal("111", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("direct", document.RootElement.GetProperty("visibility").GetString());
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    /// <summary>Writes the preference an author would have set by hand, as <c>PostCommandTests</c> does.</summary>
    private void PreferVisibility(string visibility)
    {
        var paths = new WoolyPaths(_directory.Path);

        File.AppendAllText(
            paths.ConfigFile,
            $"{Environment.NewLine}[preferences]{Environment.NewLine}default_visibility = \"{visibility}\"{Environment.NewLine}");
    }

    private CommandRun Run(string[] args)
    {
        var console = new TestConsole().Width(200);
        var errorConsole = new TestConsole().Width(200);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IDirectMessages>(_messages);
            services.AddSingleton<IPostAuthor>(_posts);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
