namespace Wooly.Tui.Screens;

/// <summary>
///     One key and what it does here. A key means different things on different screens — <c>d</c> dismisses a
///     notification and deletes a post — which is workable only because the status row always says which
///     (<c>docs/tui-shell.md</c>), so every screen owes a list of these.
/// </summary>
/// <param name="Key">How the key is written, e.g. <c>⏎</c> or <c>shift-tab</c>.</param>
/// <param name="Does">What it does, in as few words as the status row has room for.</param>
public readonly record struct KeyHint(string Key, string Does)
{
    /// <summary>How the pair reads on one row: the key, then what it does.</summary>
    public override string ToString() => $"{Key} {Does}";
}
