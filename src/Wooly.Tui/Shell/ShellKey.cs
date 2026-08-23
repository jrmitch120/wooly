namespace Wooly.Tui.Shell;

/// <summary>
///     A key this shell answers to, as this project names it rather than as a terminal delivers it. Every key in
///     <c>docs/tui-shell.md</c>'s tables is here, and nothing that is not.
/// </summary>
/// <remarks>
///     Named for the key rather than for what it does, because what it does is not a property of the key: a key means
///     different things on different screens — <c>d</c> dismisses a notification and deletes a post — and
///     <see cref="Keymap" /> is the one place the two meet. Naming a member for either meaning would put half of that
///     table here.
///     <para>
///         The point of this enum existing at all is that it is not <c>Terminal.Gui</c>'s <c>Key</c>: the framework
///         stops at <c>ShellWindow</c>, which translates a press into one of these and hands it on, so every binding
///         can be asserted with no <c>Window</c> in the room.
///     </para>
/// </remarks>
public enum ShellKey
{
    /// <summary><c>⏎</c>.</summary>
    Enter,

    /// <summary><c>esc</c>.</summary>
    Escape,

    /// <summary><c>tab</c>.</summary>
    Tab,

    /// <summary><c>shift-tab</c>.</summary>
    ShiftTab,

    /// <summary><c>↑</c>.</summary>
    Up,

    /// <summary><c>↓</c>.</summary>
    Down,

    /// <summary><c>←</c>.</summary>
    Left,

    /// <summary><c>→</c>.</summary>
    Right,

    /// <summary><c>PgUp</c>.</summary>
    PageUp,

    /// <summary><c>PgDn</c>.</summary>
    PageDown,

    /// <summary><c>Home</c>.</summary>
    Home,

    /// <summary><c>End</c>.</summary>
    End,

    /// <summary><c>ctrl-q</c>.</summary>
    CtrlQ,

    /// <summary><c>ctrl-s</c>.</summary>
    CtrlS,

    /// <summary><c>ctrl-w</c>.</summary>
    CtrlW,

    /// <summary><c>/</c>.</summary>
    Slash,

    /// <summary><c>?</c>.</summary>
    Question,

    /// <summary><c>a</c>.</summary>
    A,

    /// <summary><c>b</c>.</summary>
    B,

    /// <summary><c>c</c>.</summary>
    C,

    /// <summary><c>d</c>.</summary>
    D,

    /// <summary><c>e</c>.</summary>
    E,

    /// <summary><c>f</c>.</summary>
    F,

    /// <summary><c>g</c>.</summary>
    G,

    /// <summary><c>j</c>.</summary>
    J,

    /// <summary><c>k</c>.</summary>
    K,

    /// <summary><c>m</c>.</summary>
    M,

    /// <summary><c>p</c>.</summary>
    P,

    /// <summary><c>r</c>.</summary>
    R,

    /// <summary><c>v</c>.</summary>
    V,

    /// <summary><c>x</c>.</summary>
    X,

    /// <summary>
    ///     <c>B</c>. The four capitals are keys of their own rather than a modifier on the four above, because that is
    ///     what the contract makes them: a capital is how a tie key is told apart from a mark key, so a shell that
    ///     folded the two together would let <c>b</c> block somebody (<c>docs/tui-shell.md</c>).
    /// </summary>
    CapitalB,

    /// <inheritdoc cref="CapitalB" />
    CapitalD,

    /// <inheritdoc cref="CapitalB" />
    CapitalF,

    /// <inheritdoc cref="CapitalB" />
    CapitalM,

    /// <summary>
    ///     <c>1</c>. Ten keys rather than one with a number on it, so that the whole of this enum is things a reader
    ///     pressed — which answer each addresses is <see cref="Keymap.Answer" />'s, and is the same on every screen.
    /// </summary>
    One,

    /// <inheritdoc cref="One" />
    Two,

    /// <inheritdoc cref="One" />
    Three,

    /// <inheritdoc cref="One" />
    Four,

    /// <inheritdoc cref="One" />
    Five,

    /// <inheritdoc cref="One" />
    Six,

    /// <inheritdoc cref="One" />
    Seven,

    /// <inheritdoc cref="One" />
    Eight,

    /// <inheritdoc cref="One" />
    Nine,

    /// <inheritdoc cref="One" />
    Zero,
}
