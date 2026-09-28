namespace Wooly.Tui;

/// <summary>
///     What the TUI leaves behind on the process. Two of them, and deliberately not the CLI's five (ADR-0006): those
///     tell a script which kind of failure it hit, and nothing scripts the TUI. What is left is whether it ever got
///     as far as a screen.
/// </summary>
public enum TuiExit
{
    /// <summary>It ran and the reader quit it.</summary>
    Success = 0,

    /// <summary>
    ///     It could not open — a config file it could not read or that names a default that is not there, or a
    ///     <c>--profile</c> naming nothing. Not nobody to act as, nor a token missing or refused: those open on adding
    ///     a profile (#247).
    /// </summary>
    Failed = 1,
}
