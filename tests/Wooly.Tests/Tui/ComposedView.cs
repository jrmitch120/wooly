using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     A <see cref="ComposeView" /> over a shell built by <see cref="AShell" />, drawn headless on a terminal the size of
///     the content viewport it is laid over — and nothing else of the shell's window: the compose screen's own rows are
///     painted behind it, as the content panel paints them, so a field can be read beside its header (#365).
/// </summary>
internal sealed class ComposedView : IDisposable
{
    /// <summary>The content panel's inside at an 80 × 24 terminal: what the rail, the edges and the status row leave.</summary>
    public const int Width = 58;

    public const int Height = 21;

    private ComposedView(IApplication application, Host host, ComposeView view, Wooly.Tui.Shell.Shell shell, AShell built)
    {
        Application = application;
        Window = host;
        View = view;
        Shell = shell;
        Built = built;
    }

    public IApplication Application { get; }

    /// <summary>What holds the view, in the window's place: it hears whatever key the view leaves.</summary>
    public Host Window { get; }

    public ComposeView View { get; }

    public Wooly.Tui.Shell.Shell Shell { get; }

    public AShell Built { get; }

    /// <summary>The compose screen on top, which every test here opens.</summary>
    public ComposeScreen Compose => Assert.IsType<ComposeScreen>(Shell.Screen);

    public ComposeEditor Editor => View.SubViews.OfType<ComposeEditor>().Single();

    public ComposeWarningField Warning => View.SubViews.OfType<ComposeWarningField>().Single();

    public ComposeToField To => View.SubViews.OfType<ComposeToField>().Single();

    public ComposeLangField Lang => View.SubViews.OfType<ComposeLangField>().Single();

    /// <summary>
    ///     A compose opened for <paramref name="opening" /> over the post <paramref name="built" />'s shell opens on,
    ///     with a view <paramref name="width" /> × <paramref name="height" /> built over it in
    ///     <paramref name="theme" /> — or, where <paramref name="opening" /> is none, the view over the shell as opened
    ///     and compose left for the test to open.
    /// </summary>
    public static async Task<ComposedView> Of(
        AShell? built = null,
        ComposeFor? opening = ComposeFor.Post,
        int width = Width,
        int height = Height,
        ITheme? theme = null)
    {
        built ??= new AShell();
        theme ??= Themes.Dark;

        var shell = await built.Opened();

        var application = Terminal.Gui.App.Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        var host = new Host();
        host.SetScheme(new Terminal.Gui.Drawing.Scheme(theme.For(Role.Body)));

        var backdrop = new PaintedView(
            theme,
            (columns, rows) => shell.Screen.Lines(new Drawing(columns, built.Clock.GetUtcNow(), Height: rows)))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
        };

        var view = new ComposeView(theme, shell)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        host.Add(backdrop, view);
        shell.Changed += backdrop.SetNeedsDraw;

        application.Begin(host);
        application.LayoutAndDraw(true);

        var composed = new ComposedView(application, host, view, shell, built);

        if (opening is { } purpose)
        {
            ComposeRows.Open(shell, purpose);
            composed.Redraw();
        }

        return composed;
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
    ///     A key as the terminal hands it over, through the application — so one nothing answers falls through to
    ///     Terminal.Gui's own navigation, as it would in the running TUI.
    /// </summary>
    public void PressThroughTheApplication(Key key)
    {
        Application.Keyboard.RaiseKeyDownEvent(key);
        Redraw();
    }

    /// <summary>Each letter of <paramref name="text" /> pressed in turn.</summary>
    public void Type(string text)
    {
        foreach (var letter in text)
        {
            Window.NewKeyDownEvent(new Key(letter));
        }

        Redraw();
    }

    public void Paste(string text)
    {
        Application.RaisePasteEvent(text);
        Redraw();
    }

    /// <summary>A left click on the cell, pressed and let go first as a terminal reports one.</summary>
    public void Click(int column, int row)
    {
        Point(column, row, MouseFlags.LeftButtonPressed);
        Point(column, row, MouseFlags.LeftButtonReleased);
        Point(column, row, MouseFlags.LeftButtonClicked);
    }

    /// <summary>A left click on the first cell of <paramref name="field" />.</summary>
    public void Click(View field)
    {
        var at = field.FrameToScreen();

        Click(at.X, at.Y);
    }

    /// <summary>A left click on the first row reading <paramref name="text" />, <paramref name="past" /> columns into it.</summary>
    public void ClickOn(string text, int past = 0)
    {
        var rows = Rows();
        var row = Array.FindIndex(rows, line => line.Contains(text, StringComparison.Ordinal));

        Assert.True(row >= 0, $"Nothing reads {text}:\n{string.Join('\n', rows)}");

        Click(rows[row].IndexOf(text, StringComparison.Ordinal) + past, row);
    }

    public void RightClick(int column, int row) => Point(column, row, MouseFlags.RightButtonClicked);

    public void Point(int column, int row, MouseFlags flags)
    {
        Application.Mouse.RaiseMouseEvent(new Mouse { ScreenPosition = new Point(column, row), Flags = flags });
        Redraw();
    }

    /// <summary>Every row, as the text drawn on it.</summary>
    public string[] Rows()
    {
        var cells = Application.Driver!.Contents!;

        return
        [
            .. Enumerable.Range(0, cells.GetLength(0)).Select(row => string.Concat(
                Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))),
        ];
    }

    /// <summary>What <paramref name="field" /> shows, as drawn on its row.</summary>
    public string Shown(View field)
    {
        var at = field.FrameToScreen();

        return Rows()[at.Y].Substring(at.X, at.Width);
    }

    public Attribute Cell(int row, int column) => Application.Driver!.Contents![row, column].Attribute!.Value;

    /// <summary>Every cell's attribute.</summary>
    public List<Attribute> Cells()
    {
        var cells = new List<Attribute>();

        foreach (var cell in Application.Driver!.Contents!)
        {
            cells.Add(cell.Attribute!.Value);
        }

        return cells;
    }

    /// <summary>The status row as the shell's window would draw it, 80 columns wide.</summary>
    public string Status() =>
        ChromeLines.Status(Shell.Keys, Shell.Notice, Shell.NoticeIsError, Shell.Asking, 80).Text;

    public void Dispose()
    {
        Window.Dispose();
        Application.Dispose();
    }

    /// <summary>The window's stand-in, which hears every key the view leaves and does nothing with it.</summary>
    internal sealed class Host : Window
    {
        public Host() => BorderStyle = Terminal.Gui.Drawing.LineStyle.None;

        /// <summary>Every key that reached past the view.</summary>
        public List<Key> Reached { get; } = [];

        protected override bool OnKeyDown(Key key)
        {
            Reached.Add(key);

            return base.OnKeyDown(key);
        }
    }
}
