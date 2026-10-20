namespace Wooly.Tui.Screens;

/// <summary>
///     A screen holding something written and not yet sent, which goes with the screen: a post being composed, and the
///     edit public profile form (#373, #406). Every way out that would take one off the stack asks first where it is
///     touched, and a way out that leaves the stack to something else to settle takes one off rather than leaving it
///     standing under what comes next.
/// </summary>
public interface IDraft
{
    /// <summary>Whether anything here differs from what the screen opened with, which is what makes leaving it worth a question.</summary>
    bool Touched { get; }

    /// <summary>What is asked before it is thrown away, naming what would be lost.</summary>
    string Discarding { get; }
}
