using System.Drawing;
using System.Reflection;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Text;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Views;

/// <summary>
///     The terminal output, writing a character carrying a spacing mark, one newer than the tables, or a symbol or
///     emoji, after everything else on its row and at a place of its own, and the blanks past the end of a row last of
///     all (ADR-0031, #434).
/// </summary>
/// <remarks>
///     Terminal.Gui writes a row as one run from a single place, trusting each character to take the columns it counts
///     for it — and no terminal draws a spacing mark in the one column Terminal.Gui counts: WezTerm draws it in one,
///     herdr in two, GNU screen in two or more. Every cell after it lands wherever the terminal's count put it. So each
///     frame is written in passes: first every cell but those, each run breaking where one was and the next placed
///     where Terminal.Gui put it; then those, each placed itself. A terminal drawing a held character two wide covers
///     the blank the painter left after it (<see cref="HeldText" />), and writing it last means nothing written
///     afterwards lands on its second column and erases it, as a wide character is erased. A symbol or emoji has no
///     blank, and one drawn wider than counted covers what is next. Side by side — a field being typed in leaves no
///     blank, and a run of emoji has none to leave — every other one goes in a pass of its own, so that each is placed
///     rather than written wherever the last left off, and a run of any length takes two passes.
///     <para>
///         Handed to the application as its <c>ansi</c> driver's output (<see cref="Into" />). A row with none of
///         those is written as Terminal.Gui writes it but for the blanks past its end. And for four things more
///         (#553): a sixel is sent over cells first erased (<see cref="EraseUnderPictures" />); a frame is never cut
///         short where its first change on a row is the caret's cell, as Terminal.Gui's own writer cuts it
///         (<see cref="SetCursorPositionImpl" />); it ends by closing any hyperlink left open; and between frames,
///         a cursor no different from the one last written is not written again (<see cref="IOutput.SetCursor" />).
///         Not sealed, so that a test can read what it writes at the terminal's edge, which headless Terminal.Gui
///         writes nowhere.
///     </para>
/// </remarks>
internal class HeldOutput : AnsiOutput, IOutput
{
    /// <summary>The cursor last written since the last frame, or none.</summary>
    private Cursor? _writtenCursor;

    /// <summary>
    ///     Makes this the output <paramref name="application" /> writes through, once it is initialised — where it was
    ///     to be Terminal.Gui's <c>ansi</c> driver anyway, its default on every platform.
    /// </summary>
    /// <remarks>
    ///     Through the field Terminal.Gui's application reads a component factory from, because nothing public sets
    ///     one: its driver registry names a factory for each driver but the application makes its own by name. A
    ///     Terminal.Gui without the field leaves the application writing as Terminal.Gui does, which is the row moving
    ///     as it did before and nothing worse; <c>HeldOutputTests</c> is what says so. Called just before
    ///     <see cref="IApplication.Init" /> is, with no driver named: a factory set here is used whatever driver is
    ///     named or forced after it.
    /// </remarks>
    public static void Into(IApplication application)
    {
        if (application.ForceDriver is { Length: > 0 } forced
            && !string.Equals(forced, DriverRegistry.Names.ANSI, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var factory = application.GetType()
            .GetField("_componentFactory", BindingFlags.Instance | BindingFlags.NonPublic);

        if (factory?.FieldType.IsAssignableFrom(typeof(AnsiComponentFactory)) == true)
        {
            factory.SetValue(application, new AnsiComponentFactory(output: new HeldOutput()));
        }
    }

    /// <inheritdoc />
    public override void Write(IOutputBuffer buffer)
    {
        EraseUnderPictures(buffer);

        var blanks = PastTheEnd(buffer);
        var (first, second) = Apart(buffer);

        base.Write(buffer);

        WriteAlone(buffer, first);
        WriteAlone(buffer, second);
        WriteAlone(buffer, blanks);

        // No link is left open past a frame, whatever Terminal.Gui wrote (#553). It links every URL it draws, and a
        // link it failed to close takes in whatever is written next — blanks a terminal then underlines on hover.
        Write(new StringBuilder(EscSeqUtils.OSC_EndHyperlink()));

        // A frame moves the terminal's cursor, so whatever cursor comes next is written, even one the same as before.
        _writtenCursor = null;
    }

    /// <summary>
    ///     Writes <paramref name="cursor" />, unless it is the very one last written since the last frame (#553).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui 2.5.0 sets the cursor on every pass of its main loop, 25 a second, and never marks it as
    ///     needing no update; each time, its <see cref="AnsiOutput" /> writes the cursor shown or hidden again, and
    ///     put again. So the terminal was sent a few bytes every 40ms for as long as wooly ran, whatever was on the
    ///     screen. Windows Terminal finds URLs in the screen's text by itself, and looks again only once output has
    ///     been quiet for 100ms — which it never was. A URL it had found once stayed found: hovered, it underlined
    ///     the cells where that URL had been, on whatever screen was there now. Nothing is written for a cursor
    ///     already written, so between frames the terminal hears nothing.
    /// </remarks>
    void IOutput.SetCursor(Cursor cursor)
    {
        if (cursor == _writtenCursor)
        {
            return;
        }

        SetCursor(cursor);
        _writtenCursor = cursor;
    }

    /// <summary>The rows of <paramref name="buffer" /> with something waiting to be written.</summary>
    private static IEnumerable<int> RowsBeingWritten(IOutputBuffer buffer)
    {
        for (var row = 0; row < buffer.Contents!.GetLength(0) && row < buffer.DirtyLines.Length; row++)
        {
            if (buffer.DirtyLines[row])
            {
                yield return row;
            }
        }
    }

    /// <summary>
    ///     The blanks past the last thing on each row being written, taken out of Terminal.Gui's first pass over the
    ///     rest and written after it, past the panel's edge and to the end of the terminal's row.
    /// </summary>
    /// <remarks>
    ///     Those cells stay blank from frame to frame, so Terminal.Gui never writes them again. A cell some terminal's
    ///     count put there would stay when the row had moved on, which is how a panel's edge and a scrollbar's thumb
    ///     came to be left behind in the margin. Whatever the next such text is, this clears it in the frame that drew
    ///     it. A pass of their own, because a run carrying on through them to the terminal's last column would wrap on
    ///     a count out by one, where stopping short leaves a stray cell here to clear.
    ///     <para>
    ///         Only what is right of everything else on the row: anything further left is drawn by a view that knows it
    ///         is there. The cell after a wide, held or written-apart character is not a blank to write, since a
    ///         terminal may have drawn that character over it and writing it would erase the character. Nor are a
    ///         picture's blanks, which are the terminal's own colour and which Terminal.Gui will not write in any pass
    ///         (ADR-0016, ADR-0023).
    ///     </para>
    /// </remarks>
    private static List<(int Row, int Column)> PastTheEnd(IOutputBuffer buffer)
    {
        var blanks = new List<(int Row, int Column)>();
        var cells = buffer.Contents;

        if (cells is null)
        {
            return blanks;
        }

        foreach (var row in RowsBeingWritten(buffer))
        {
            for (var column = cells.GetLength(1) - 1; column >= 0; column--)
            {
                if (cells[row, column].Grapheme != " "
                    || (column > 0 && CoversTheNextCell(cells[row, column - 1].Grapheme)))
                {
                    break;
                }

                cells[row, column].IsDirty = false;
                blanks.Add((row, column));
            }
        }

        return blanks;
    }

    /// <summary>Whether a terminal may have drawn <paramref name="character" /> over the cell after its own.</summary>
    private static bool CoversTheNextCell(string character) =>
        Glyphs.Columns(character) > 1 || Glyphs.WrittenApart(character);

    /// <summary>
    ///     Erases, at the terminal, every cell a sixel this frame sends is drawn over — just before Terminal.Gui sends
    ///     it (#553).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui never writes the blank cells under a sixel, since text written there would erase the picture;
    ///     and a terminal draws a sixel over the text in its cells without erasing it. So the text the screen before
    ///     had there stayed under the picture, out of sight — a URL included, which Windows Terminal finds by itself
    ///     and takes for a link: hovered, it underlined the row out past the picture and offered to open it, and the
    ///     link it had found outlived the screen. Erased first, the cells hold nothing but blanks for the picture to
    ///     be drawn over. Only a picture sent this frame, as Terminal.Gui decides it: one already on the screen and
    ///     not sent again would be taken off with the text. Any link left open is closed first and the colours reset,
    ///     so that the blanks are plain ones, but not the text style, which Terminal.Gui would not be told of. On a
    ///     Kitty terminal a picture is placed under the text, and nothing is erased.
    /// </remarks>
    private void EraseUnderPictures(IOutputBuffer buffer)
    {
        if (IsLegacyConsole || UseKittyGraphics)
        {
            return;
        }

        var screen = new Rectangle(0, 0, buffer.Cols, buffer.Rows);
        StringBuilder? erase = null;

        foreach (var picture in buffer.GetRasterImages())
        {
            if (picture.Pixels is null || (!picture.IsDirty && !picture.AlwaysRender))
            {
                continue;
            }

            foreach (var part in CellsUnder(picture, screen))
            {
                // The colours alone, and not the text style: Terminal.Gui writes a style only where it differs from
                // the one it last wrote, frames apart, so a reset it was never told of would draw a reversed run plain.
                erase ??= new StringBuilder(EscSeqUtils.OSC_EndHyperlink()).Append(EscSeqUtils.CSI).Append("39;49m");

                for (var row = part.Top; row < part.Bottom; row++)
                {
                    erase.Append(EscSeqUtils.CSI_SetCursorPosition(row + 1, part.Left + 1))
                        .Append(EscSeqUtils.CSI)
                        .Append(part.Width)
                        .Append('X');
                }
            }
        }

        if (erase is not null)
        {
            Write(erase);
        }
    }

    /// <summary>
    ///     The parts of the screen <paramref name="picture" /> is drawn over: its cells, inside what it was let draw in
    ///     and on the screen — as Terminal.Gui cuts it.
    /// </summary>
    private static IEnumerable<Rectangle> CellsUnder(RasterImageCommand picture, Rectangle screen)
    {
        var cells = Rectangle.Intersect(picture.DestinationCells, screen);
        var parts = picture.Clip is { } clip ? clip.GetRectangles() : [cells];

        return parts
            .Select(part => Rectangle.Intersect(cells, part))
            .Where(part => part is { Width: > 0, Height: > 0 });
    }

    /// <summary>
    ///     Puts the cursor at <paramref name="col" /> of <paramref name="row" />, always, and says it did (#553).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui 2.5.0's <see cref="AnsiOutput" /> moves nothing and answers <see langword="false" /> where the
    ///     place asked for is the caret's — a move it takes to be unneeded — and its frame writer reads that answer as
    ///     a failure and gives up on the rest of the frame: every row after the caret's cell was left unwritten until
    ///     the caret moved, and a hyperlink it had opened on that row was never closed, so a terminal underlined the
    ///     blanks written later inside it. The terminal's cursor is wherever the last write left it, not at the caret,
    ///     so the move is made here instead — at the row as given, since the TUI always runs full screen and an inline
    ///     region's offset is never added.
    /// </remarks>
    protected override bool SetCursorPositionImpl(int col, int row)
    {
        if (!base.SetCursorPositionImpl(col, row))
        {
            Write(new StringBuilder(EscSeqUtils.CSI_SetCursorPosition(row + 1, col + 1)));
        }

        return true;
    }

    /// <summary>
    ///     Every cell waiting to be written that holds a character <see cref="Glyphs.MeasuredAlike" /> says is not, or
    ///     one <see cref="Glyphs.WrittenApart" /> says to write apart, taken out of Terminal.Gui's first pass so that
    ///     its run breaks there, and shared between two passes so that no two side by side are ever one run.
    /// </summary>
    /// <remarks>
    ///     Side by side counts a wide character's second column as its own: Terminal.Gui steps over it, so the
    ///     character after it would otherwise carry straight on from it.
    /// </remarks>
    private static (List<(int Row, int Column)> First, List<(int Row, int Column)> Second) Apart(IOutputBuffer buffer)
    {
        var first = new List<(int Row, int Column)>();
        var second = new List<(int Row, int Column)>();
        var cells = buffer.Contents;

        if (cells is null)
        {
            return (first, second);
        }

        foreach (var row in RowsBeingWritten(buffer))
        {
            var lastEnded = -1;
            var lastWasFirst = false;

            for (var column = 0; column < cells.GetLength(1); column++)
            {
                var grapheme = cells[row, column].Grapheme;

                if (!cells[row, column].IsDirty || (Glyphs.MeasuredAlike(grapheme) && !Glyphs.WrittenApart(grapheme)))
                {
                    continue;
                }

                var isFirst = column != lastEnded || !lastWasFirst;

                (isFirst ? first : second).Add((row, column));
                cells[row, column].IsDirty = false;
                lastEnded = column + (grapheme.GetColumns() > 1 ? 2 : 1);
                lastWasFirst = isFirst;
            }
        }

        return (first, second);
    }

    /// <summary>Has Terminal.Gui write <paramref name="cells" />, and nothing else, in a pass of their own.</summary>
    private void WriteAlone(IOutputBuffer buffer, List<(int Row, int Column)> cells)
    {
        if (cells.Count == 0)
        {
            return;
        }

        foreach (var (row, column) in cells)
        {
            buffer.Contents![row, column].IsDirty = true;
            buffer.DirtyLines[row] = true;
        }

        base.Write(buffer);
    }
}
