using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     The whole shell drawn headless on a terminal of a given size, and read back off its cells — for the tests whose
///     promise is about what reaches the terminal rather than which role a line names.
/// </summary>
internal sealed class DrawnShell : IDisposable
{
    private DrawnShell(IApplication application, ShellWindow window, Wooly.Tui.Shell.Shell shell, AShell built)
    {
        Application = application;
        Window = window;
        Shell = shell;
        Built = built;
    }

    public IApplication Application { get; }

    public ShellWindow Window { get; }

    public Wooly.Tui.Shell.Shell Shell { get; }

    public AShell Built { get; }

    /// <summary>The content panel's view.</summary>
    public PaintedView Content =>
        Window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

    /// <summary>
    ///     The shell drawn once on a <paramref name="width" />×<paramref name="height" /> terminal in
    ///     <paramref name="theme" />, opened on its first timeline — or launched, where <paramref name="launch" /> says
    ///     so, for a shell with nobody to act as.
    /// </summary>
    /// <param name="pictures">A terminal that draws pictures, where a test is about them; one that draws none if not.</param>
    /// <param name="drawsPictures">
    ///     Whether the headless terminal says it draws sixel, which is what puts a picture's box on screen at all — a
    ///     box on a terminal drawing neither protocol is never shown.
    /// </param>
    public static async Task<DrawnShell> Of(
        int width,
        int height,
        ITheme theme,
        AShell? built = null,
        bool launch = false,
        IPictures? pictures = null,
        bool drawsPictures = false)
    {
        built ??= new AShell();

        var shell = launch ? await built.Launched() : await built.Opened();

        var application = Terminal.Gui.App.Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        if (drawsPictures)
        {
            application.Driver.SetSixelSupport(new Terminal.Gui.Drawing.SixelSupportResult { IsSupported = true });
        }

        var window = new ShellWindow(shell, theme, built.Clock, () => { }, pictures ?? FakePictures.DrawingNothing());

        application.Begin(window);
        application.LayoutAndDraw(true);

        return new DrawnShell(application, window, shell, built);
    }

    public void Redraw() => Application.LayoutAndDraw(true);

    public void Press(Key key)
    {
        Window.NewKeyDownEvent(key);
        Redraw();
    }

    /// <summary>A left click on the cell at <paramref name="column" />, <paramref name="row" />.</summary>
    public void Click(int column, int row) => Point(column, row, MouseFlags.LeftButtonClicked);

    /// <summary>A double click on the cell, as Terminal.Gui reports one once it has counted the clicks.</summary>
    public void DoubleClick(int column, int row) => Point(column, row, MouseFlags.LeftButtonDoubleClicked);

    /// <summary>One notch of the wheel over the cell: down, towards the foot of the page, or up.</summary>
    public void Wheel(int column, int row, bool down = true) =>
        Point(column, row, down ? MouseFlags.WheeledDown : MouseFlags.WheeledUp);

    /// <summary>
    ///     A mouse event at a cell of the terminal, raised through the application the way a terminal's report is — so
    ///     it reaches whichever view is under the pointer and bubbles from there, which is what makes a wheel over the
    ///     compose editor the editor's and a wheel over the content the window's.
    /// </summary>
    private void Point(int column, int row, MouseFlags flags)
    {
        Application.Mouse.RaiseMouseEvent(new Mouse { ScreenPosition = new Point(column, row), Flags = flags });
        Redraw();
    }

    /// <summary>Every row, as the text drawn on it.</summary>
    public string[] Rows() => Rows(Application.Driver!.Contents!.GetLength(0), Application.Driver.Contents.GetLength(1));

    /// <summary>The rail's columns of every row above the status row.</summary>
    public string[] Rail() => Rows(Application.Driver!.Contents!.GetLength(0) - 1, RailLines.Width);

    public Attribute Cell(int row, int column) => Application.Driver!.Contents![row, column].Attribute!.Value;

    /// <summary>Every cell's attribute, row by row.</summary>
    public List<Attribute> Cells()
    {
        var cells = new List<Attribute>();

        foreach (var cell in Application.Driver!.Contents!)
        {
            cells.Add(cell.Attribute!.Value);
        }

        return cells;
    }

    /// <summary>The frame as the bytes a terminal would be sent.</summary>
    public string Ansi() => Application.Driver!.ToAnsi();

    public void Dispose()
    {
        Window.Dispose();
        Application.Dispose();
    }

    private string[] Rows(int count, int columns)
    {
        var cells = Application.Driver!.Contents!;

        return
        [
            .. Enumerable.Range(0, count).Select(row => string.Concat(
                Enumerable.Range(0, columns).Select(column => cells[row, column].Grapheme))),
        ];
    }
}
