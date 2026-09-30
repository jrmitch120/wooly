using Terminal.Gui.Input;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Variant D: yazi / ranger. The whole client is one tree — places › their posts › a post's
// thread — walked with h/l as three Miller columns: where you came from, where you are, and a live preview of what is
// under the cursor. Drilling in slides everything left, so the back stack is always visible. The header is the path;
// the footer is yazi's mode pill and position.

internal sealed class ColumnsVariant : Variant
{
    private sealed record Item(string Glyph, string Label, string Right, uint Tint, Post? Post = null, Place? Place = null);

    private readonly List<(List<Item> Items, int Sel, string Name)> _stack = [];
    private int _sel;
    private List<Item> _items;
    private string _name = "wooly";

    public ColumnsVariant()
    {
        _items = PlaceItems();
    }

    public override string Key => "D";
    public override string Name => "Miller columns";
    public override string Inspiration => "yazi / ranger";

    private static List<Item> PlaceItems() =>
        Fixtures.Places.Select(p => new Item(p.Glyph, p.Name, p.Unread > 0 ? p.Unread.ToString() : "", P.Blue, Place: p)).ToList();

    private static List<Item> PostItems() =>
        Fixtures.Posts.Select(p => new Item(
            p.Warning is not null ? "⚠" : p.Media is not null ? "▣" : p.BoostedBy is not null ? "↺" : "·",
            $"{p.Handle.Split('@')[1],-7} {p.FirstLine}", p.Ago,
            p.Warning is not null ? P.Peach : p.Media is not null ? P.Teal : P.Overlay, Post: p)).ToList();

    private static List<Item> ThreadItems(Post p)
    {
        var items = new List<Item> { new("↑", "mira    " + Fixtures.Ancestor.FirstLine, Fixtures.Ancestor.Ago, P.Overlay, Fixtures.Ancestor) };
        items.Add(new Item("●", $"{p.Handle.Split('@')[1],-7} {p.FirstLine}", p.Ago, P.Mauve, p));
        items.AddRange(Fixtures.Replies.Select(r => new Item("↳", $"{r.Handle.Split('@')[1],-7} {r.FirstLine}", r.Ago, P.Overlay, r)));

        return items;
    }

    private static List<Item> NoticeItems() =>
        Fixtures.Notices.Select(n => new Item(Fixtures.Glyph(n.Kind), $"{n.Who} {n.What}", n.Ago, Fixtures.Tint(n.Kind))).ToList();

    private static List<Item> ConvoItems() =>
        Fixtures.Convos.Select(c => new Item(c.Unread > 0 ? "●" : "✉", $"{c.With}: {c.Last}", c.Ago, c.Unread > 0 ? P.Red : P.Overlay)).ToList();

    /// <summary>What lies one level under an item, or null where it is a leaf.</summary>
    private static List<Item>? Children(Item item) => item switch
    {
        { Place.Name: "Notifications" } => NoticeItems(),
        { Place.Name: "Messages" } => ConvoItems(),
        { Place.Group: "timelines" } => PostItems(),
        { Place: not null } => [new Item("·", "nothing here in the prototype", "", P.Overlay)],
        { Post: { } p } when p != Fixtures.Ancestor && !Fixtures.Replies.Contains(p) => ThreadItems(p),
        _ => null,
    };

    public override void Draw(Surface s)
    {
        s.Fill(0, 0, s.W, s.H, P.A(P.Text));

        // Header: the path so far, yazi-style.
        var path = string.Join(" › ", _stack.Select(f => f.Name).Append(_name));
        var col = s.Put(1, 0, $"{Fixtures.Me}", P.B(P.Blue));
        s.Put(col, 0, $"  {path}", P.A(P.Text), s.W - col - 14);
        s.Put(s.W - 12, 0, $"↻ {Fixtures.Quota}", P.A(P.Overlay));

        // Yazi defaults to 1:4:3; the parent gets a little more here because a handle needs the room.
        var usable = s.W - 2;
        var w1 = usable * 2 / 9;
        var w2 = usable * 4 / 9;
        var w3 = usable - w1 - w2;
        var top = 1;
        var h = s.H - 2;

        if (_stack.Count > 0)
        {
            var parent = _stack[^1];
            Column(s, 0, top, w1, h, parent.Items, parent.Sel, active: false);
        }

        s.VRule(w1, top, h, P.A(P.Surface0));
        Column(s, w1 + 1, top, w2, h, _items, _sel, active: true);
        s.VRule(w1 + 1 + w2, top, h, P.A(P.Surface0));
        Preview(s, w1 + w2 + 2, top, w3, h);

        Footer(s, s.H - 1);
    }

    private static void Column(Surface s, int x, int y, int w, int h, List<Item> items, int sel, bool active)
    {
        var top = Math.Max(0, Text.Follow(sel, 0, h));

        for (var r = 0; r < h && top + r < items.Count; r++)
        {
            var i = top + r;
            var item = items[i];
            var hovered = i == sel;
            var bg = hovered ? (active ? P.Blue : P.Surface0) : P.Base;
            var fg = hovered && active ? P.Crust : P.Text;
            s.Fill(x, y + r, w, 1, P.A(fg, bg));
            s.Put(x + 1, y + r, item.Glyph, P.A(hovered && active ? P.Crust : item.Tint, bg));
            var rw = item.Right.Length;
            s.Put(x + 3, y + r, Text.Clip(item.Label, w - 5 - rw), hovered && active ? P.B(fg, bg) : P.A(fg, bg));
            s.Put(x + w - rw - 1, y + r, item.Right, P.A(hovered && active ? P.Crust : P.Overlay, bg));
        }
    }

    private void Preview(Surface s, int x, int y, int w, int h)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var item = _items[_sel];

        if (item.Post is { } p)
        {
            PostView.Draw(s, p, x + 1, y + 1, w - 2, h - 1, P.Base);

            return;
        }

        if (Children(item) is { } children)
        {
            // Previewing a place is previewing its list, like yazi previewing a directory.
            Column(s, x, y, w, h, children, -1, active: false);

            return;
        }

        foreach (var (line, i) in Text.Wrap(item.Label, w - 2).Select((l, i) => (l, i)))
        {
            s.Put(x + 1, y + 1 + i, line, P.A(P.Text));
        }
    }

    private void Footer(Surface s, int y)
    {
        s.Fill(0, y, s.W, 1, P.A(P.Text, P.Mantle));
        var col = s.Put(0, y, " NOR ", P.B(P.Crust, P.Blue));
        col = s.Put(col, y, " ", P.A(P.Text, P.Surface0));

        if (_items.Count > 0 && _items[_sel].Post is { } p)
        {
            col = s.Put(col, y, $"{p.AudienceMark} {p.Audience} ", P.A(P.Text, P.Surface0));
            col = s.Put(col + 1, y, $"↩ {p.Replies}  ", P.A(P.Overlay, P.Mantle));
            col = s.Put(col, y, $"↺ {p.Boosts}  ", P.A(p.Boosted ? P.Green : P.Overlay, P.Mantle));
            s.Put(col, y, $"★ {p.Favs}", P.A(p.Faved ? P.Yellow : P.Overlay, P.Mantle));
        }
        else
        {
            s.Put(col, y, $"{_name} ", P.A(P.Text, P.Surface0));
        }

        var pos = $" {_sel + 1}/{_items.Count} ";
        s.Put(s.W - pos.Length, y, pos, P.B(P.Crust, P.Blue));
        s.Put(s.W - pos.Length - 22, y, "f fav  b boost  x show", P.A(P.Overlay, P.Mantle));
    }

    public override bool OnKey(Key k)
    {
        var ch = Keys.Ch(k);

        if (Keys.Down(k))
        {
            _sel = Math.Min(_items.Count - 1, _sel + 1);
        }
        else if (Keys.Up(k))
        {
            _sel = Math.Max(0, _sel - 1);
        }
        else if (Keys.Right(k) || Keys.Enter(k))
        {
            if (_items.Count > 0 && Children(_items[_sel]) is { } children)
            {
                var item = _items[_sel];
                _stack.Add((_items, _sel, _name));
                _name = item.Place?.Name ?? item.Post?.Handle.Split('@')[1] ?? item.Label;
                _items = children;
                _sel = item.Post is not null ? 1 : 0; // a thread opens on the post itself, not its ancestor
            }
        }
        else if (Keys.Left(k) || Keys.Backspace(k))
        {
            if (_stack.Count > 0)
            {
                (_items, _sel, _name) = _stack[^1];
                _stack.RemoveAt(_stack.Count - 1);
            }
        }
        else if (ch is 'f' or 'b' or 'x' && _items.Count > 0 && _items[_sel].Post is { } p)
        {
            if (ch == 'f')
            {
                p.ToggleFav();
            }
            else if (ch == 'b')
            {
                p.ToggleBoost();
            }
            else
            {
                p.Revealed = !p.Revealed;
            }
        }
        else
        {
            return false;
        }

        return true;
    }
}
