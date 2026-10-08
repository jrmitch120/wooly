// PROTOTYPE (#374) — throwaway, not production. Answers: how should attachments look and handle on the compose screen?
//
// Three layouts, switchable while it runs (F2 or a click on the bottom bar):
//   A  an Attach header (📎) with one row per attachment
//   B  a strip of drawn thumbnails between the editor and the footer
//   C  a one-line summary on the header that opens an attachments screen
// plus the file browser (ctrl-o), the description editor (enter on an attachment), remove (del) and reorder (shift-↑/↓,
// or drag with the mouse), the quiet "no description" mark, the sensitive toggle (s, or a click), fake uploads with
// progress, processing, ready and one refusal (the third file attached drops its connection; retry with r), real
// Kitty/sixel pictures, real OS-clipboard ctrl-v, and real drag-and-drop of paths onto the terminal.
//
// Run:  dotnet run --project src/Wooly.Tui.Prototype.Attachments
// Also: F3 compact/tall rows (A) · F5 the "no description" mark's wording · F6 dark/light · ctrl-q quits.

using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Tui.Prototype.Attachments;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Glyphs = Wooly.Tui.Rendering.Glyphs;

using var application = Application.Create();

application.Init();
Pics.App = application;
Proto.App = application;

var shell = new ShellWindow();
Proto.Shell = shell;

application.Paste += (_, pasted) => pasted.Handled = Proto.Paste(pasted.Text);
application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
{
    Proto.Draft.Tick(0.1);
    return true;
});

shell.Initialized += (_, _) =>
{
    Pics.ForgetAll();
    shell.Push(new ComposeScreen());
};
shell.IsRunningChanged += (_, _) =>
{
    if (!shell.IsRunning) Pics.ForgetAll();
};

application.Run(shell);
shell.Dispose();

return 0;

namespace Wooly.Tui.Prototype.Attachments
{
    internal enum ComposeLayout { Rows, Strip, Summary }

    internal static class Proto
    {
        public static IApplication App { get; set; } = null!;

        public static ShellWindow Shell { get; set; } = null!;

        public static ITheme Theme { get; private set; } = Themes.Dark;

        public static Draft Draft { get; } = new();

        public static ComposeLayout Layout { get; set; } = ComposeLayout.Rows;

        public static bool TallRows { get; set; }

        public static int MarkStyle { get; set; }

        public static readonly string[] Marks = ["no alt text", "○ no description", "○", "· add a description"];

        public static string Mark => Marks[MarkStyle];

        public static int LabelStyle { get; set; }

        /// <summary>The Attach header's label: candidates for the paper clip, F7 cycles them.</summary>
        public static readonly string[] AttachLabels = ["Media", "File", "Att", "+", "📎", "▒"];

        public static string AttachLabel => AttachLabels[LabelStyle];

        public static int GripStyle { get; set; }

        /// <summary>Candidates for a row's drag handle, F8 cycles them; a blank is "no handle".</summary>
        public static readonly string[] Grips = ["⠶", "≡", "∷", "⁞", "↕", " ", "⋮⋮", "▴▾", "⇕", "∶"];

        public static string Grip => Grips[GripStyle];

        /// <summary>Where the browser opens: the folder last attached from this session, else where Wooly was launched.</summary>
        public static string Folder { get; set; } = Environment.CurrentDirectory;

        private static bool _toldNoTool;

        public static bool Global(Key key)
        {
            if (key == Key.F2)
            {
                Layout = (ComposeLayout)(((int)Layout + 1) % 3);
                Say($"layout {LayoutName(Layout)}");
                Shell.Relaid();
                return true;
            }

            if (key == Key.F3)
            {
                TallRows = !TallRows;
                Shell.Relaid();
                return true;
            }

            if (key == Key.F5)
            {
                MarkStyle = (MarkStyle + 1) % Marks.Length;
                Shell.Relaid();
                return true;
            }

            if (key == Key.F7)
            {
                LabelStyle = (LabelStyle + 1) % AttachLabels.Length;
                Say($"Attach header label: {AttachLabel}");
                Shell.Relaid();
                return true;
            }

            if (key == Key.F8)
            {
                GripStyle = (GripStyle + 1) % Grips.Length;
                Say($"drag handle: {(Grip == " " ? "none" : Grip)}");
                Shell.Relaid();
                return true;
            }

            if (key == Key.F6)
            {
                Theme = ReferenceEquals(Theme, Themes.Dark) ? Themes.Light : Themes.Dark;
                Shell.Relaid();
                return true;
            }

            if (key == Key.Q.WithCtrl)
            {
                App.RequestStop();
                return true;
            }

            return false;
        }

        public static string LayoutName(ComposeLayout layout) => layout switch
        {
            ComposeLayout.Rows => "A · rows",
            ComposeLayout.Strip => "B · strip",
            _ => "C · summary",
        };

        public static void Say(string message) => Shell.Say(message);

        public static void Attach(IReadOnlyList<string> paths, string how)
        {
            if (Draft.Items.Count >= Instance.Most)
            {
                Say($"{Instance.Most} is the most a post can carry here — nothing attached");
                return;
            }

            var leftOut = Draft.Attach(paths);
            var attached = paths.Count - leftOut;

            Say(leftOut > 0
                ? $"attached {attached} {how}; {leftOut} left out — {Instance.Most} is the most a post can carry"
                : $"attached {attached} {how}");
        }

        public static void OpenBrowser()
        {
            if (Draft.Items.Count >= Instance.Most)
            {
                Say($"already {Instance.Most} attached — the most a post can carry here");
                return;
            }

            Shell.Push(new BrowserScreen());
        }

        public static void Describe(Attachment item) => Shell.Push(new DescribeScreen(item));

        public static void ToggleSensitive()
        {
            if (Draft.Locked)
            {
                Say("sensitive is locked on while there is a content warning — the warning hides attachments already");
                return;
            }

            Draft.SensitiveChosen = !Draft.SensitiveChosen;
            Draft.Touch();
        }

        public static void Send()
        {
            if (Draft.Items.FirstOrDefault(item => item.State is State.Refused) is { } refused)
            {
                Say($"can't send: {refused.Name} was refused — remove it (del) or retry (r)");
            }
            else if (Draft.Unfinished > 0)
            {
                Say($"will send once {Draft.Unfinished} finish{(Draft.Unfinished == 1 ? "es" : "")} uploading… (the real thing: esc cancels the wait)");
            }
            else
            {
                Say($"would send now: {Draft.Items.Count} attachment(s), sensitive {(Draft.Sensitive ? "on" : "off")} (prototype — nothing goes anywhere)");
            }
        }

        /// <summary>A paste: dropped files where every word is an accepted, existing file; text otherwise.</summary>
        public static bool Paste(string text)
        {
            if (Shell.Top is not (ComposeScreen or ManageScreen))
            {
                return false;
            }

            if (Dropped.Paths(text) is { } paths)
            {
                Attach(paths, paths.Count == 1 ? "dropped file" : "dropped files");
                return true;
            }

            return false;
        }

        /// <summary>ctrl-v: the OS clipboard first, then the text paste as now.</summary>
        public static void ClipboardPaste(Action<string> text)
        {
            switch (OsClipboard.Read())
            {
                case Clipped.Files(var files):
                    var accepted = files.Where(Instance.Takes).ToList();

                    if (accepted.Count == 0)
                    {
                        Say("the copied files aren't types this instance accepts");
                    }
                    else
                    {
                        Attach(accepted, "copied file" + (accepted.Count == 1 ? "" : "s"));
                    }

                    break;

                case Clipped.Picture(var path):
                    Attach([path], $"from the clipboard as {Path.GetFileName(path)}");
                    break;

                case Clipped.Text(var value):
                    text(value);
                    break;

                case Clipped.NoTool(var why):
                    if (!_toldNoTool)
                    {
                        _toldNoTool = true;
                        Say(why);
                    }

                    break;
            }
        }
    }

    internal abstract class Screen : Painted
    {
        public abstract string Hints { get; }

        public virtual void Shown() => SetFocus();

        public virtual void Relaid() => SetNeedsLayout();
    }

    internal sealed class ShellWindow : Window
    {
        private readonly View _host = new() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(2), CanFocus = true };

        private readonly StatusRow _status = new() { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Height = 1 };

        private readonly PrototypeBar _bar = new() { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1 };

        private readonly List<Screen> _stack = [];

        private string? _said;

        private DateTime _saidAt;

        public ShellWindow()
        {
            BorderStyle = LineStyle.None;
            Add(_host, _status, _bar);
            Proto.Draft.Changed += () =>
            {
                SetNeedsDraw();
                Top?.Relaid();
            };
        }

        public Screen? Top => _stack.Count > 0 ? _stack[^1] : null;

        public string Said => _said is { } said && DateTime.Now - _saidAt < TimeSpan.FromSeconds(6) ? said : Top?.Hints ?? "";

        public bool Saying => _said is not null && DateTime.Now - _saidAt < TimeSpan.FromSeconds(6);

        public void Say(string message)
        {
            _said = message;
            _saidAt = DateTime.Now;
            SetNeedsDraw();
        }

        public void Push(Screen screen)
        {
            if (Top is { } under) under.Visible = false;

            screen.X = 0;
            screen.Y = 0;
            screen.Width = Dim.Fill();
            screen.Height = Dim.Fill();
            _stack.Add(screen);
            _host.Add(screen);
            screen.Shown();
            SetNeedsDraw();
        }

        public void Pop()
        {
            if (_stack.Count <= 1) return;

            var screen = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            _host.Remove(screen);
            screen.Dispose();
            Top!.Visible = true;
            Top.Relaid();
            Top.Shown();
            SetNeedsDraw();
        }

        public void Relaid()
        {
            foreach (var screen in _stack) screen.Relaid();
            SetNeedsDraw();
        }

        protected override bool OnKeyDown(Key key) => Proto.Global(key) || base.OnKeyDown(key);
    }

    internal sealed class StatusRow : Painted
    {
        public StatusRow() => CanFocus = false;

        protected override void Paint()
        {
            var shell = Proto.Shell;
            Put(1, 0, shell.Said, shell.Saying ? Role.Body : Role.Muted);
        }
    }

    /// <summary>The prototype's own bar: the layout (clickable), the variants, and the state behind it.</summary>
    internal sealed class PrototypeBar : Painted
    {
        private readonly List<(Rectangle Where, ComposeLayout Layout)> _hits = [];

        public PrototypeBar() => CanFocus = false;

        protected override void Paint()
        {
            _hits.Clear();
            Blank(0, Role.Band);

            var x = Spans(0, 0, (" PROTOTYPE #374 ", Role.SelectedText), (" ", Role.Band));

            foreach (var layout in Enum.GetValues<ComposeLayout>())
            {
                var name = $" {Proto.LayoutName(layout)} ";
                Put(x, 0, name, layout == Proto.Layout ? Role.SelectedText : Role.Muted);
                _hits.Add((new Rectangle(x, 0, Glyphs.Columns(name), 1), layout));
                x += Glyphs.Columns(name);
            }

            var draft = Proto.Draft;
            var states = string.Join(" ", draft.Items.Select(item => item.State switch
            {
                State.Uploading(var done) => $"{done:P0}",
                State.Processing => "proc",
                State.Ready => "ok",
                State.Refused => "✗",
                _ => "?",
            }));
            var cell = Pics.Cell;

            Put(x, 0,
                $" F2  · rows {(Proto.TallRows ? "tall" : "compact")} F3 · mark F5 · theme F6 · label {Proto.AttachLabel} F7 · grip {(Proto.Grip == " " ? "none" : Proto.Grip)} F8 │ {Pics.Way} {cell.Width}×{cell.Height}px"
                + $" · sent {Pics.BytesSent / 1024}KB │ {draft.Items.Count}/{Instance.Most} [{states}] sensitive {(draft.Sensitive ? draft.Locked ? "on·locked" : "on" : "off")} │ ctrl-q",
                Role.Band);
        }

        protected override bool OnMouseEvent(Mouse mouse)
        {
            if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) && mouse.Position is { } at
                && _hits.FirstOrDefault(hit => hit.Where.Contains(at)) is { Where.Width: > 0 } hit)
            {
                Proto.Layout = hit.Layout;
                Proto.Shell.Relaid();
                return true;
            }

            return base.OnMouseEvent(mouse);
        }
    }

    /// <summary>A one-line field in the theme, with a dim hint while empty and keys looked at before its own.</summary>
    internal sealed class Field : TextField
    {
        public string Hint { get; set; } = "";

        public Func<Key, bool>? Ahead { get; set; }

        protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
        {
            currentAttribute = Proto.Theme.For(Role.ContentWarning);
            return true;
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var drawn = base.OnDrawingContent(context);

            if (Text.Length == 0)
            {
                SetAttribute(Proto.Theme.For(Role.Muted));
                AddStr(0, 0, Glyphs.Cut(Hint, Viewport.Width));
            }

            return drawn;
        }

        protected override bool OnKeyDown(Key key) => Proto.Global(key) || Ahead?.Invoke(key) == true || base.OnKeyDown(key);
    }

    /// <summary>A multi-line editor in the theme, with a dim hint while empty and keys looked at before its own.</summary>
    internal sealed class Editor : TextView
    {
        public string Hint { get; set; } = "";

        public Func<Key, bool>? Ahead { get; set; }

        public Editor() => WordWrap = true;

        protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
        {
            currentAttribute = role == VisualRole.Active ? Proto.Theme.For(Role.SelectedText) : Proto.Theme.For(Role.Body);
            return true;
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var drawn = base.OnDrawingContent(context);

            if (Text.Length == 0 && Viewport.Width > 0)
            {
                SetAttribute(Proto.Theme.For(Role.Muted));
                AddStr(0, 0, Glyphs.Cut(Hint, Viewport.Width));
            }

            return drawn;
        }

        public void Insert(string text) => OnPaste(text);

        protected override bool OnKeyDown(Key key) => Proto.Global(key) || Ahead?.Invoke(key) == true || base.OnKeyDown(key);
    }
}
