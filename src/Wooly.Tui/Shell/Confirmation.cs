namespace Wooly.Tui.Shell;

/// <summary>
///     Something the shell will not do until it is told again. Story 43's rule, and the one thing in this client whose
///     effect running another command does not undo — so a person at a terminal is asked, exactly as
///     <c>post delete</c> asks one.
/// </summary>
/// <param name="Question">What is being asked, in full, including that it cannot be undone.</param>
/// <param name="Confirm">The key that goes ahead with it.</param>
public sealed record Confirmation(string Question, string Confirm = "y");
