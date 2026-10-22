using Wooly.Tui.Screens;

namespace Wooly.Tui.Drafting;

/// <summary>
///     What <see cref="Drafting" /> can reach of the stack of screens (ADR-0029): what is on it, what is on top, putting
///     a screen on, and taking a named one off. The shell's own stack in a session, and a list in the tests — one set of
///     facts holds of both.
/// </summary>
/// <remarks>
///     Never "take off the top" and never an index: a screen is taken off by naming it, so an answer that lands after
///     something else has been put on top can only ever take off the screen it was about.
///     <para>
///         Both hooks are called by whoever takes a screen off, on every way off — <see cref="Drafting.Left" /> for each
///         screen before it is off the stack, so a description editor leaving can still reach its compose, and
///         <see cref="Drafting.Uncovered" /> once the stack has settled and the notice is cleared.
///     </para>
/// </remarks>
public interface IStack
{
    /// <summary>The screens on the stack, the bottom one first.</summary>
    IReadOnlyList<Screen> Screens { get; }

    /// <summary>The screen on top, which is the one in front of the reader.</summary>
    Screen Top { get; }

    /// <summary>Puts <paramref name="screen" /> on top, clearing the notice said over whatever was in front.</summary>
    void Push(Screen screen);

    /// <summary>
    ///     Takes <paramref name="screen" /> off, with every screen above it, telling drafting of each as it leaves and
    ///     that what is left was uncovered. Nothing where it is no longer on the stack, or is the bottom screen, which
    ///     never leaves this way: the stack is never left with nothing on it.
    /// </summary>
    void TakeOff(Screen screen);
}
