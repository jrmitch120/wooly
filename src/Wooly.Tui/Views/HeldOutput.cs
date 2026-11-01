using System.Drawing;
using System.Reflection;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Views;

/// <summary>
///     The terminal output, writing a character carrying a spacing mark after everything else on its row and at a place
///     of its own (ADR-0031, #434).
/// </summary>
/// <remarks>
///     Terminal.Gui writes a row as one run from a single place, trusting each character to take the columns it counts
///     for it — and no terminal draws such a character in the one column Terminal.Gui counts: WezTerm draws it in one,
///     herdr in two, GNU screen in two or more. Every cell after it lands wherever the terminal's count put it. So each
///     frame is written in passes: first every cell but those, each run breaking where one was and the next placed
///     where Terminal.Gui put it; then those, each placed itself. A terminal drawing one two wide covers the blank
///     the painter left after it (<see cref="HeldText" />), and writing it last means nothing written afterwards
///     lands on its second column and erases it, as a wide character is erased. Two side by side — a field being
///     typed in leaves no blank — go in separate passes, so that each is placed rather than written wherever the
///     first left off.
///     <para>
///         Handed to the application as its <c>ansi</c> driver's output (<see cref="Into" />). A frame with no such
///         character is written as Terminal.Gui writes it, in one pass, but for three things (#553): a sixel is sent
///         over cells first erased (<see cref="EraseUnderPictures" />); the frame is never cut short where its first
///         change on a row is the caret's cell, as Terminal.Gui's own writer cuts it
///         (<see cref="SetCursorPositionImpl" />); and it ends by closing any hyperlink left open. Not sealed, so that
///         a test can read what it writes at the terminal's edge, which headless Terminal.Gui writes nowhere.
///     </para>
/// </remarks>
internal class HeldOutput : AnsiOutput
{
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

        var held = Hold(buffer);

        base.Write(buffer);

        // Each pass writes the held cells whose left neighbour is not being written with them, so no two of them are
        // ever one run.
        while (held.Count > 0)
        {
            var pass = held.Where(cell => !held.Contains((cell.Row, cell.Column - 1))).ToHashSet();

            foreach (var (row, column) in pass)
            {
                buffer.Contents![row, column].IsDirty = true;
                buffer.DirtyLines[row] = true;
            }

            held.ExceptWith(pass);
            base.Write(buffer);
        }

        // No link is left open past a frame, whatever Terminal.Gui wrote (#553). It links every URL it draws, and a
        // link it failed to close takes in whatever is written next — blanks a terminal then underlines on hover.
        Write(new StringBuilder(EscSeqUtils.OSC_EndHyperlink()));
    }

    /// <summary>
    ///     Erases, at the terminal, every cell a sixel this frame sends is drawn over — just before Terminal.Gui sends it
    ///     (#553).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui never writes the blank cells under a sixel, since text written there would erase the picture;
    ///     and a terminal draws a sixel over the text in its cells without erasing it. So the text the screen before
    ///     had there stayed under the picture, out of sight — a URL included, which Windows Terminal finds by itself
    ///     and takes for a link: hovered, it underlined the row out past the picture and offered to open it, and the
    ///     link it had found outlived the screen. Erased first, the cells hold nothing but blanks for the picture to
    ///     be drawn over. Only a picture sent this frame, as Terminal.Gui decides it: one already on the screen and
    ///     not sent again would be taken off with the text. Any link left open is closed first and the colours reset,
    ///     so that the blanks are plain ones; on a Kitty terminal a picture is placed under the text, and nothing is
    ///     erased.
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
                erase ??= new StringBuilder(EscSeqUtils.OSC_EndHyperlink()).Append(EscSeqUtils.CSI_ResetAttributes);

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

        return parts.Select(part => Rectangle.Intersect(cells, part)).Where(part => part is { Width: > 0, Height: > 0 });
    }

    /// <summary>
    ///     Puts the cursor at <paramref name="col" /> of <paramref name="row" />, always, and says it did (#553).
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui 2.5.0's <see cref="AnsiOutput" /> moves nothing and answers <see langword="false" /> where the place
    ///     asked for is the caret's — a move it takes to be unneeded — and its frame writer reads that answer as a
    ///     failure and gives up on the rest of the frame: every row after the caret's cell was left unwritten until the
    ///     caret moved, and a hyperlink it had opened on that row was never closed, so a terminal underlined the blanks
    ///     written later inside it. The terminal's cursor is wherever the last write left it, not at the caret, so the
    ///     move is made here instead — at the row as given, since the TUI always runs full screen and an inline
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
    ///     Every cell waiting to be written that holds a character carrying a spacing mark, set aside — marked as
    ///     written, so that Terminal.Gui's pass over the rest breaks its run there.
    /// </summary>
    private static HashSet<(int Row, int Column)> Hold(IOutputBuffer buffer)
    {
        var held = new HashSet<(int Row, int Column)>();
        var cells = buffer.Contents;

        if (cells is null)
        {
            return held;
        }

        for (var row = 0; row < cells.GetLength(0) && row < buffer.DirtyLines.Length; row++)
        {
            if (!buffer.DirtyLines[row])
            {
                continue;
            }

            for (var column = 0; column < cells.GetLength(1); column++)
            {
                if (cells[row, column].IsDirty && !Glyphs.MeasuredAlike(cells[row, column].Grapheme))
                {
                    cells[row, column].IsDirty = false;
                    held.Add((row, column));
                }
            }
        }

        return held;
    }
}
