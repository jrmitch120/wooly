using Terminal.Gui.App;
using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
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
    public static async Task<DrawnShell> Of(
        int width,
        int height,
        ITheme theme,
        AShell? built = null,
        bool launch = false)
    {
        built ??= new AShell();

        var shell = launch ? await built.Launched() : await built.Opened();

        var application = Terminal.Gui.App.Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        var window = new ShellWindow(shell, theme, built.Clock, () => { }, FakePictures.DrawingNothing());

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
