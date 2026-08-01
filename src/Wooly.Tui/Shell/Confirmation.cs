namespace Wooly.Tui.Shell;

/// <summary>
///     Something the shell will not do until it is told again. Story 43's rule, and the things in this client whose
///     effect running another command does not undo — so a person at a terminal is asked, exactly as
///     <c>post delete</c> and <c>notification clear</c> ask one.
/// </summary>
/// <param name="Question">What is being asked, in full, including that it cannot be undone.</param>
/// <param name="Going">
///     The word for going ahead, which the status row puts against the key. Named rather than assumed: "delete" and
///     "clear" are different words for the same keypress, and a row that said the wrong one would be asking a
///     question nobody could answer confidently.
/// </param>
/// <param name="Confirm">The key that goes ahead with it.</param>
public sealed record Confirmation(string Question, string Going = "delete", string Confirm = "y");
