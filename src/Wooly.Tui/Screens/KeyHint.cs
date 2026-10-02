using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One key and what it does here. A key means different things on different screens — <c>d</c> dismisses a
///     notification and deletes a post — which is workable only because the status row always says which
///     (<c>docs/tui-shell.md</c>), so every screen owes a list of these.
/// </summary>
/// <param name="Key">How the key is written, e.g. <c>⏎</c> or <c>shift-tab</c>.</param>
/// <param name="Does">What it does, in as few words as the status row has room for.</param>
/// <param name="NeedsAPick">
///     Whether it needs something <b>picked</b> to act on, and so has nothing to do while the screen's list is empty —
///     <c>d dismiss</c> on an inbox with nothing waiting, <c>a accept</c> with nobody asking. The thing picked out, or
///     the list it would be picked from: <c>D clear all</c> empties the whole inbox and is as idle on an empty one,
///     which is the same fact CONTEXT.md states as an empty list having nothing picked (#195).
///     Declared here, on the key, because this is the one place the key is already written down: a second list of
///     which keys need a pick would be a list a screen could forget to keep in step, and a row that lies is the
///     failure this exists to stop. <see langword="false" /> by default, that being every key that acts on the screen
///     itself — the walk, <c>g</c>, <c>tab</c>, <c>?</c> — and what leaves every existing declaration as it was.
/// </param>
public readonly record struct KeyHint(string Key, string Does, bool NeedsAPick = false)
{
    /// <summary>
    ///     The explanation and its key as the status row draws them, <c>Does: key</c> — lazygit's strip (ADR-0021,
    ///     #268): the words capitalised as a label and in <see cref="Role.Muted" />, reusing its existing hints job
    ///     rather than a role of its own (#66), then the key in <see cref="Role.Key" /> (#221). Only the first letter
    ///     of the words is capitalised and never the key, which is case-significant: <c>d</c> deletes, <c>D</c> clears
    ///     all.
    /// </summary>
    public IReadOnlyList<Span> Spans =>
        [new Span($"{char.ToUpperInvariant(Does[0])}{Does[1..]}: ", Role.Muted), new Span(Key, Role.Key)];

    /// <summary>
    ///     The pair written compactly, key first — how the docs and the tests name a hint (<c>F:follow</c>), which the
    ///     row no longer draws (<see cref="Spans" />).
    /// </summary>
    public override string ToString() => $"{Key}:{Does}";
}
