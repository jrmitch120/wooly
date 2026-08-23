using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     The one place a <c>Terminal.Gui</c> press becomes a key this project has a word for. Everything past here talks
///     in <see cref="ShellKey" />, which is what lets every binding be asserted with no <c>Window</c> in the room
///     (#147).
/// </summary>
/// <remarks>
///     Two tables, because a terminal delivers two kinds of key. A named one — <c>⏎</c>, the arrows, a ctrl pair —
///     arrives as a code and is matched on it. A typed one arrives as a character, and is matched on that rather than
///     on a code and a modifier: the capitals are how the contract tells a tie key from a mark key, and a reader who
///     has caps lock on has still typed a capital (<c>docs/tui-shell.md</c>).
/// </remarks>
internal static class ShellKeys
{
    /// <summary>The keys that arrive as a code: the frame's, the movements, and the three ctrl pairs.</summary>
    private static readonly Dictionary<KeyCode, ShellKey> Coded = new()
    {
        [Key.Enter.KeyCode] = ShellKey.Enter,
        [Key.Esc.KeyCode] = ShellKey.Escape,
        [Key.Tab.KeyCode] = ShellKey.Tab,
        [Key.Tab.WithShift.KeyCode] = ShellKey.ShiftTab,
        [Key.CursorUp.KeyCode] = ShellKey.Up,
        [Key.CursorDown.KeyCode] = ShellKey.Down,
        [Key.CursorLeft.KeyCode] = ShellKey.Left,
        [Key.CursorRight.KeyCode] = ShellKey.Right,
        [Key.PageUp.KeyCode] = ShellKey.PageUp,
        [Key.PageDown.KeyCode] = ShellKey.PageDown,
        [Key.Home.KeyCode] = ShellKey.Home,
        [Key.End.KeyCode] = ShellKey.End,
        [Key.Q.WithCtrl.KeyCode] = ShellKey.CtrlQ,
        [Key.S.WithCtrl.KeyCode] = ShellKey.CtrlS,
        [Key.W.WithCtrl.KeyCode] = ShellKey.CtrlW,
    };

    /// <summary>The keys that arrive as a character: the letters, the four capitals, the digits, <c>/</c> and <c>?</c>.</summary>
    private static readonly Dictionary<char, ShellKey> Typed = new()
    {
        ['a'] = ShellKey.A,
        ['b'] = ShellKey.B,
        ['c'] = ShellKey.C,
        ['d'] = ShellKey.D,
        ['e'] = ShellKey.E,
        ['f'] = ShellKey.F,
        ['g'] = ShellKey.G,
        ['j'] = ShellKey.J,
        ['k'] = ShellKey.K,
        ['m'] = ShellKey.M,
        ['p'] = ShellKey.P,
        ['r'] = ShellKey.R,
        ['v'] = ShellKey.V,
        ['x'] = ShellKey.X,
        ['B'] = ShellKey.CapitalB,
        ['D'] = ShellKey.CapitalD,
        ['F'] = ShellKey.CapitalF,
        ['M'] = ShellKey.CapitalM,
        ['/'] = ShellKey.Slash,
        ['?'] = ShellKey.Question,
        ['1'] = ShellKey.One,
        ['2'] = ShellKey.Two,
        ['3'] = ShellKey.Three,
        ['4'] = ShellKey.Four,
        ['5'] = ShellKey.Five,
        ['6'] = ShellKey.Six,
        ['7'] = ShellKey.Seven,
        ['8'] = ShellKey.Eight,
        ['9'] = ShellKey.Nine,
        ['0'] = ShellKey.Zero,
    };

    /// <summary>
    ///     Which of this shell's keys was pressed, or <see langword="null" /> where it was one the shell has no word
    ///     for — a function key, an alt pair, a letter no screen answers to.
    /// </summary>
    /// <remarks>
    ///     The coded table first, since <c>⏎</c> and <c>tab</c> carry a character of their own that means nothing here.
    ///     A ctrl or alt press that got past it is not a typed key however printable its character looks, which is
    ///     what keeps <c>ctrl-b</c> from boosting.
    /// </remarks>
    public static ShellKey? Of(Key key)
    {
        if (Coded.TryGetValue(key.KeyCode, out var coded))
        {
            return coded;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return null;
        }

        var rune = key.AsRune.Value;

        return rune <= char.MaxValue && Typed.TryGetValue((char)rune, out var typed) ? typed : null;
    }
}
