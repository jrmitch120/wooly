// PROTOTYPE — throwaway. Question: what should the Wooly TUI look like, if we started again?
// Four structurally different layouts on the same fixture data, switchable with < and > (or by clicking the arrows on
// the floating bar). Start on a given one with --variant B. Nothing is persisted and nothing talks to an instance.
//
//   A  Panels         — lazygit / k9s: stacked, numbered, bordered panels with a big detail pane
//   B  Mail client    — aerc / neomutt / helix: tab bar, dense column table, split preview, modal status line
//   C  Zen reader     — glow / helix / posting: one centred column, no chrome, command palette and which-key popups
//   D  Miller columns — yazi / ranger: places › posts › thread as three sliding columns
//
// Run: dotnet run --project src/Wooly.Tui.Prototype [-- --variant B]

using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Prototype;

Variant[] variants = [new PanelsVariant(), new MailVariant(), new ZenVariant(), new ColumnsVariant()];

var start = 0;
var named = Array.IndexOf(args, "--variant");

if (named >= 0 && named + 1 < args.Length)
{
    start = Math.Max(0, Array.FindIndex(variants, v => v.Key.Equals(args[named + 1], StringComparison.OrdinalIgnoreCase)));
}

// --dump A 120x40 [keys]: paint a variant as plain text after typing keys, for checking layout without a terminal.
var dump = Array.IndexOf(args, "--dump");

if (dump >= 0)
{
    var v = variants.First(x => x.Key.Equals(args[dump + 1], StringComparison.OrdinalIgnoreCase));
    var size = args[dump + 2].Split('x').Select(int.Parse).ToArray();

    foreach (var c in args.Length > dump + 3 ? args[dump + 3] : "")
    {
        v.OnKey(c switch { '\n' => Key.Enter, '\t' => Key.Tab, _ => new Key(c) });
    }

    var s = Surface.Text(size[0], size[1], out var grid);
    v.Draw(s);

    for (var r = 0; r < size[1]; r++)
    {
        Console.WriteLine(new string(Enumerable.Range(0, size[0]).Select(c => grid[r, c] == '\0' ? ' ' : grid[r, c]).ToArray()).TrimEnd());
    }

    return;
}

using var application = Application.Create();
application.Init();

using var window = new PrototypeWindow(variants, start, application.RequestStop);
application.Run(window);

internal sealed class PrototypeWindow : Window
{
    private readonly Variant[] _variants;
    private readonly Action _quit;
    private readonly Canvas _canvas;
    private int _at;
    private bool _barShown = true;

    public PrototypeWindow(Variant[] variants, int start, Action quit)
    {
        _variants = variants;
        _at = start;
        _quit = quit;
        BorderStyle = Terminal.Gui.Drawing.LineStyle.None;
        CanFocus = true;

        _canvas = new Canvas(this) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false };
        Add(_canvas);
    }

    public Variant Current => _variants[_at];

    public bool BarShown => _barShown;

    public string Label => $"{Current.Key} · {Current.Name}";

    public void Cycle(int by)
    {
        _at = Keys.Wrap(_at + by, _variants.Length);
        _canvas.SetNeedsDraw();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == Key.Q.WithCtrl.KeyCode)
        {
            _quit();

            return true;
        }

        var ch = Keys.Ch(key);

        if (!Current.Typing && ch is '<' or '>')
        {
            Cycle(ch == '<' ? -1 : 1);

            return true;
        }

        if (!Current.Typing && ch == '~')
        {
            _barShown = !_barShown;
            _canvas.SetNeedsDraw();

            return true;
        }

        // Everything else is the variant's, Esc and Tab included, so Terminal.Gui's own quit and focus keys never fire.
        Current.OnKey(key);
        _canvas.SetNeedsDraw();

        return true;
    }
}

internal sealed class Canvas(PrototypeWindow owner) : View
{
    private (int X0, int X1, int Y) _left, _right;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var w = Viewport.Width;
        var h = Viewport.Height;

        if (w <= 0 || h <= 0)
        {
            return true;
        }

        var s = new Surface(this, w, h);
        owner.Current.Draw(s);

        if (owner.BarShown)
        {
            Bar(s);
        }

        return true;
    }

    /// <summary>The floating switcher: loud on purpose, so nobody mistakes it for part of a design.</summary>
    private void Bar(Surface s)
    {
        var label = $"  {owner.Label}  ";
        var hint = $" {owner.Current.Inspiration} · < > switch · ~ hide · ctrl-q quit ";
        var width = 3 + label.Length + 3;
        var x = (s.W - width) / 2;
        var y = s.H - 3;

        var pill = P.B(P.Crust, P.Yellow);
        s.Put(x, y, " ◀ ", pill);
        s.Put(x + 3, y, label, pill);
        s.Put(x + 3 + label.Length, y, " ▶ ", pill);
        _left = (x, x + 3, y);
        _right = (x + 3 + label.Length, x + width, y);

        s.Put((s.W - hint.Length) / 2, y + 1, hint, P.A(P.Yellow, P.Crust));
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.IsSingleClicked || mouse.Position is not { } at)
        {
            return false;
        }

        if (at.Y == _left.Y && at.X >= _left.X0 && at.X < _left.X1)
        {
            owner.Cycle(-1);

            return true;
        }

        if (at.Y == _right.Y && at.X >= _right.X0 && at.X < _right.X1)
        {
            owner.Cycle(1);

            return true;
        }

        return false;
    }
}
