using System.Globalization;
using Terminal.Gui.Drawing;

namespace Wooly.Tui.Theme;

/// <summary>
///     What a colour may be written as in a theme: a hex triple, or one of the sixteen names ANSI has for the colours
///     a terminal has always had (docs/tui-shell.md).
/// </summary>
/// <remarks>
///     Two spellings rather than one because they answer different questions. Hex says exactly which colour, and
///     Terminal.Gui quantises it to the nearest of sixteen where the terminal has only sixteen, so a theme is authored
///     once. A name says <em>the terminal's own red</em>, which is what a theme that wants to sit inside somebody
///     else's carefully chosen palette has to be able to say.
/// </remarks>
public static class ColourName
{
    /// <summary>
    ///     The sixteen, spelled the way ANSI spells them rather than the way Terminal.Gui's own enum does. Two of them
    ///     are worth knowing about: ANSI's <c>white</c> is the dim one every terminal writes its text in, and the
    ///     bright one is <c>bright-white</c>; <c>bright-black</c> is the dark grey.
    /// </summary>
    private static readonly Dictionary<string, ColorName16> Sixteen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = ColorName16.Black,
        ["red"] = ColorName16.Red,
        ["green"] = ColorName16.Green,
        ["yellow"] = ColorName16.Yellow,
        ["blue"] = ColorName16.Blue,
        ["magenta"] = ColorName16.Magenta,
        ["cyan"] = ColorName16.Cyan,
        ["white"] = ColorName16.Gray,
        ["bright-black"] = ColorName16.DarkGray,
        ["bright-red"] = ColorName16.BrightRed,
        ["bright-green"] = ColorName16.BrightGreen,
        ["bright-yellow"] = ColorName16.BrightYellow,
        ["bright-blue"] = ColorName16.BrightBlue,
        ["bright-magenta"] = ColorName16.BrightMagenta,
        ["bright-cyan"] = ColorName16.BrightCyan,
        ["bright-white"] = ColorName16.White,
    };

    /// <summary>The colour <paramref name="written" /> names, or <see langword="null" /> where it names none.</summary>
    public static Color? Parse(string? written)
    {
        if (written is null)
        {
            return null;
        }

        var value = written.Trim();

        if (Sixteen.TryGetValue(value, out var named))
        {
            return new Color(named);
        }

        // A triple and nothing shorter: #fff would be a second spelling of the same colour, and this is a file people
        // copy lines between.
        if (value.Length != 7 || value[0] != '#')
        {
            return null;
        }

        // The hex specifier alone, without the leading and trailing whitespace NumberStyles.HexNumber would wave
        // through: "# ff000" is a typo rather than a colour, and one that would otherwise parse as a different one.
        return int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff)
            : null;
    }

    /// <summary>
    ///     How a value that is not a colour is described, in one place so that every theme key turned down is turned
    ///     down in the same words.
    /// </summary>
    public static string Rejection(string written) =>
        $"'{written}' is not a colour. Write a hex triple like \"#8fa8ff\", or one of "
        + $"{string.Join(", ", Sixteen.Keys)}.";
}
