using Wooly.Core.Accounts;
using Wooly.Core.Profiles;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What a screen can reach of the shell while it answers one of its own verbs (<see cref="Screen.Answer" />): the
///     ports and the profile to ask with, the one way of asking, and the handful of things a screen may do to what is
///     around it — open a subject, stand another in its own place, say something, ask before going ahead.
/// </summary>
/// <remarks>
///     Narrow on purpose, and concrete on purpose. Narrow, because the stack, the rail and the caches are the shell's:
///     a screen that could reach them would be a second shell, and the reason a key's action lives with the screen that
///     announces it is that it no longer has to live in the one file every screen's keys ended up in (#232). Concrete,
///     because the shell is the only thing that ever builds one, and an interface with one implementer would be a
///     second place to add every member to.
///     <para>
///         A verb that needs more than is here is a sign it is not screen-local, and the thing to do is say so rather
///         than widen this. One member was added past the first list on exactly that footing:
///         <see cref="Stands" />, which a tie needs for the reason <see cref="Screen.Stands" /> gives.
///     </para>
///     <para>
///         What a screen opens it names by its <see cref="Subject" />, and <see cref="Arrival" /> brings it up — so
///         a screen opening an account and the shell opening one are the same read, with the same stale rule, rather
///         than a delegate per kind of thing opened (#233).
///     </para>
/// </remarks>
public sealed class Reach
{
    private readonly Arrival _arrival;
    private readonly Action _changed;
    private readonly Action<Confirmation> _confirm;
    private readonly Action<DestinationKind, int> _count;
    private readonly SubjectCache _cache;
    private readonly Enquiry _enquiry;
    private readonly Says _say;
    private readonly Action<Account> _stands;

    /// <summary>Built by the shell alone, with the shell's own hands for everything that touches its stack.</summary>
    internal Reach(
        ActiveProfile profile,
        ShellPorts ports,
        Enquiry enquiry,
        Arrival arrival,
        SubjectCache cache,
        Says say,
        Action<Confirmation> confirm,
        Action changed,
        Action<DestinationKind, int> count,
        Action<Account> stands)
    {
        Profile = profile;
        Ports = ports;
        _enquiry = enquiry;
        _arrival = arrival;
        _cache = cache;
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

    /// <summary>Drills into <paramref name="subject" />, on top of whatever is showing.</summary>
    public Task Open(Subject subject) => _arrival.Open(subject);

    /// <summary>
    ///     Stands <paramref name="subject" /> in place of the screen showing — the other side of a follow list, the
    ///     other run of an account — where that screen is still the one in front when it lands (#180, #229).
    /// </summary>
    public Task Swap(Subject subject) => _arrival.Swap(subject);

    /// <summary>Puts <paramref name="notice" /> on the status row, or takes whatever is there off it.</summary>
    public void Say(string? notice, bool isError) => _say(notice, isError);

    /// <summary>Asks before going ahead, and goes ahead with what the confirmation carries once it is agreed to.</summary>
    public void Confirm(Confirmation confirmation) => _confirm(confirmation);

    /// <summary>Says something on screen has changed, so that it is drawn again.</summary>
    public void Changed() => _changed();

    /// <summary>Lets go of what <paramref name="subject" /> last held, so that reaching it again asks again.</summary>
    public void Forget(Subject subject) => _cache.Forget(subject);

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
