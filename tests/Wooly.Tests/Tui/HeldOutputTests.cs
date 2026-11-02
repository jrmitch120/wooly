using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Wooly.Tui.Rendering;
using Wooly.Tui.Views;

using Color = Terminal.Gui.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using TerminalAttribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     What reaches a terminal for a row holding a character terminals count differently, a spacing mark, one newer
///     than the tables or a symbol or emoji (ADR-0031): written by the client's own output, and played on a terminal
///     that draws such a character one, two or three columns wide, as WezTerm, herdr and GNU screen were measured to
///     draw a spacing mark. Whatever the width, nothing past what it covers on the row moves. And what lies past the
///     end of a row: written with the row, in a run of its own, so that a cell strayed there goes.
/// </summary>
public partial class HeldOutputTests
{
    private const int Columns = 12;

    /// <summary>
    ///     <c>অসমীয়া</c> as the painter leaves it — each character carrying a mark followed by a blank — then
    ///     <c>|ab</c>. The <c>b</c> is at column eight, where the row was laid out to put it, on every terminal.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void WhatFollowsIsWhereTheRowPutItHoweverWideTheTerminalDrawsTheMark(int drawnIn)
    {
        var terminal = Played("অসমী য়া |ab", drawnIn);

        Assert.Equal("b", terminal.At(8));
    }

    /// <summary>
    ///     And a terminal drawing it two wide — herdr — draws it over its blank and keeps it whole: drawn after
    ///     everything else on the row, nothing written afterwards lands on its second column and erases it.
    /// </summary>
    [Fact]
    public void ATerminalDrawingTheMarkTwoWideKeepsTheWordWhole() =>
        Assert.Equal("অসমীয়া|ab", Played("অসমী য়া |ab", 2).Text());

    /// <summary>A terminal drawing it one wide — WezTerm — shows the blank after it.</summary>
    [Fact]
    public void ATerminalDrawingTheMarkOneWideShowsTheBlank() =>
        Assert.Equal("অসমী য়া |ab", Played("অসমী য়া |ab", 1).Text());

    /// <summary>
    ///     Two such characters side by side, as in a field being typed in, where nothing left a blank: the second is
    ///     still placed in its own column rather than wherever the terminal left the first, so a terminal drawing them
    ///     wide loses what they cover and nothing past it.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TwoMarkedCharactersSideBySideAreEachPlacedInTheirOwnColumn(int drawnIn) =>
        Assert.Equal("b", Played("মীদী|ab", drawnIn).At(4));

    /// <summary>
    ///     An emoji newer than Terminal.Gui's tables, 🫪 as the painter leaves it, then <c>|ab</c>: a terminal that
    ///     knows it draws it two wide and one that doesn't draws it one, and the <c>b</c> stays at column four on both
    ///     — where it was laid out, rather than a post's right edge landing in the margin past it, and staying there.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WhatFollowsANewEmojiIsWhereTheRowPutItHoweverWideTheTerminalDrawsIt(int drawnIn) =>
        Assert.Equal("b", Played("\U0001FAEA |ab", drawnIn).At(4));

    /// <summary>
    ///     A row being written has the blanks past its last drawn cell written too, which nothing else would touch: a
    ///     cell some count put past the panel's edge is gone the next time its row is drawn, whatever put it there.
    /// </summary>
    [Fact]
    public void ACellStrayedPastTheEndOfARowIsClearedTheNextTimeItsRowIsWritten()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr("ab|");

        var output = new Recording();
        output.Write(buffer);

        var terminal = new Terminal(1);
        Play(output, terminal);
        terminal.Write(7, "x");

        buffer.Move(0, 0);
        buffer.AddStr("cd|");
        output.Write(buffer);
        Play(output, terminal);

        Assert.Equal("cd|", terminal.Text());
    }

    /// <summary>
    ///     Those blanks are a run of their own, placed by itself, dirty or not: a run reaching the terminal's last
    ///     column would wrap on a count out by one, where stopping short leaves the stray cell for them to clear.
    /// </summary>
    [Fact]
    public void TheBlanksPastTheEndOfARowAreARunOfTheirOwn()
    {
        var output = Written("ab|");

        Assert.Contains(3, output.Places);
        Assert.Equal("ab|", Terminal.Plain(output.Writes[0].Text));
    }

    /// <summary>
    ///     And nothing else is: a row with text to its right keeps it clean, so a picture beside what is being written
    ///     is not written over by the blanks between them.
    /// </summary>
    [Fact]
    public void WhatLiesBetweenARowsLastDrawnCellAndItsLastTextIsLeftAlone()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr("ab");
        buffer.Move(8, 0);
        buffer.AddStr("|");

        var output = new Recording();
        output.Write(buffer);
        output.Places.Clear();

        buffer.Move(0, 0);
        buffer.AddStr("cd");
        output.Write(buffer);

        Assert.DoesNotContain(output.Places, place => place is >= 2 and <= 8);
    }

    /// <summary>
    ///     A picture's box at the very end of a row, with nothing right of it, is not those blanks either: its cells
    ///     are the terminal's own colour, which Terminal.Gui never writes, and writing them would erase a sixel
    ///     (ADR-0016, ADR-0023).
    /// </summary>
    [Fact]
    public void APicturesBoxAtTheEndOfARowIsNotWrittenOver()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr("ab");
        buffer.CurrentAttribute = new TerminalAttribute(Color.None, Color.None);
        buffer.Move(8, 0);
        buffer.AddStr("    ");
        buffer.AddRasterImage(new RasterImageCommand
        {
            Id = "picture",
            Pixels = new Color[1, 1],
            EncodedSixel = "sixel",
            DestinationCells = new Rectangle(8, 0, 4, 1),
            IsDirty = false,
        });

        var output = new Recording();
        output.Write(buffer);

        Assert.All(output.Writes, write => Assert.True((write.Place ?? 0) + Terminal.Plain(write.Text).Length <= 8));
    }

    /// <summary>
    ///     The cell a wide character at the end of a row covers is not one of those blanks: writing it would erase it.
    /// </summary>
    [Fact]
    public void AWideCharacterAtTheEndOfARowKeepsItsSecondColumn()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr("ab");
        buffer.Move(Columns - 2, 0);
        buffer.AddStr("\U0001F633");

        var output = new Recording();
        output.Write(buffer);
        output.Places.Clear();

        buffer.Move(0, 0);
        buffer.AddStr("cd");
        output.Write(buffer);

        Assert.DoesNotContain(output.Places, place => place >= Columns - 2);
    }

    /// <summary>
    ///     Nor is the cell after anything a terminal may draw wider than Terminal.Gui counts it: held, as 🫪 and
    ///     <c>মী</c> are, or written apart, as ⚠ is. The blanks past that cell are still written.
    /// </summary>
    [Theory]
    [InlineData("\U0001FAEA")]
    [InlineData("মী")]
    [InlineData("\u26A0")]
    public void ACharacterAtTheEndOfARowKeepsTheCellAfterIt(string character)
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr("ab");
        buffer.Move(6, 0);
        buffer.AddStr(character);

        var output = new Recording();
        output.Write(buffer);
        output.Places.Clear();

        buffer.Move(0, 0);
        buffer.AddStr("cd");
        output.Write(buffer);

        Assert.DoesNotContain(7, output.Places);
        Assert.Contains(8, output.Places);
    }

    /// <summary>
    ///     An emoji sequence on a terminal that adds its parts up, six columns where it was counted two, covers what is
    ///     next and moves nothing past it: the row's edge stays at its column however the sequence is drawn.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void WhatLiesPastAnEmojiIsWhereTheRowPutItHoweverWideTheTerminalDrawsIt(int drawnIn) =>
        Assert.Equal("d", Played("\U0001F468\u200D\U0001F469\u200D\U0001F467|abcdef", drawnIn).At(6));

    /// <summary>A terminal drawing it as counted keeps the row whole, its second column covered by nothing.</summary>
    [Fact]
    public void ATerminalDrawingAnEmojiAsCountedKeepsTheRowWhole() =>
        Assert.Equal("\U0001F633|ab", Played("\U0001F633|ab", 2).Text());

    /// <summary>
    ///     Nothing is written into an emoji's second column but blanks, and those before it: what a terminal shows for
    ///     the frame's moment before the emoji is written over them is a gap, not a stand-in character.
    /// </summary>
    [Fact]
    public void AnEmojisSecondColumnIsWrittenAsABlankIfAtAll()
    {
        var writes = Written("\U0001F633|ab").Writes.Select(write => write.Text).ToList();
        var emoji = writes.FindIndex(text => text.Contains("\U0001F633"));

        Assert.True(emoji > 0);
        Assert.DoesNotContain(writes[..emoji], text => text.Contains('\uFFFD'));
        Assert.Contains(writes[..emoji], text => Terminal.Plain(text).TrimStart().StartsWith("|ab"));
    }

    /// <summary>
    ///     A rule and a scrollbar's thumb run the length of a row and every terminal agrees on them: one run.
    /// </summary>
    [Fact]
    public void BoxDrawingAndBlocksAreWrittenInOneRun() =>
        Assert.Equal("│─┃█▌▐░─│", Terminal.Plain(Written("│─┃█▌▐░─│").Writes[0].Text));

    /// <summary>
    ///     Wide emoji side by side are each placed: no two are one run, whose first miscount moves the rest.
    /// </summary>
    [Fact]
    public void WideEmojiSideBySideAreEachPlaced()
    {
        var output = Written("\U0001F44D\U0001F44D\U0001F44D|");

        Assert.Contains(2, output.Places);
        Assert.Contains(4, output.Places);
        Assert.DoesNotContain(output.Writes, write => write.Text.Split("\U0001F44D").Length > 2);
    }

    /// <summary>
    ///     However long a run of them, a frame takes the same few passes: one for everything else, two for every other
    ///     cell of a run, one for the blanks past the end. Each pass walks the frame, and a pass each cost a frame's
    ///     budget.
    /// </summary>
    [Fact]
    public void ARunOfSymbolsTakesTheSameFewPassesHoweverLong()
    {
        var output = Written(new string('\u265B', Columns));

        Assert.True(output.Places.Count(place => place == 0) <= 4);
    }

    /// <summary>
    ///     A row with no such character has its text written as Terminal.Gui writes it: one run, one place.
    /// </summary>
    [Fact]
    public void ARowWithNoMarkIsWrittenInOneRun()
    {
        var output = Written("Maria Ochoa");

        Assert.Equal([0, 11], output.Places.Distinct());
        Assert.Equal("Maria Ochoa", Terminal.Plain(output.Writes[0].Text));
    }

    /// <summary>
    ///     An application given the output writes every frame through it. Terminal.Gui offers no public way to hand an
    ///     application its own output, so this is what says a Terminal.Gui that has moved the one used has moved it.
    /// </summary>
    [Fact]
    public void AnApplicationGivenTheOutputWritesThroughIt()
    {
        using var application = Application.Create();

        HeldOutput.Into(application);
        application.Init();

        Assert.IsType<HeldOutput>(application.Driver!.GetOutput());
    }

    private static Terminal Played(string row, int drawnIn)
    {
        var terminal = new Terminal(drawnIn);

        Play(Written(row), terminal);

        return terminal;
    }

    /// <summary>
    ///     What <paramref name="output" /> has written since this last asked, played on <paramref name="terminal" />.
    /// </summary>
    private static void Play(Recording output, Terminal terminal)
    {
        foreach (var (place, text) in output.Writes)
        {
            terminal.Write(place, text);
        }

        output.Writes.Clear();
    }

    private static Recording Written(string row)
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        buffer.Move(0, 0);
        buffer.AddStr(row);

        var output = new Recording();
        output.Write(buffer);

        return output;
    }

    /// <summary>
    ///     The client's output, with what it writes kept: each place the cursor is put and each run of text after it.
    ///     Headless, Terminal.Gui writes neither to the terminal, so both are read off here.
    /// </summary>
    private sealed class Recording : HeldOutput
    {
        private int? _place;

        public List<(int? Place, string Text)> Writes { get; } = [];

        public List<int> Places { get; } = [];

        protected override bool SetCursorPositionImpl(int col, int row)
        {
            _place = col;
            Places.Add(col);

            return base.SetCursorPositionImpl(col, row);
        }

        protected override void Write(StringBuilder output)
        {
            Writes.Add((_place, output.ToString()));
            _place = null;
            base.Write(output);
        }
    }

    /// <summary>
    ///     One row of a terminal that draws a character carrying a spacing mark or an emoji <c>drawnIn</c> columns wide
    ///     and every other one column, and — as herdr and the xterm family do with a wide character — erases a wide one
    ///     when anything is written into any of its columns but by itself.
    /// </summary>
    private sealed partial class Terminal(int drawnIn)
    {
        private readonly string?[] _cells = Enumerable.Repeat<string?>(" ", Columns + 4).ToArray();

        private readonly int[] _owner = Enumerable.Range(0, Columns + 4).ToArray();

        private int _cursor;

        public void Write(int? place, string text)
        {
            _cursor = place ?? _cursor;

            var plain = Escapes().Replace(text, string.Empty);
            var characters = StringInfo.GetTextElementEnumerator(plain);

            while (characters.MoveNext())
            {
                var character = (string)characters.Current;
                var wide = Glyphs.MeasuredAlike(character) && !Glyphs.WrittenApart(character) ? 1 : drawnIn;

                for (var column = _cursor; column < _cursor + wide; column++)
                {
                    Erase(_owner[column]);
                }

                _cells[_cursor] = character;

                for (var column = _cursor + 1; column < _cursor + wide; column++)
                {
                    _cells[column] = null;
                    _owner[column] = _cursor;
                }

                _cursor += wide;
            }
        }

        /// <summary>
        ///     What is drawn in <paramref name="column" />: a character, a blank, or nothing, under a wide one.
        /// </summary>
        public string? At(int column) => _cells[column];

        /// <summary>The row read back as herdr reads one: each character once, trailing blanks dropped.</summary>
        public string Text() => string.Concat(_cells.Where(cell => cell is not null)).TrimEnd();

        private void Erase(int owner)
        {
            for (var column = owner; column < _cells.Length && (column == owner || _owner[column] == owner); column++)
            {
                _cells[column] = " ";
                _owner[column] = column;
            }
        }

        /// <summary><paramref name="text" /> with what moves the cursor or sets a colour taken out.</summary>
        public static string Plain(string text) => Escapes().Replace(text, string.Empty);

        [GeneratedRegex(@"\u001b(\[[0-9;?]*[A-Za-z]|\][^\u0007\u001b]*(\u0007|\u001b\\))")]
        private static partial Regex Escapes();
    }
}
