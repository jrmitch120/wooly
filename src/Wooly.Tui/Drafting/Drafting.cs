using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Drafting;

/// <summary>
///     What the session does for the **draft**s it has open, beyond holding what is written (ADR-0029): opening a compose
///     screen to post, reply or edit, with the defaults it starts on and the limits it is measured against. One for the
///     session, beside the shell, rather than one per draft: what it remembers outlives any one draft.
/// </summary>
/// <remarks>
///     It reaches the shell through two things and no more: the stack (<see cref="IStack" />), and the
///     <see cref="Reach" /> a screen gets for a verb of its own — read afresh on every call, so a switch of profile needs
///     no rewiring. A draft verb comes in through <see cref="Do" />, and the shell tells it when a screen leaves
///     (<see cref="Left" />) and when one is uncovered (<see cref="Uncovered" />).
///     <para>
///         Everything here runs on the drawing thread: what an instance answers is brought back onto it through the
///         shell host, as it always was.
///     </para>
/// </remarks>
public sealed class Drafting
{
    private readonly IStack _stack;
    private readonly Func<Reach?> _reach;

    /// <summary>
    ///     What the config file says a post goes out at — <c>default_visibility</c> and <c>default_language</c>, either
    ///     unknown where it says nothing — which a compose screen's To and Lang start on ahead of the account's own
    ///     (ADR-0024, #338, #340).
    /// </summary>
    private readonly PostDefaults _preferred;

    /// <summary>How long each instance lets a post be, asked as posts are first written there (#319).</summary>
    private readonly LimitsByInstance _limits;

    /// <summary>
    ///     What each account acted as posts at by default, asked as the session starts acting as it — the fallback for
    ///     what compose starts on after the config's own preferences (ADR-0024, #339).
    /// </summary>
    private readonly DefaultsByProfile _defaults;

    /// <param name="stack">The stack of screens, which a compose is pushed onto.</param>
    /// <param name="reach">
    ///     What a screen can reach of the shell, as it is now: read on every call, so that it is always whoever is
    ///     being acted as — or nobody, while the shell has nobody to act as.
    /// </param>
    /// <param name="ports">What an instance's limits and an account's defaults are asked through.</param>
    /// <param name="host">The drawing thread, which their answers are handed back on.</param>
    /// <param name="preferred">What the config file says a post goes out at.</param>
    public Drafting(IStack stack, Func<Reach?> reach, ShellPorts ports, IShellHost host, PostDefaults preferred)
    {
        _stack = stack;
        _reach = reach;
        _preferred = preferred;
        _limits = new LimitsByInstance(ports.Limits, host);
        _limits.Heard += Measured;
        _defaults = new DefaultsByProfile(ports.Defaults, host);
    }

    /// <summary>
    ///     The limits on the instance acted as, as far as they are known — Mastodon's own until it has said — asking it
    ///     where nobody has yet. What the edit public profile form's bio is counted against.
    /// </summary>
    public PostLimits Limits => _limits.For(Reach.Profile);

    private Reach Reach => _reach() ?? throw new InvalidOperationException("Nobody is being acted as.");

    /// <summary>
    ///     Carries out <paramref name="verb" /> where it is a draft verb, which is the whole of how the shell's verb route
    ///     knows one: there is no list of them anywhere else.
    /// </summary>
    /// <returns>
    ///     <see cref="Drafted.NotMine" /> for any verb that is not a draft's, which the shell carries on with;
    ///     otherwise whether it was used or declined.
    /// </returns>
    public Drafted Do(Verb verb) => verb switch
    {
        Verb.Compose => Ran(() => Compose(ComposeFor.Post)),
        Verb.Reply => Ran(() => Compose(ComposeFor.Reply)),
        Verb.Edit => Ran(() => Compose(ComposeFor.Edit)),
        _ => Drafted.NotMine,
    };

    /// <summary>
    ///     The session has started acting as <paramref name="profile" />, at launch or at a switch: its account's
    ///     defaults are asked now, where nobody has yet, so that opening a compose asks nothing (ADR-0024).
    /// </summary>
    public void ActingAs(ActiveProfile profile) => _defaults.Ask(profile);

    /// <summary>
    ///     <paramref name="screen" /> is about to come off the stack, by whichever way out: called for each screen
    ///     before it is off, so that what it is about is still on the stack beneath it.
    /// </summary>
    public void Left(Screen screen) => Leaving?.Invoke(screen);

    /// <summary>
    ///     The stack has settled with a screen uncovered, its notice cleared — by <c>esc</c>, or by walking back down
    ///     it.
    /// </summary>
    public void Uncovered() => Uncovering?.Invoke();

    /// <summary>
    ///     Raised as <see cref="Left" /> is told, so that whatever takes screens off a stack can be held to telling it
    ///     on every way off (#427). Nothing in a session listens.
    /// </summary>
    internal event Action<Screen>? Leaving;

    /// <summary>Raised as <see cref="Uncovered" /> is told, for the reason <see cref="Leaving" /> is.</summary>
    internal event Action? Uncovering;

    /// <summary>An arm of <see cref="Do" /> that answers at once, as a used key.</summary>
    private static Drafted Ran(Action verb)
    {
        verb();

        return Drafted.Used;
    }

    /// <summary>
    ///     Opens a compose screen for <paramref name="purpose" /> on what the screen on top has picked out: a fresh post,
    ///     or one to a mention picked out; a reply to the post picked; or an edit of it, where it is the profile's own.
    /// </summary>
    private void Compose(ComposeFor purpose)
    {
        var screen = _stack.Top;
        var about = screen.Picked?.Boosted ?? screen.Picked;

        // A mention picked out is an account the reader walked to, so c writes to them — a fresh post rather than a
        // reply, since what they picked is somebody named in the post rather than the post itself (#85).
        if (purpose == ComposeFor.Post && screen.MentionedAs is { } handle)
        {
            Composing(new ComposeScreen(
                purpose,
                addressing: $"@{handle}",
                from: ComposeFrom.Of(Reach.Profile),
                starting: StartingDefaults(purpose, about: null)));

            return;
        }

        switch (purpose)
        {
            case ComposeFor.Reply or ComposeFor.Edit when about is null:
                return;
            case ComposeFor.Edit when !IsMine(about!):
                Reach.Say("Only your own posts can be edited.", isError: true);

                return;
        }

        Composing(new ComposeScreen(
            purpose,
            purpose == ComposeFor.Post ? null : about,
            purpose == ComposeFor.Reply ? Addressed(about!) : null,
            aboutIsMine: purpose == ComposeFor.Reply && IsMine(about!),
            from: ComposeFrom.Of(Reach.Profile),
            starting: StartingDefaults(purpose, about)));
    }

    /// <summary>
    ///     What a compose screen's To and Lang start on, which is what would go out if nobody touched them (ADR-0024): the
    ///     config's <c>default_visibility</c> and <c>default_language</c>, else the account's own as its instance said
    ///     them (#339), else nothing known. On a reply the visibility is the narrower of that and the post being
    ///     answered, which is where a reply with no visibility of its own goes out anyway (#338); the language is not,
    ///     since a reply is in its author's language rather than the answered post's (#340). An edit opens on the post's
    ///     own, which is the screen's to say.
    /// </summary>
    private PostDefaults StartingDefaults(ComposeFor purpose, Post? about)
    {
        var preferred = _preferred.Or(_defaults.For(Reach.Profile));

        return purpose == ComposeFor.Reply && about is { } answered
            ? preferred with
            {
                Visibility = PostAudience.Narrower(preferred.Visibility ?? answered.Visibility, answered.Visibility),
            }
            : preferred;
    }

    /// <summary>
    ///     Pushes <paramref name="compose" />, measured against its instance's own limit as far as that is known (#319).
    /// </summary>
    private void Composing(ComposeScreen compose)
    {
        compose.Limits = _limits.For(Reach.Profile);

        _stack.Push(compose);
    }

    /// <summary>
    ///     An instance said how long it lets a post be, which a compose on that instance still in front is measured
    ///     against from now on — and how long it lets a bio be, which the edit public profile form's count is.
    /// </summary>
    private void Measured(string instance, PostLimits limits)
    {
        if (_reach() is not { } reach
            || !string.Equals(reach.Profile.Instance, instance, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        switch (_stack.Top)
        {
            case ComposeScreen compose:
                compose.Limits = limits;
                reach.Changed();
                break;
            case PublicProfileScreen form:
                form.Limits = limits;
                reach.Changed();
                break;
        }
    }

    /// <summary>
    ///     What a reply has to be written to, or <see langword="null" /> where there is nobody left to name. Mastodon
    ///     routes by the handles it parses out of a post's <em>text</em>: a direct post reaches the accounts its text
    ///     mentions and nobody else (ADR-0013), and on every other visibility <c>in_reply_to_id</c> puts the reply in
    ///     the thread and notifies nobody at all. Either way a reply that named nobody would not reach the account it
    ///     answers, which is the one thing a reply is for (#130) — so every reply is addressed, and <c>dm send</c>
    ///     writes its mention for the same reason.
    /// </summary>
    /// <remarks>
    ///     The mention is written into the editor rather than added on the way out, so a reader who does not want to
    ///     ping somebody can delete the name before sending — and so this client never sends words the reader could
    ///     not see.
    ///     <para>
    ///         Who it goes to is the conversation where there is one, rather than whoever spoke last: a thread with
    ///         three accounts in it answered to only one of them is a reply that dropped the rest of the conversation.
    ///         Elsewhere it is the account being answered followed by everyone their post named, which is that same
    ///         conversation as the post itself carries it — a direct message included, since the accounts a direct post
    ///         names are the accounts its instance delivered it to (#132). The conversation still wins where there is
    ///         one on screen, being who is left in it rather than who one message in it happened to name.
    ///     </para>
    /// </remarks>
    private string? Addressed(Post about)
    {
        // An instance says who a conversation is with rather than who is having it, so the profile's own account is
        // already not among them. Everywhere else it is the post itself, whatever its visibility: the account being
        // answered and everyone their post named, off the post already in hand.
        IReadOnlyList<string> with = (about.Visibility, _stack.Top) switch
        {
            (PostVisibility.Direct, ConversationScreen conversation) => conversation.Conversation.With,
            _ => [about.Account, .. about.Mentions],
        };

        // An address this client cannot make sense of is left out rather than thrown over the reply, since a handle
        // an instance sent is not something the reader can do anything about. What is left is in the editor in front
        // of them, so a mention that is missing is missing where they can see it and type it themselves. The reader's
        // own account goes the same way: nobody is notified of their own reply, and a post answering a thread they are
        // in names them where the instance listed them.
        var accounts = with
            .Where(account => AccountAddress.IsWellFormed(account) && !Reach.IsMe(account))
            .Select(AccountAddress.Parse)
            .DistinctBy(account => account.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return accounts.Count == 0 ? null : DirectMessage.To(accounts, string.Empty);
    }

    /// <summary>
    ///     Whether a post is the profile's own, which is what settles whether it can be edited.
    /// </summary>
    /// <remarks>
    ///     Asked of the profile rather than read off <see cref="Post.IsMine" />, which is what the status row reads: both
    ///     are the one comparison <see cref="ActiveProfile.SignsInAs" /> makes, so the row and the refusal cannot come
    ///     to disagree (#220).
    /// </remarks>
    private bool IsMine(Post post) => Reach.IsMe(post.Account);
}
