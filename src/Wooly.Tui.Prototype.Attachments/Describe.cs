// PROTOTYPE (#374) — throwaway. The description editor: the picture in view, and a counter against the limit.

using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype.Attachments;

internal sealed class DescribeScreen : Screen
{
    private readonly Attachment _item;

    private readonly Editor _editor;

    public DescribeScreen(Attachment item)
    {
        _item = item;
        _editor = new Editor
        {
            Text = item.Description,
            Hint = "Describe it for people who can't see it",
            X = Pos.Func(_ => Wide ? Viewport.Width / 2 + 1 : Geometry.Pad),
            Y = Pos.Func(_ => Wide ? 5 : PictureRows + 6),
            Width = Dim.Func(_ => Wide ? Viewport.Width - Viewport.Width / 2 - 1 - Geometry.Pad : Viewport.Width - Geometry.Pad * 2),
            Height = Dim.Func(_ => Math.Max(3, Viewport.Height - (Wide ? 5 : PictureRows + 6) - 2)),
        };
        _editor.ContentsChanged += (_, _) =>
        {
            _item.Description = _editor.Text;
            SetNeedsDraw();
        };
        _editor.Ahead = key =>
        {
            if (key == Key.Esc || key == Key.S.WithCtrl)
            {
                Proto.Shell.Pop();
                Proto.Say(_item.Description.Trim().Length > 0 ? $"described {_item.Name}" : $"{_item.Name} has no description");
                return true;
            }

            return false;
        };
        Add(_editor);
    }

    private bool Wide => Viewport.Width >= 110;

    private int PictureRows => Math.Max(4, (Viewport.Height - 8) / 2);

    public override string Hints => "esc or ctrl-s done (what you typed is kept) · enter makes a new line · the instance gets it as soon as it's sent";

    public override void Shown() => _editor.SetFocus();

    protected override void Paint()
    {
        var width = Viewport.Width;

        Spans(Geometry.Pad, 0, ("Compose", Role.Muted), (" › ", Role.Muted), ("Describe ", Role.PanelTitle), (_item.Name, Role.Body),
            ($" · {_item.KindWord} · {_item.Size} · ", Role.Muted),
            (_item.State switch
            {
                State.Uploading(var done) => $"uploading {done:P0}",
                State.Processing => "processing",
                State.Ready => "ready",
                State.Refused(var why, _) => $"refused: {why}",
                _ => "",
            }, _item.State is State.Refused ? Role.Error : Role.Muted));
        Put(Geometry.Pad, 1, new string('─', Math.Max(0, width - Geometry.Pad * 2)), Role.PanelBorder);

        var picture = Wide
            ? new Rectangle(Geometry.Pad, 3, width / 2 - Geometry.Pad - 1, Viewport.Height - 5)
            : new Rectangle(Geometry.Pad, 3, width - Geometry.Pad * 2, PictureRows);

        Pics.Paint(this, picture, _item.Path, Theme, topLeft: true); // level with the label beside it

        var labelY = Wide ? 3 : PictureRows + 4;
        var labelX = Wide ? width / 2 + 1 : Geometry.Pad;
        Put(labelX, labelY, "Description (alt text)", Role.Muted);

        var used = _item.Description.Length;
        var count = $"{used} / {Instance.DescriptionLimit}";
        var role = used > Instance.DescriptionLimit ? Role.Error : used > Instance.DescriptionLimit * 9 / 10 ? Role.QuotaLow : Role.Muted;
        Put(width - Geometry.Pad - Glyphs.Columns(count), Viewport.Height - 1, count, role);
    }
}
