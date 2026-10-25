namespace Wooly.Tui.Screens;

/// <summary>
///     A screen whose list <b>reads on</b> (GLOSSARY.md, #460): what a read on brought is put under what it holds, by
///     the screen, which is the only thing that knows how its list is kept.
/// </summary>
/// <remarks>
///     Said by the screen rather than by each subject that reads on into one, so that a read on lands on whichever
///     list it was asked from without its subject having to say which kind of screen that is.
/// </remarks>
/// <typeparam name="T">What the list is of.</typeparam>
public interface IReadsOn<T>
{
    /// <summary>
    ///     Everything the list that reads on holds, top to bottom: what arrived live on top and what was answered since
    ///     as much as anything read — which is what a badge counted off the list is counted again off (#466).
    /// </summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>
    ///     Puts <paramref name="older" /> under everything the list holds, as a read on brought them: below everything,
    ///     with the pick left where it was and nothing above it moved.
    /// </summary>
    void ReadOn(IReadOnlyList<T> older);
}
