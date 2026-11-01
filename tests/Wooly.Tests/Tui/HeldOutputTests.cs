using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Wooly.Tui.Rendering;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What reaches a terminal for a row holding a character carrying a spacing mark (ADR-0031): written by the
///     client's own output, and played on a terminal that draws such a character one, two or three columns wide — as
///     WezTerm, herdr and GNU screen were measured to. Whatever the width, nothing after it on the row moves. And a
///     frame reaching the terminal whole wherever the caret is (#553).
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

    /// <summary>A row with no such character is written as Terminal.Gui writes it: one run, one place.</summary>
    [Fact]
    public void ARowWithNoMarkIsWrittenInOneRun()
    {
        var output = Written("Maria Ochoa");

        Assert.Single(output.Places);
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

    /// <summary>
    ///     A frame whose first changed cell on a row is the one the caret sits on is written whole, rows after it
    ///     included (#553). Terminal.Gui's own writer takes the caret's cell for a cursor it need not move and gives up
    ///     on the rest of the frame there, so nothing after it reached the terminal until the caret moved.
    /// </summary>
    [Fact]
    public void AFrameChangingTheCellUnderTheCaretIsWrittenWhole()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 2);
        var output = new Recording();
        output.Write(buffer);

        buffer.Move(5, 0);
        buffer.AddStr("x");
        buffer.Move(0, 1);
        buffer.AddStr("yz");
        output.SetCursor(new Cursor { Position = new Point(5, 0) });
        output.Writes.Clear();
        output.Write(buffer);

        Assert.Equal("xyz", Drawn(output));
    }

    /// <summary>
    ///     A link drawn on the caret's row is closed before anything else is written (#553). Terminal.Gui links every
    ///     URL it draws; cut short at the caret's cell, its writer left the link open, and the blanks written next were
    ///     inside it — a line a terminal underlines when the pointer passes over.
    /// </summary>
    [Fact]
    public void ALinkOnTheCaretsRowIsClosed()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(16, 1);
        var output = new Recording();
        output.Write(buffer);

        buffer.Move(0, 0);
        buffer.AddStr("http://a.io");
        buffer.Contents![0, 11].IsDirty = false;
        buffer.Move(13, 0);
        buffer.AddStr("x");
        output.SetCursor(new Cursor { Position = new Point(13, 0) });
        output.Writes.Clear();
        output.Write(buffer);

        Assert.False(LinkOpen(output));
    }

    /// <summary>
    ///     Every frame leaves no link open behind it, whatever Terminal.Gui wrote (#553): a link is closed at the end
    ///     of each frame, so nothing written after it — the next frame, or a terminal's own erasing — can be taken
    ///     into one.
    /// </summary>
    [Fact]
    public void AFrameLeavesNoLinkOpen()
    {
        var buffer = new OutputBufferImpl();
        buffer.SetSize(Columns, 1);
        var output = new Recording();

        output.Write(buffer);

        Assert.EndsWith(EscSeqUtils.OSC_EndHyperlink(), string.Concat(output.Writes.Select(write => write.Text)));
    }

    private static Terminal Played(string row, int drawnIn)
    {
        var terminal = new Terminal(drawnIn);

        foreach (var (place, text) in Written(row).Writes)
        {
            terminal.Write(place, text);
        }

        return terminal;
    }

    /// <summary>Whether the last hyperlink sequence <paramref name="output" /> wrote opened a link rather than closed one.</summary>
    private static bool LinkOpen(Recording output) =>
        Links().Matches(string.Concat(output.Writes.Select(write => write.Text))) is { Count: > 0 } links
        && links[^1].Groups["url"].Length > 0;

    /// <summary>An OSC 8 hyperlink sequence: a link's address opens it, an empty one closes it.</summary>
    [GeneratedRegex(@"\u001b\]8;[^;\u0007\u001b]*;(?<url>[^\u0007\u001b]*)")]
    private static partial Regex Links();

    /// <summary>Every escape sequence a terminal reads and draws nothing for.</summary>
    [GeneratedRegex(@"\u001b(\[[0-9;?]*[A-Za-z]|\][^\u0007\u001b]*(\u0007|\u001b\\))")]
    private static partial Regex Escapes();

    /// <summary>The text <paramref name="output" /> wrote, without its escapes or blanks.</summary>
    private static string Drawn(Recording output) =>
        Escapes().Replace(string.Concat(output.Writes.Select(write => write.Text)), string.Empty).Replace(" ", string.Empty);

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
    ///     One row of a terminal that draws a character carrying a spacing mark <c>drawnIn</c> columns wide and every
    ///     other one column, and — as herdr and the xterm family do with a wide character — erases a wide one when
    ///     anything is written into any of its columns but by itself.
    /// </summary>
    private sealed class Terminal(int drawnIn)
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
                var wide = Glyphs.MeasuredAlike(character) ? 1 : drawnIn;

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

    }
}
