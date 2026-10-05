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
    /// <param name="kittyImages">
    ///     The Kitty terminal's image store, where a test is about what it is sent; one nobody reads if not.
    /// </param>
    /// <param name="drawsPlaceholders">
    ///     Whether the terminal is known by name to draw Kitty's placeholders, which is what draws a picture as
    ///     placeholder cells rather than through a box (ADR-0022).
    /// </param>
    /// <param name="answersKitty">
    ///     Whether the headless terminal answers that it speaks the Kitty graphics protocol — which, on a terminal not
    ///     known by name, still draws through a box.
    /// </param>
    /// <param name="encoding">Where a picture is encoded for a Kitty terminal, where a test is about when; on the spot if not.</param>
    /// <param name="sixels">Where a sixel terminal's pictures are encoded and kept, where a test is about which; the window's own if not.</param>
    public static async Task<DrawnShell> Of(
        int width,
        int height,
        ITheme theme,
        AShell? built = null,
        bool launch = false,
        IPictures? pictures = null,
        bool drawsPictures = false,
        FakeTerminalImages? kittyImages = null,
        bool drawsPlaceholders = false,
        bool answersKitty = false,
        Action<Action>? encoding = null,
        SixelPictures? sixels = null)
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

        if (answersKitty)
        {
            application.Driver.SetKittyGraphicsSupport(
                new Terminal.Gui.Drawing.KittyGraphicsSupportResult { IsSupported = true });
        }

        // Encoded on the spot rather than off the UI thread, so that a picture is sent on the frame that first wants it.
        var placeholders = new Placeholders(
            kittyImages ?? new FakeTerminalImages(),
            drawsPlaceholders,
            () => { },
            encoding ?? (work => work()));

        var window = new ShellWindow(
            shell,
            theme,
            built.Clock,
            () => { },
            pictures ?? FakePictures.DrawingNothing(),
            placeholders: placeholders,
            sixels: sixels);

        application.Begin(window);
        application.LayoutAndDraw(true);

        return new DrawnShell(application, window, shell, built);
    }

    public void Redraw() => Application.LayoutAndDraw(true);

    /// <summary>Whatever was asked of the instance answered, and drawn.</summary>
    public void Settle()
    {
        Built.Host.Drain();
        Redraw();
    }

    public void Press(Key key)
    {
        Window.NewKeyDownEvent(key);
        Redraw();
    }

    /// <summary>
    ///     A key as the terminal hands it over, through the application rather than straight to the window — so that one
    ///     nothing answers falls through to Terminal.Gui's own navigation, as it would in the running TUI.
    /// </summary>
    public void PressThroughTheApplication(Key key)
    {
        Application.Keyboard.RaiseKeyDownEvent(key);
        Redraw();
    }

    /// <summary>A left click on the cell at <paramref name="column" />, <paramref name="row" />.</summary>
    public void Click(int column, int row) => Point(column, row, MouseFlags.LeftButtonClicked);

    /// <summary>
    ///     A double click on the cell, as Terminal.Gui reports one: the first click on its own the moment it is let go,
    ///     and then the pair once the second is.
    /// </summary>
    public void DoubleClick(int column, int row)
    {
        Click(column, row);
        Point(column, row, MouseFlags.LeftButtonDoubleClicked);
    }

    /// <summary>
    ///     Right clicks on the cell in quick succession, as Terminal.Gui reports them: one event as each is let go — a
    ///     click, then a double click, then a triple click for the third and every one after it.
    /// </summary>
    public void RightClick(int column, int row, int times = 1)
    {
        for (var at = 1; at <= times; at++)
        {
            Point(column, row, at switch
            {
                1 => MouseFlags.RightButtonClicked,
                2 => MouseFlags.RightButtonDoubleClicked,
                _ => MouseFlags.RightButtonTripleClicked,
            });
        }
    }

    /// <summary>One notch of the wheel over the cell: down, towards the foot of the page, or up.</summary>
    public void Wheel(int column, int row, bool down = true) =>
        Point(column, row, down ? MouseFlags.WheeledDown : MouseFlags.WheeledUp);

    /// <summary>
    ///     One notch of the wheel over the cell, handled and not yet drawn — as notches arriving while a slow frame is
    ///     being drawn are, before the next one.
    /// </summary>
    public void WheelUndrawn(int column, int row) =>
        Application.Mouse.RaiseMouseEvent(
            new Mouse { ScreenPosition = new Point(column, row), Flags = MouseFlags.WheeledDown });

    /// <summary>One sideways notch over the cell, as a trackpad sends when a finger drifts: right, or left.</summary>
    public void WheelSideways(int column, int row, bool right = true) =>
        Point(column, row, right ? MouseFlags.WheeledRight : MouseFlags.WheeledLeft);

    /// <summary>
    ///     A mouse event at a cell of the terminal, raised through the application the way a terminal's report is — so
    ///     it reaches whichever view is under the pointer and bubbles from there, which is what makes a wheel over the
    ///     compose editor the editor's and a wheel over the content the window's.
    /// </summary>
    public void Point(int column, int row, MouseFlags flags)
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
