using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. A bare cell grid each variant paints onto; nothing here is meant to survive.

/// <summary>Catppuccin Mocha, because half the modern TUIs this borrows from ship it as their default.</summary>
internal static class P
{
    public const uint Base = 0x1e1e2e, Mantle = 0x181825, Crust = 0x11111b;
    public const uint Surface0 = 0x313244, Surface1 = 0x45475a, Surface2 = 0x585b70;
    public const uint Overlay = 0x6c7086, Subtext = 0xa6adc8, Text = 0xcdd6f4;
    public const uint Mauve = 0xcba6f7, Blue = 0x89b4fa, Sapphire = 0x74c7ec, Teal = 0x94e2d5, Green = 0xa6e3a1;
    public const uint Yellow = 0xf9e2af, Peach = 0xfab387, Red = 0xf38ba8, Pink = 0xf5c2e7, Lavender = 0xb4befe;

    public static Color C(uint hex) => new((int)((hex >> 16) & 0xff), (int)((hex >> 8) & 0xff), (int)(hex & 0xff), 255);

    public static Attribute A(uint fg, uint bg = Base) => new(C(fg), C(bg));

    public static Attribute B(uint fg, uint bg = Base) => new(C(fg), C(bg), TextStyle.Bold);

    public static Attribute I(uint fg, uint bg = Base) => new(C(fg), C(bg), TextStyle.Italic);
}

internal sealed class Surface(View? view, int width, int height, char[,]? grid = null)
{
    /// <summary>A surface painting into plain text, for --dump.</summary>
    public static Surface Text(int width, int height, out char[,] grid)
    {
        grid = new char[height, width];

        return new Surface(null, width, height, grid);
    }

    public int W => width;
    public int H => height;

    /// <summary>Writes text, clipped to the screen and to <paramref name="max" /> columns. Returns the column after it.</summary>
    public int Put(int x, int y, string text, Attribute a, int max = int.MaxValue)
    {
        if (y < 0 || y >= height || x >= width || text.Length == 0 || max <= 0)
        {
            return x + text.Length;
        }

        var end = x + text.Length;

        if (x < 0)
        {
            if (-x >= text.Length)
            {
                return end;
            }

            text = text[-x..];
            max += x;
            x = 0;
        }

        var room = Math.Min(max, width - x);

        if (room <= 0)
        {
            return end;
        }

        if (text.Length > room)
        {
            text = text[..room];
        }

        if (grid is not null)
        {
            for (var i = 0; i < text.Length; i++)
            {
                grid[y, x + i] = text[i];
            }
        }
        else
        {
            view!.SetAttribute(a);
            view.AddStr(x, y, text);
        }

        return end;
    }

    public void Fill(int x, int y, int w, int h, Attribute a)
    {
        if (w <= 0)
        {
            return;
        }

        var blank = new string(' ', w);

        for (var r = 0; r < h; r++)
        {
            Put(x, y + r, blank, a);
        }
    }

    /// <summary>A box, with an optional title in the top edge and a footer in the bottom-right edge (lazygit-style).</summary>
    public void Box(int x, int y, int w, int h, Attribute line, Attribute inside, string? title = null,
        Attribute? titleA = null, string? foot = null, bool rounded = true)
    {
        if (w < 2 || h < 2)
        {
            return;
        }

        Fill(x + 1, y + 1, w - 2, h - 2, inside);

        var (tl, tr, bl, br) = rounded ? ("╭", "╮", "╰", "╯") : ("┌", "┐", "└", "┘");

        Put(x, y, tl + new string('─', w - 2) + tr, line);
        Put(x, y + h - 1, bl + new string('─', w - 2) + br, line);

        for (var r = 1; r < h - 1; r++)
        {
            Put(x, y + r, "│", line);
            Put(x + w - 1, y + r, "│", line);
        }

        if (title is not null)
        {
            Put(x + 1, y, Prototype.Text.Clip(title, w - 2), titleA ?? line);
        }

        if (foot is not null && foot.Length < w - 3)
        {
            Put(x + w - 1 - foot.Length - 1, y + h - 1, foot, line);
        }
    }

    public void Rule(int x, int y, int w, Attribute a, char c = '─') => Put(x, y, new string(c, Math.Max(0, w)), a);

    public void VRule(int x, int y, int h, Attribute a)
    {
        for (var r = 0; r < h; r++)
        {
            Put(x, y + r, "│", a);
        }
    }
}

internal static class Text
{
    public static string Clip(string s, int w) =>
        w <= 0 ? string.Empty : s.Length <= w ? s : w == 1 ? "…" : s[..(w - 1)] + "…";

    public static string Pad(string s, int w) => Clip(s, w).PadRight(Math.Max(0, w));

    public static string Right(string s, int w) => Clip(s, w).PadLeft(Math.Max(0, w));

    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();

        if (width <= 0)
        {
            return lines;
        }

        foreach (var paragraph in text.Split('\n'))
        {
            var line = string.Empty;

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length == 0)
                {
                    line = word;
                }
                else if (line.Length + 1 + word.Length <= width)
                {
                    line += " " + word;
                }
                else
                {
                    lines.Add(line);
                    line = word;
                }

                while (line.Length > width)
                {
                    lines.Add(line[..width]);
                    line = line[width..];
                }
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Keeps <paramref name="sel" /> on a page of <paramref name="h" /> rows starting at <paramref name="top" />.</summary>
    public static int Follow(int sel, int top, int h) =>
        h <= 0 ? 0 : sel < top ? sel : sel >= top + h ? sel - h + 1 : top;

    /// <summary>Draws text with #tags and @mentions coloured, one wrapped row at a time.</summary>
    public static void Rich(Surface s, int x, int y, string row, uint bg, uint fg = P.Text)
    {
        var col = x;

        foreach (var word in row.Split(' '))
        {
            var colour = word.StartsWith('#') ? P.Sapphire : word.StartsWith('@') ? P.Mauve :
                word.StartsWith("http") ? P.Blue : fg;

            col = s.Put(col, y, word, P.A(colour, bg));
            col = s.Put(col, y, " ", P.A(fg, bg));
        }
    }
}

internal static class Keys
{
    public static char Ch(Key k) =>
        k.IsCtrl || k.IsAlt || k.AsRune.Value > char.MaxValue ? '\0' : (char)k.AsRune.Value;

    public static bool Down(Key k) => k.KeyCode == Key.CursorDown.KeyCode || Ch(k) == 'j';
    public static bool Up(Key k) => k.KeyCode == Key.CursorUp.KeyCode || Ch(k) == 'k';
    public static bool Left(Key k) => k.KeyCode == Key.CursorLeft.KeyCode || Ch(k) == 'h';
    public static bool Right(Key k) => k.KeyCode == Key.CursorRight.KeyCode || Ch(k) == 'l';
    public static bool Enter(Key k) => k.KeyCode == Key.Enter.KeyCode;
    public static bool Esc(Key k) => k.KeyCode == Key.Esc.KeyCode;
    public static bool Tab(Key k) => k.KeyCode == Key.Tab.KeyCode;
    public static bool ShiftTab(Key k) => k.KeyCode == Key.Tab.WithShift.KeyCode;
    public static bool Backspace(Key k) => k.KeyCode == Key.Backspace.KeyCode;
    public static bool CtrlP(Key k) => k.KeyCode == Key.P.WithCtrl.KeyCode;

    public static int Wrap(int i, int n) => n == 0 ? 0 : ((i % n) + n) % n;
}

/// <summary>One whole-screen design. Each owns its layout outright; nothing structural is shared between them.</summary>
internal abstract class Variant
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Inspiration { get; }

    public abstract void Draw(Surface s);

    /// <summary>Returns whether the key was used.</summary>
    public abstract bool OnKey(Key k);

    /// <summary>Whether the variant is holding the keyboard (a palette or text field), so &lt; and &gt; are left to it.</summary>
    public virtual bool Typing => false;
}
