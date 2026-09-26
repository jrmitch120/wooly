using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What a screen can reach of the shell while it answers one of its own verbs (<see cref="Screen.Answer" />): the
///     ports and the profile to ask with, the one way of asking, and the handful of things a screen may do to what is
///     around it — open something, stand something fresh in its own place, say something, ask before going ahead.
/// </summary>
/// <remarks>
///     Narrow on purpose, and concrete on purpose. Narrow, because the stack, the rail and the caches are the shell's:
///     a screen that could reach them would be a second shell, and the reason a key's action lives with the screen that
///     announces it is that it no longer has to live in the one file every screen's keys ended up in (#232). Concrete,
///     because the shell is the only thing that ever builds one, and an interface with one implementer would be a
///     second place to add every member to.
///     <para>
///         A verb that needs more than is here is a sign it is not screen-local, and the thing to do is say so rather
///         than widen this. Two members were added past the first list on exactly that footing:
///         <see cref="OpenFollows" />, the fourth thing a screen opens, and <see cref="Stands" />, which a tie needs
///         for the reason <see cref="Screen.Stands" /> gives.
///     </para>
/// </remarks>
public sealed class Reach
{
    private readonly Action _changed;
    private readonly Action<Confirmation> _confirm;
    private readonly Action<DestinationKind, int> _count;
    private readonly DestinationCache _cache;
    private readonly Enquiry _enquiry;
    private readonly Func<AccountAddress, Task> _openAccount;
    private readonly Func<Account, FollowSide, bool, Task> _openFollows;
    private readonly Func<Post, Task> _openPost;
    private readonly Func<string, Task> _openTag;
    private readonly Action<Screen> _push;
    private readonly Says _say;
    private readonly Action<Account> _stands;
    private readonly Action<Screen, Screen> _swap;

    /// <summary>Built by the shell alone, with the shell's own hands for everything that touches its stack.</summary>
    internal Reach(
        ActiveProfile profile,
        ShellPorts ports,
        Enquiry enquiry,
        DestinationCache cache,
        Action<Screen> push,
        Action<Screen, Screen> swap,
        Func<AccountAddress, Task> openAccount,
        Func<string, Task> openTag,
        Func<Post, Task> openPost,
        Func<Account, FollowSide, bool, Task> openFollows,
        Says say,
        Action<Confirmation> confirm,
        Action changed,
        Action<DestinationKind, int> count,
        Action<Account> stands)
    {
        Profile = profile;
        Ports = ports;
        _enquiry = enquiry;
        _cache = cache;
        _push = push;
        _swap = swap;
        _openAccount = openAccount;
        _openTag = openTag;
        _openPost = openPost;
        _openFollows = openFollows;
        _say = say;
        _confirm = confirm;
        _changed = changed;
        _count = count;
        _stands = stands;
    }

    /// <summary>Who is asking.</summary>
    public ActiveProfile Profile { get; }

    /// <summary>What is asked through — the same ports the shell has, and the CLI's commands (ADR-0005).</summary>
    public ShellPorts Ports { get; }

    /// <summary>
    ///     Puts a question to the instance through the shell's <see cref="Enquiry" />, with its stale-answer rule
    ///     exactly as it is everywhere else.
    /// </summary>
    /// <inheritdoc cref="Enquiry.Put{T}" />
    public Task Put<T>(Func<Enquiry.Ask, Task<T>> question, Action<T>? eitherWay = null, Action<T>? ifStillHere = null) =>
        _enquiry.Put(question, eitherWay, ifStillHere);

    /// <inheritdoc cref="Enquiry.Put(Func{Enquiry.Ask, Task}, Action?, Action?)" />
    public Task Put(Func<Enquiry.Ask, Task> question, Action? eitherWay = null, Action? ifStillHere = null) =>
        _enquiry.Put(question, eitherWay, ifStillHere);

    /// <summary>Drills into <paramref name="screen" />, on top of whatever is showing.</summary>
    public void Push(Screen screen) => _push(screen);

    /// <summary>
    ///     Stands <paramref name="fresh" /> in place of <paramref name="showing" />, where <paramref name="showing" />
    ///     is still the screen on top — and nowhere, where the reader has walked off it since.
    /// </summary>
    public void Swap(Screen showing, Screen fresh) => _swap(showing, fresh);

    /// <summary>Opens an account screen: who they are, their standing, and their posts.</summary>
    public Task OpenAccount(AccountAddress address) => _openAccount(address);

    /// <summary>Opens a hashtag's timeline as a screen on the stack, leaving the rail's own hashtag alone.</summary>
    public Task OpenTag(string hashtag) => _openTag(hashtag);

    /// <summary>Opens <paramref name="post" />, with what has been said in answer to it.</summary>
    public Task OpenPost(Post post) => _openPost(post);

    /// <summary>
    ///     Opens <paramref name="side" /> of <paramref name="whose" />'s follows — on top of what is showing, or in
    ///     its place where <paramref name="replacing" /> says a side is being swapped (#180).
    /// </summary>
    public Task OpenFollows(Account whose, FollowSide side, bool replacing) => _openFollows(whose, side, replacing);

    /// <summary>Puts <paramref name="notice" /> on the status row, or takes whatever is there off it.</summary>
    public void Say(string? notice, bool isError) => _say(notice, isError);

    /// <summary>Asks before going ahead, and goes ahead with what the confirmation carries once it is agreed to.</summary>
    public void Confirm(Confirmation confirmation) => _confirm(confirmation);

    /// <summary>Says something on screen has changed, so that it is drawn again.</summary>
    public void Changed() => _changed();

    /// <summary>Lets go of what <paramref name="kind" /> last held, so that arriving there asks again.</summary>
    public void Forget(DestinationKind kind) => _cache.Forget(kind);

    /// <summary>Puts <paramref name="unread" /> on <paramref name="kind" />'s badge on the rail.</summary>
    public void Count(DestinationKind kind, int unread) => _count(kind, unread);

    /// <summary>
    ///     Puts an account whose tie has just changed in place of the copy every screen in the stack is holding, which
    ///     is how a row under the screen a tie was made on comes to agree with it (#181).
    /// </summary>
    public void Stands(Account account) => _stands(account);

    /// <summary>Whether <paramref name="address" /> is the profile's own account.</summary>
    public bool IsMe(string address) => Profile.SignsInAs(address);
}
