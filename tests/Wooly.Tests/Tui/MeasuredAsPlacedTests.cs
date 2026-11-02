using System.Globalization;
using System.Text;
using Terminal.Gui.Drivers;
using Wooly.Tui.Rendering;

namespace Wooly.Tests.Tui;

/// <summary>
///     Every character there is, held to the two things a row depends on: the columns <see cref="Glyphs.Columns" /> gives
///     it are the cells Terminal.Gui puts it in, and a character Unicode calls wide is one this client either counts two
///     wide or holds (ADR-0031). A row a layout and a grid disagree about comes apart at its edge, and a table upgraded
///     on one side only is how a disagreement arrives: these are what says so before a post does.
/// </summary>
public class MeasuredAsPlacedTests
{
    private const int Cells = 14;

    /// <summary>
    ///     Between two letters, each character's measure is where Terminal.Gui puts what follows it. Text is held to this
    ///     as a span would paint it, with what <see cref="Glyphs.Plain" /> drops gone.
    /// </summary>
    [Fact]
    public void WhatFollowsACharacterIsWhereItsMeasureSays()
    {
        var apart = new List<int>();

        foreach (var character in Every())
        {
            var text = Glyphs.Plain($"a{character}|");

            // Held, it is painted through its own blank rather than as a run (HeldText).
            if (Glyphs.MeasuredAlike(text) && BarAt(text) is { } bar && bar != Glyphs.Columns(text) - 1)
            {
                apart.Add(character.Value);
            }
        }

        Assert.Equal(string.Empty, Ranges(apart.Where(value => !LoneVowelJamo(value))));
    }

    /// <summary>
    ///     Every character Unicode calls wide takes two columns here or is held in two. What takes no room of its own is
    ///     left out, along with a skin tone, which is wide only with nothing before it to wear it.
    /// </summary>
    [Fact]
    public void ACharacterUnicodeCallsWideIsCountedTwoWideOrHeld()
    {
        var narrow = new List<int>();

        foreach (var line in File.ReadLines(Path.Combine(RepositoryRoot.Path, "tests/Wooly.Tests/Tui/UnicodeWide.txt")))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            var ends = line.Split("..");
            var first = int.Parse(ends[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var last = int.Parse(ends[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            for (var value = first; value <= last; value++)
            {
                var character = new Rune(value);

                if (Rune.GetUnicodeCategory(character)
                        is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
                    && !SkinTone(value)
                    && Glyphs.Columns(character.ToString()) != 2)
                {
                    narrow.Add(value);
                }
            }
        }

        Assert.Equal(string.Empty, Ranges(narrow));
    }

    private static IEnumerable<Rune> Every()
    {
        for (var value = 0; value <= 0x10FFFF; value++)
        {
            if (Rune.IsValid(value) && value != '|')
            {
                yield return new Rune(value);
            }
        }
    }

    /// <summary>
    ///     Where Terminal.Gui puts the bar at the end of <paramref name="text" />, or null where it has joined it to what
    ///     came before it, as Unicode's prepended marks ask, and it has no cell of its own to be looked for in.
    /// </summary>
    private static int? BarAt(string text)
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Cells, 1);
        buffer.Move(0, 0);
        buffer.AddStr(text);

        for (var column = 0; column < Cells; column++)
        {
            if (buffer.Contents![0, column].Grapheme == "|")
            {
                return column;
            }
        }

        return null;
    }

    /// <summary>
    ///     A vowel or final of Hangul's conjoining jamo with no consonant before it, which no Korean text has: Terminal.Gui
    ///     counts it as no column and gives it a cell of its own all the same, and nothing here can tell it from one that
    ///     has its consonant without cutting all Korean up.
    /// </summary>
    private static bool LoneVowelJamo(int value) => value is (>= 0x1160 and <= 0x11FF) or (>= 0xD7B0 and <= 0xD7FF);

    private static bool SkinTone(int value) => value is >= 0x1F3FB and <= 0x1F3FF;

    private static string Ranges(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        var ranges = new StringBuilder();

        for (var at = 0; at < sorted.Count;)
        {
            var end = at;

            while (end + 1 < sorted.Count && sorted[end + 1] == sorted[end] + 1)
            {
                end++;
            }

            ranges.Append(CultureInfo.InvariantCulture, $"{sorted[at]:X}..{sorted[end]:X} ");
            at = end + 1;
        }

        return ranges.ToString();
    }
}
