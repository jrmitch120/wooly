namespace Wooly.Tui.Shell;

/// <summary>
///     Something the shell will not do until it is told again. Story 43's rule, and the things in this client whose
///     effect running another command does not undo — so a person at a terminal is asked, exactly as
///     <c>post delete</c> and <c>notification clear</c> ask one.
/// </summary>
/// <param name="Ask">
///     What is being asked, naming nothing a reader cannot check against what is on screen (#219): the row it is
///     drawn on belongs to no post in particular, and the post it is about is the one drawn picked.
/// </param>
/// <param name="Agreed">
///     What going ahead does, carried by the question rather than kept beside it — so that whatever puts a question
///     says in the same breath what agreeing to it means, and the shell holds one thing while it waits rather than two
///     that could come apart (#232).
/// </param>
/// <param name="Going">
///     The word for going ahead, which the status row puts against the key. Named rather than assumed: "delete" and
///     "clear" are different words for the same keypress, and a row that said the wrong one would be asking a
///     question nobody could answer confidently.
/// </param>
/// <param name="Confirm">The key that goes ahead with it.</param>
/// <param name="Warning">
///     What is said after the ask, and the half of the question the status row drops first where both will not fit
///     (<see cref="Screens.ChromeLines.Status" />). Defaulted, because it is the same sentence on every confirmation
///     and carries nothing particular to what is being asked. Empty for a question that warns of nothing.
/// </param>
/// <param name="YesOrNo">
///     Whether the status row offers the answer as <c>y / n</c> rather than <c>y delete · esc keep</c>: for a question
///     put in front of a way out, <c>esc</c> among them, which is answered <c>y</c> to go on or anything else to stay
///     (#373).
/// </param>
public sealed record Confirmation(
    string Ask,
    Func<Task> Agreed,
    string Going = "delete",
    string Confirm = "y",
    string Warning = Confirmation.CannotBeUndone,
    bool YesOrNo = false)
{
    /// <summary>
    ///     What every confirmation warns, in the words <c>post delete</c> and <c>notification clear</c> already use.
    /// </summary>
    public const string CannotBeUndone = "This cannot be undone.";

    /// <summary>
    ///     Asked before a compose screen that differs from how it opened is thrown away, by whichever way out (#373):
    ///     <c>Discard this post? y / n</c>. Only <c>y</c> agrees — not the way out's own key pressed again, so a draft
    ///     is never lost to a key pressed twice. No warning, since the question is the whole of it.
    /// </summary>
    /// <param name="leave">The way out, finished once the question is agreed to.</param>
    public static Confirmation Discarding(Func<Task> leave) =>
        new("Discard this post?", leave, Going: "discard", Warning: string.Empty, YesOrNo: true);

    /// <summary>
    ///     Whether <paramref name="pressed" /> agrees: the key that goes ahead. Anything else — a key with no meaning to
    ///     the shell among them — is a no.
    /// </summary>
    public bool AgreedBy(ShellKey? pressed) => pressed == ShellKey.Y && Confirm == "y";
}
