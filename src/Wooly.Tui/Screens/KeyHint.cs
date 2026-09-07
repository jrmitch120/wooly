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
    ///     The key and its explanation as the status row draws them (#66): the key in <see cref="Role.Chrome" />, the
    ///     explanation in <see cref="Role.Muted" /> — reusing its existing hints job rather than a role of its own —
    ///     glued by a colon that costs no extra columns, being the same width as the space it replaces.
    /// </summary>
    public IReadOnlyList<Span> Spans => [new Span(Key, Role.Chrome), new Span($":{Does}", Role.Muted)];

    /// <summary>How the pair reads on one row: the key, then what it does.</summary>
    public override string ToString() => $"{Key}:{Does}";
}
