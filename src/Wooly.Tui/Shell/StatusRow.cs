namespace Wooly.Tui.Shell;

/// <summary>
///     The one row at the foot of the shell (<c>GLOSSARY.md</c>): the <see cref="Notice" /> said on it, the
///     <see cref="Confirmation" /> open on it, which of them is <see cref="Shown" />, and the wait that lets a remark
///     about a key just pressed go (#413, #436).
/// </summary>
/// <remarks>
///     The shell keeps what sets it going — a push clears it, <c>esc</c> and a profile switch dismiss the
///     confirmation — and this keeps the rules, so they are written once rather than wherever the shell says or
///     answers: anything said calls the wait off, and while a confirmation is open it takes the press.
/// </remarks>
/// <param name="after">
///     The host's wait (<see cref="IShellHost.After" />), which runs its work once on the drawing thread and is called
///     off when the handle it gives back is disposed.
/// </param>
/// <param name="remarkFor">
///     How long a remark said for a moment stands (<see cref="ShellTiming.WidthSaidFor" />).
/// </param>
/// <param name="changed">Called whenever what the row shows may have changed, so that it is drawn again.</param>
public sealed class StatusRow(Func<TimeSpan, Action, IDisposable> after, TimeSpan remarkFor, Action changed)
{
    /// <summary>The wait after which a remark said for a moment is let go of, which anything said calls off.</summary>
    private IDisposable? _wait;

    /// <summary>
    ///     What is said on the row, as one value: its words and whether it is a failure. Kept while a confirmation is
    ///     open, behind it, and shown once that is answered.
    /// </summary>
    private Shown.Notice? _notice;

    /// <summary>
    ///     Something said that is not a screen: a refusal, or the countdown on a rate limit being waited out. Kept
    ///     while a confirmation is open, behind it, and shown once that is answered.
    /// </summary>
    public string? Notice => _notice?.Text;

    /// <summary>Whether that notice is a failure rather than a remark, which settles the role it is drawn in.</summary>
    public bool NoticeIsError => _notice?.IsError ?? false;

    /// <summary>
    ///     What the shell is waiting to be told again before it does, or <see langword="null" /> if nothing.
    /// </summary>
    public Confirmation? Asking { get; private set; }

    /// <summary>What the row shows: the confirmation, else the notice, else the keys of the screen in front.</summary>
    public Shown Shown =>
        Asking is { } asking ? new Shown.Asking(asking)
        : _notice is { } notice ? notice
        : new Shown.Keys();

    /// <summary>
    ///     Puts <paramref name="notice" /> on the row, or takes whatever is there off it where it is
    ///     <see langword="null" />. Calls off the wait on a remark said for a moment, so what is said now stands.
    /// </summary>
    public void Say(string? notice, bool isError)
    {
        _wait?.Dispose();
        _wait = null;

        _notice = notice is null ? null : new Shown.Notice(notice, isError);

        changed();
    }

    /// <summary>
    ///     Says <paramref name="remark" /> and lets go of it once its moment has passed — a remark about a key just
    ///     pressed, which is worth nothing once the reader has seen what the key did. Anything said in the meantime
    ///     stands.
    /// </summary>
    public void SayForAMoment(string remark)
    {
        Say(remark, isError: false);

        _wait = after(remarkFor, Clear);
    }

    /// <summary>Lets go of whatever notice the row was saying, by the same rules as saying something.</summary>
    public void Clear() => Say(null, isError: false);

    /// <summary>
    ///     Waits to be told again before going ahead with what <paramref name="confirmation" /> carries. A notice said
    ///     while it is open waits behind it.
    /// </summary>
    public void Ask(Confirmation confirmation)
    {
        Asking = confirmation;

        changed();
    }

    /// <summary>
    ///     <paramref name="press" /> as the answer to the confirmation open on the row, where one is: the key that
    ///     agrees goes ahead (<see cref="Confirmation.AgreedBy" />), and any other press keeps —
    ///     <see langword="null" />, a click or a notch of the wheel, among them (story 43). Where none is open it does
    ///     nothing.
    /// </summary>
    /// <returns>Whether a confirmation was open, and the press was spent on it.</returns>
    public bool Answered(ShellKey? press)
    {
        if (Asking is not { } open)
        {
            return false;
        }

        _ = Answer(open.AgreedBy(press));

        return true;
    }

    /// <summary>
    ///     Puts away the confirmation open on the row, where one is, without going ahead with what it carries — not
    ///     an answer to it but the shell letting go of it, as when the profile it was asked as is switched away from.
    ///     Where none is open it does nothing.
    /// </summary>
    public void Dismiss()
    {
        if (Asking is null)
        {
            return;
        }

        Asking = null;

        changed();
    }

    /// <summary>
    ///     <paramref name="press" /> as <see cref="Answered" /> takes it, for a test to await what agreeing goes ahead
    ///     with.
    /// </summary>
    internal Task Answer(ShellKey? press) => Answer(Asking?.AgreedBy(press) == true);

    /// <summary>
    ///     Answers the confirmation open on the row, where one is: put away either way, and gone ahead with where
    ///     <paramref name="agreed" />. What <see cref="Answered" /> runs, and what a test awaits to see the going ahead
    ///     finish.
    /// </summary>
    internal async Task Answer(bool agreed)
    {
        var confirmed = Asking;

        Asking = null;

        changed();

        if (agreed && confirmed is not null)
        {
            await confirmed.Agreed();
        }
    }
}
