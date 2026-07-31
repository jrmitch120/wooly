using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     The TUI's application shell: the rail, the stack of screens you drill into and walk back out of, and the one
///     thing in the TUI that reaches an instance. Everything here is decided without a terminal — which destination is
///     selected, what a run of rail steps fetches, whether an answer that arrived late is drawn, whether a delete has
///     been agreed to — so all of it is testable at the same port seam the CLI's commands are (ADR-0005, ADR-0014).
/// </summary>
/// <remarks>
///     Views observe this and draw it. Nothing here holds a Terminal.Gui type, and the two things it needs a terminal
///     for — waiting, and getting back onto the drawing thread — come in through <see cref="IShellHost" />.
/// </remarks>
public sealed class Shell
{
    /// <summary>
    ///     How many posts a screen asks for. A timeline's page, which is the most an instance serves in one call — so
    ///     arriving at a destination is one request rather than several, which matters most for the mechanism that can
    ///     spend the rate-limit budget by accident.
    /// </summary>
    private const int PostsWanted = 40;

    /// <summary>How many notifications, conversations or requests a count asks for before it stops counting.</summary>
    private const int CountedAtMost = 40;

    /// <summary>What a call that answers with nothing answers with, so that one retry loop serves both kinds.</summary>
    private static readonly object Done = new();

    private readonly DestinationCache _cache;
    private readonly TimeProvider _clock;
    private readonly IShellHost _host;
    private readonly ActiveProfile _profile;
    private readonly ShellPorts _ports;
    private readonly List<Screen> _stack = [];
    private readonly ShellTiming _timing;

    /// <summary>
    ///     What the last destination fetch was, so that an answer arriving after the reader has moved on can be told
    ///     apart and dropped. A reader two destinations further along must not have a stale timeline appear underneath
    ///     them (ADR-0014).
    /// </summary>
    private int _asked;

    private Func<Task>? _confirming;

    public Shell(
        ActiveProfile profile,
        ShellPorts ports,
        IShellHost host,
        TimeProvider clock,
        ShellTiming timing,
        string? hashtag = null)
    {
        _profile = profile;
        _ports = ports;
        _host = host;
        _clock = clock;
        _timing = timing;
        _cache = new DestinationCache(clock, timing.CacheFor);

        Rail = new Rail(Destinations(profile, hashtag), host, timing.Settle);
        Rail.Selected += destination => _ = Go(destination);
        Rail.Changed += () => Changed?.Invoke();

        _stack.Add(new FeedScreen(Rail.Showing, []));
    }

    /// <summary>Raised whenever anything on screen has changed. Always on the drawing thread.</summary>
    public event Action? Changed;

    /// <summary>The rail: the nine destinations, the cursor, and the selection.</summary>
    public Rail Rail { get; }

    /// <summary>The screen on top of the stack, which is what the content region is showing.</summary>
    public Screen Screen => _stack[^1];

    /// <summary>How deep the drill is, where one is a destination with nothing opened from it.</summary>
    public int Depth => _stack.Count;

    /// <summary>Where you are, as the trail along the top: <c>home › post by @ben › @ben@hachyderm.io</c>.</summary>
    public string Breadcrumb => string.Join(" › ", _stack.Select(screen => screen.Crumb));

    /// <summary>Whether a fetch is in flight, which the breadcrumb says once and the rail never does.</summary>
    public bool Fetching { get; private set; }

    /// <summary>
    ///     Something the shell has to say out loud that is not a screen: a refusal, or the countdown on a rate limit
    ///     being waited out.
    /// </summary>
    public string? Notice { get; private set; }

    /// <summary>Whether that notice is a failure rather than a remark, which settles the role it is drawn in.</summary>
    public bool NoticeIsError { get; private set; }

    /// <summary>What the shell is waiting to be told again before it does, or <see langword="null" /> if nothing.</summary>
    public Confirmation? Asking { get; private set; }

    /// <summary>What the instance last said is left of the profile's budget, for the rail's foot (story 54).</summary>
    public RateLimitQuota? Quota => _ports.RateLimit.Latest;

    /// <summary>The keys the current screen answers to, for the status row.</summary>
    public IReadOnlyList<KeyHint> Keys => Screen.Keys;

    /// <summary>Opens the shell onto its first destination, and reads the counts the rail carries.</summary>
    public async Task Open()
    {
        await Go(Rail.Showing);
        await Counts();
    }

    /// <summary>
    ///     A rail keypress. The cursor moves at once; the selection — and the fetch — follow when the pressing stops.
    /// </summary>
    public void Step(int by) => Rail.Step(by);

    /// <summary>Moves what is picked out on the current screen.</summary>
    public void Move(int by)
    {
        Screen.Move(by);
        Changed?.Invoke();
    }

    /// <summary>Shows what the picked post's content warning is hiding.</summary>
    public void Reveal()
    {
        if (Screen.Reveal())
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Opens the picked post, with what has been said in answer to it.</summary>
    public async Task Enter()
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        var about = picked.Boosted ?? picked;

        // Which destination this drill started from. A reader who tabbed away while the replies were in flight is
        // somewhere else now, and a post screen appearing over it would be the same stale-answer problem the rail's
        // own discard rule solves.
        var from = _asked;
        var replies = await Ask(cancellation => _ports.Engagement.Replies(_profile, about.Id, cancellation));

        if (replies is not null)
        {
            Apply(() =>
            {
                if (!Overtaken(from))
                {
                    Push(new PostScreen(picked, replies));
                }
            });
        }
    }

    /// <summary>Opens the account that wrote the picked post.</summary>
    public async Task OpenAuthor()
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        await OpenAccount(AccountAddress.Parse((picked.Boosted ?? picked).Account), _asked);
    }

    /// <summary>Walks back up one level of the stack. Never quits, and never leaves the shell with nothing on it.</summary>
    public void Back()
    {
        if (Asking is not null)
        {
            // Escaping out of a confirmation is answering it, and the answer is no.
            Asking = null;
            _confirming = null;

            Changed?.Invoke();

            return;
        }

        if (_stack.Count > 1)
        {
            _stack.RemoveAt(_stack.Count - 1);
        }

        Notice = null;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Goes to search, which is a frame key rather than a screen's (<c>docs/tui-shell.md</c>): it means the same
    ///     thing everywhere, so it is bound here even though what it opens onto is #29's.
    /// </summary>
    public void Search() => Rail.GoTo(DestinationKind.Search);

    /// <summary>Shows the current screen's keymap, which is itself a place in the stack.</summary>
    public void Help()
    {
        if (Screen is not HelpScreen)
        {
            Push(new HelpScreen(Screen));
        }
    }

    /// <summary>
    ///     Puts <paramref name="mark" /> on the picked post, or takes it off — whichever the post does not already
    ///     have, which is why a post carries the reader's own marks.
    /// </summary>
    public async Task Mark(PostMark mark)
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        var about = picked.Boosted ?? picked;

        if (mark == PostMark.Pin && !IsMine(about))
        {
            Say("Only your own posts can be pinned.", isError: true);

            return;
        }

        var marked = await Ask(token =>
            _ports.Engagement.Mark(_profile, about.Id, mark, !about.Marks.Has(mark), token));

        if (marked is not null)
        {
            Apply(() => Replace(marked));
        }
    }

    /// <summary>Opens an editor answering the picked post.</summary>
    public void Reply() => Compose(ComposeFor.Reply);

    /// <summary>Opens an editor for a new post.</summary>
    public void Compose() => Compose(ComposeFor.Post);

    /// <summary>Opens an editor on one of the profile's own posts.</summary>
    public void Edit() => Compose(ComposeFor.Edit);

    /// <summary>
    ///     Asks before taking a post down. The one thing here whose effect running something else does not undo, so
    ///     nothing is deleted until it has been said twice (story 43).
    /// </summary>
    public void AskToDelete()
    {
        if (Screen.Picked is not { } picked)
        {
            return;
        }

        var about = picked.Boosted ?? picked;

        if (!IsMine(about))
        {
            Say("Only your own posts can be deleted.", isError: true);

            return;
        }

        Asking = new Confirmation($"Delete post {about.Id}? This cannot be undone.");
        _confirming = () => Delete(about.Id);

        Changed?.Invoke();
    }

    /// <summary>Answers whatever the shell was waiting to be told again.</summary>
    public async Task Answer(bool agreed)
    {
        var confirmed = _confirming;

        Asking = null;
        _confirming = null;

        Changed?.Invoke();

        if (agreed && confirmed is not null)
        {
            await confirmed();
        }
    }

    /// <summary>Publishes, replies with, or saves whatever the compose screen is holding.</summary>
    public async Task Send()
    {
        if (Screen is not ComposeScreen compose)
        {
            return;
        }

        if (compose.IsEmpty)
        {
            Say("There is nothing written to send.", isError: true);

            return;
        }

        var written = await (compose.Purpose == ComposeFor.Edit
            ? Ask(token => _ports.Author.Edit(_profile, compose.About!.Id, new PostEdit { Text = compose.Text }, token))
            : Ask(token => _ports.Author.Publish(
                _profile,
                new PostDraft
                {
                    Text = compose.Text,

                    // Silence rather than a visibility of the shell's choosing. A reply is answered as narrowly as
                    // the post it answers, and a post says nothing so that the account's own default on the instance
                    // decides — this shell has no visibility picker to have been told anything by.
                    InReplyTo = compose.Purpose == ComposeFor.Reply ? compose.About?.Id : null,
                },
                token)));

        if (written is null)
        {
            return;
        }

        Apply(() =>
        {
            // This client is what changed the timeline, so its age says nothing useful about it any more.
            _cache.Forget(Rail.Showing.Kind);

            _stack.RemoveAt(_stack.Count - 1);

            if (compose.Purpose == ComposeFor.Edit)
            {
                Replace(written);
            }

            Say(compose.Purpose == ComposeFor.Edit ? "Saved." : "Sent.", isError: false);
        });
    }

    /// <summary>The nine, in the order the rail draws them.</summary>
    private static IReadOnlyList<Destination> Destinations(ActiveProfile profile, string? hashtag) =>
    [
        new(DestinationKind.Home, "Home", Timeline.Home),
        new(DestinationKind.Local, "Local", Timeline.Local),
        new(DestinationKind.Federated, "Federated", Timeline.Federated),
        new(
            DestinationKind.Hashtag,
            hashtag is null ? "Hashtag" : $"#{hashtag}",
            hashtag is null ? null : Timeline.Tag(hashtag)),
        new(DestinationKind.Notifications, "Notifications"),
        new(DestinationKind.Messages, "Direct messages"),
        new(DestinationKind.Requests, "Follow requests"),
        new(DestinationKind.Search, "Search"),
        new(DestinationKind.Profile, profile.Account is { } account ? $"@{account.Split('@')[0]}" : "Profile"),
    ];

    /// <summary>Arriving at a destination, which is what moving the rail's selection means.</summary>
    private async Task Go(Destination destination)
    {
        // Taken before anything else, and by every arrival rather than only the ones that fetch. A destination that
        // asks the instance for nothing still overtakes what the last one asked for — otherwise a timeline still in
        // flight lands on top of the notice screen the reader has since walked to.
        var token = ++_asked;

        // Walking to a destination is arriving somewhere, so whatever was drilled into from the last one is left
        // behind: the stack is where you went from here, and this is a different here.
        Apply(() => Reset(OnArrival(destination)));

        if (destination.Kind == DestinationKind.Profile)
        {
            if (_profile.Account is { } account)
            {
                await OpenAccount(AccountAddress.Parse(account), token, replacing: true);
            }

            return;
        }

        if (destination.Timeline is not { } timeline)
        {
            return;
        }

        if (_cache.Fresh(destination.Kind) is { } held)
        {
            Apply(() => Reset(new FeedScreen(destination, held, Emptiness(held, destination))));

            return;
        }

        var fetch = await Ask(cancellation => _ports.Timelines.Read(_profile, timeline, PostsWanted, cancellation));

        if (fetch is null)
        {
            return;
        }

        Apply(() =>
        {
            // Overtaken. The reader has asked for somewhere else since, and drawing this now would put a timeline
            // they have left underneath the destination they are on.
            if (Overtaken(token))
            {
                return;
            }

            _cache.Keep(destination.Kind, fetch.Posts);
            Reset(new FeedScreen(destination, fetch.Posts, Emptiness(fetch.Posts, destination, fetch.StoppedBy)));
        });
    }

    /// <summary>Opens an account screen: who they are, their standing, and their posts.</summary>
    /// <param name="token">
    ///     Which arrival this belongs to. Two calls deep, so it is checked once at the end rather than after each:
    ///     what matters is whether the reader is still where they were when they asked, not how far the answer got.
    /// </param>
    private async Task OpenAccount(AccountAddress address, int token, bool replacing = false)
    {
        var account = await Ask(token => _ports.Accounts.Show(_profile, address, token));

        if (account is null)
        {
            return;
        }

        var posts = await Ask(token =>
            _ports.Timelines.Read(_profile, Timeline.By(address), PostsWanted, token));

        if (posts is null)
        {
            return;
        }

        Apply(() =>
        {
            if (Overtaken(token))
            {
                return;
            }

            var screen = new AccountScreen(account, posts.Posts);

            if (replacing)
            {
                Reset(screen);
            }
            else
            {
                Push(screen);
            }
        });
    }

    private async Task Delete(string postId)
    {
        if (!await Did(token => _ports.Author.Delete(_profile, postId, token)))
        {
            return;
        }

        Apply(() =>
        {
            _cache.Forget(Rail.Showing.Kind);

            // Walked out of first, because a post screen showing a post that is no longer there is a screen about
            // nothing.
            if (Screen is PostScreen post && (post.Post.Boosted ?? post.Post).Id == postId && _stack.Count > 1)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            foreach (var screen in _stack)
            {
                screen.Remove(postId);
            }

            Say("Deleted.", isError: false);
        });
    }

    /// <summary>Reads the counts the rail carries, none of which is worth failing the shell over.</summary>
    private async Task Counts()
    {
        await Count(
            DestinationKind.Notifications,
            async token => (await _ports.Notifications.Read(_profile, CountedAtMost, token)).Notifications.Count);

        await Count(
            DestinationKind.Messages,
            async token => (await _ports.Messages.List(_profile, CountedAtMost, token))
                .Conversations.Count(conversation => conversation.Unread));

        await Count(
            DestinationKind.Requests,
            async token => (await _ports.Accounts.PendingRequests(_profile, CountedAtMost, token)).Accounts.Count);
    }

    private async Task Count(DestinationKind kind, Func<CancellationToken, Task<int>> read)
    {
        try
        {
            var unread = await read(CancellationToken.None);

            Apply(() => Rail.Update(Rail.Destinations.First(destination => destination.Kind == kind) with
            {
                Unread = unread,
            }));
        }
        catch (WoolyException)
        {
            // A count that could not be read is drawn as no count. It is the least of what is on screen, and a shell
            // that refused to open because a badge was unavailable would be trading the whole thing for a number.
        }
    }

    /// <summary>
    ///     Makes a call, waiting out a rate limit with a visible countdown rather than failing on it (story 53) — the
    ///     opposite of the CLI's fail-fast, which is right there because a script cannot be told to wait and wrong
    ///     here because a person can see that it is (ADR-0006).
    /// </summary>
    /// <returns>What the call answered, or <see langword="null" /> where it failed for a reason waiting cannot mend.</returns>
    private async Task<T?> Ask<T>(Func<CancellationToken, Task<T>> call)
        where T : class
    {
        Apply(() =>
        {
            Fetching = true;
            Changed?.Invoke();
        });

        try
        {
            while (true)
            {
                try
                {
                    return await call(CancellationToken.None);
                }
                catch (RateLimitedException limit)
                {
                    await WaitOut(limit);
                }
            }
        }
        catch (WoolyException failure)
        {
            Apply(() => Say(failure.Message, isError: true));

            return null;
        }
        finally
        {
            Apply(() =>
            {
                Fetching = false;
                Changed?.Invoke();
            });
        }
    }

    /// <summary>
    ///     The same as <see cref="Ask{T}" /> for a call that answers with nothing, which is only ever a delete.
    /// </summary>
    /// <returns>Whether it went through.</returns>
    private async Task<bool> Did(Func<CancellationToken, Task> call) =>
        await Ask<object>(async token =>
        {
            await call(token);

            return Done;
        }) is not null;

    /// <summary>Counts a rate limit down where the reader can see it, then lets the call be made again.</summary>
    private async Task WaitOut(RateLimitedException limit)
    {
        // An instance that named no reset is waited on for as long as it usually takes one to roll a window over,
        // rather than given up on: the reader asked for something, and "try again yourself" is the CLI's answer.
        var until = limit.ResetsAt ?? _clock.GetUtcNow() + TimeSpan.FromMinutes(5);

        while (_clock.GetUtcNow() < until)
        {
            var left = (int)Math.Ceiling((until - _clock.GetUtcNow()).TotalSeconds);

            Apply(() => Say($"Rate limited by {limit.Instance}. Trying again in {left}s.", isError: false));

            await Wait(_timing.CountdownStep);
        }

        Apply(() => Say(null, isError: false));
    }

    private Task Wait(TimeSpan howLong)
    {
        var waited = new TaskCompletionSource();

        _host.After(howLong, () => waited.TrySetResult());

        return waited.Task;
    }

    /// <summary>
    ///     What a destination puts on screen the moment it is arrived at: an empty feed for the four timelines, whose
    ///     posts land a moment later, and a standing notice for the ones whose screens later tickets bring.
    /// </summary>
    private Screen OnArrival(Destination destination) => destination.Kind switch
    {
        DestinationKind.Notifications => new NoticeScreen(
            "notifications",
            "Notifications land here in a later release.",
            "For now: wooly notification list"),
        DestinationKind.Messages => new NoticeScreen(
            "direct messages",
            "Conversations land here in a later release.",
            "For now: wooly dm list"),
        DestinationKind.Requests => new NoticeScreen(
            "follow requests",
            "Follow requests land here in a later release.",
            "For now: wooly account request list"),
        DestinationKind.Search => new NoticeScreen(
            "search",
            "Search lands here in a later release.",
            "For now: wooly search"),
        DestinationKind.Hashtag when destination.Timeline is null => new NoticeScreen(
            "hashtag",
            "No hashtag is set for the rail.",
            """Put hashtag = "cats" under [preferences] in your config file to keep one here."""),
        _ => new FeedScreen(destination, []),
    };

    /// <summary>What to say about a timeline that came back with little or nothing on it.</summary>
    private static string? Emptiness(
        IReadOnlyList<Post> posts,
        Destination destination,
        RateLimitedException? stoppedBy = null)
    {
        if (stoppedBy is not null)
        {
            return $"Rate limited part way through — this is what arrived of {destination.Timeline?.Description}.";
        }

        return posts.Count == 0 ? $"Nothing on {destination.Timeline?.Description} yet." : null;
    }

    private void Compose(ComposeFor purpose)
    {
        var about = Screen.Picked?.Boosted ?? Screen.Picked;

        switch (purpose)
        {
            case ComposeFor.Reply or ComposeFor.Edit when about is null:
                return;
            case ComposeFor.Edit when !IsMine(about!):
                Say("Only your own posts can be edited.", isError: true);

                return;
        }

        Push(new ComposeScreen(purpose, purpose == ComposeFor.Post ? null : about));
    }

    /// <summary>
    ///     Whether a post is the profile's own, which is what settles whether pinning, editing and deleting are
    ///     offered. Compared on the address, because that is the one name for an account that means the same thing on
    ///     two instances.
    /// </summary>
    private bool IsMine(Post post) =>
        _profile.Account is { } account && string.Equals(post.Account, account, StringComparison.OrdinalIgnoreCase);

    private void Push(Screen screen)
    {
        _stack.Add(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Puts the stack back to one screen, which is what arriving at a destination does.</summary>
    private void Reset(Screen screen)
    {
        _stack.Clear();
        _stack.Add(screen);
        Notice = null;

        Changed?.Invoke();
    }

    /// <summary>Puts a post that has just changed in place of the copy every screen in the stack is holding.</summary>
    private void Replace(Post post)
    {
        foreach (var screen in _stack)
        {
            screen.Replace(post);
        }

        _cache.Forget(Rail.Showing.Kind);

        Changed?.Invoke();
    }

    private void Say(string? notice, bool isError)
    {
        Notice = notice;
        NoticeIsError = isError;

        Changed?.Invoke();
    }

    /// <summary>
    ///     Whether the reader has arrived somewhere else since <paramref name="token" /> was taken, which makes
    ///     whatever it belongs to an answer to a question nobody is asking any more.
    /// </summary>
    private bool Overtaken(int token) => token != _asked;

    private void Apply(Action work) => _host.OnUiThread(work);
}
