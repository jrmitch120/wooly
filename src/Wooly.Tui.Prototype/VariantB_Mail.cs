using Terminal.Gui.Input;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Variant B: aerc / neomutt, with a helix-style status line. A timeline is an inbox: one dense
// row per post in aligned columns (when, who, flags, text, counts), and the selected one previewed underneath with a
// mail-style header. Places are tabs across the top instead of a rail; opening a post opens it as a tab of its own
// (aerc's message tabs), so the tab bar doubles as the back stack.

internal sealed class MailVariant : Variant
{
    private sealed record Tab(string Title, int Badge, Post? Open = null);

    private readonly List<Tab> _tabs =
    [
        new("Home", 3), new("Local", 0), new("Federated", 0), new("#rust", 7), new("Notifications", 12), new("Messages", 2),
    ];

    private int _tab;
    private int _sel;
    private int _top;
    private bool _preview = true;
    private string _message = "";

    public override string Key => "B";
    public override string Name => "Mail client";
    public override string Inspiration => "aerc / neomutt / helix";

    public override void Draw(Surface s)
    {
        s.Fill(0, 0, s.W, s.H, P.A(P.Text));
        TabBar(s);

        var tab = _tabs[_tab];
        var bodyTop = 1;
        var bodyBottom = s.H - 2; // status line, then message line

        if (tab.Open is not null)
        {
            Opened(s, tab.Open, bodyTop, bodyBottom);
        }
        else if (tab.Title == "Notifications")
        {
            Table(s, bodyTop, bodyBottom, Fixtures.Notices.Count, NoticeHeader, NoticeRow, null);
        }
        else if (tab.Title == "Messages")
        {
            Table(s, bodyTop, bodyBottom, Fixtures.Convos.Count, ConvoHeader, ConvoRow, null);
        }
        else
        {
            Table(s, bodyTop, bodyBottom, Fixtures.Posts.Count, PostHeader, PostRow, _preview ? Preview : null);
        }

        StatusLine(s, s.H - 2);
        s.Fill(0, s.H - 1, s.W, 1, P.A(P.Text));
        s.Put(1, s.H - 1, _message, P.A(P.Subtext));
    }

    private void TabBar(Surface s)
    {
        s.Fill(0, 0, s.W, 1, P.A(P.Text, P.Mantle));
        var col = 0;

        for (var i = 0; i < _tabs.Count; i++)
        {
            var t = _tabs[i];
            var active = i == _tab;
            var bg = active ? P.Base : P.Mantle;
            var title = t.Open is not null ? $" {Text.Clip(t.Title, 22)} ×" : $" {t.Title}";
            col = s.Put(col, 0, title, active ? P.B(P.Mauve, bg) : P.A(P.Overlay, bg));

            if (t.Badge > 0)
            {
                col = s.Put(col, 0, $" {t.Badge}", P.B(active ? P.Peach : P.Surface2, bg));
            }

            col = s.Put(col, 0, " ", P.A(P.Text, bg));
            col = s.Put(col, 0, "│", P.A(P.Surface0, P.Mantle));
        }

        var brand = $" {Fixtures.Instance} ";

        if (col < s.W - brand.Length)
        {
            s.Put(s.W - brand.Length, 0, brand, P.A(P.Overlay, P.Mantle));
        }
    }

    /// <summary>The inbox: a header row, then one row per thing, then (optionally) a preview pane under a rule.</summary>
    private void Table(Surface s, int top, int bottom, int count, Action<Surface, int> header, Action<Surface, int, int, uint> row,
        Action<Surface, int, int>? preview)
    {
        var listBottom = preview is null ? bottom : top + Math.Max(5, (bottom - top) * 2 / 5);
        header(s, top);
        var rows = listBottom - top - 1;
        _sel = Math.Min(_sel, count - 1);
        _top = Text.Follow(_sel, _top, rows);

        for (var r = 0; r < rows && _top + r < count; r++)
        {
            var i = _top + r;
            var sel = i == _sel;
            var bg = sel ? P.Surface0 : P.Base;
            s.Fill(0, top + 1 + r, s.W, 1, P.A(P.Text, bg));

            if (sel)
            {
                s.Put(0, top + 1 + r, "▌", P.A(P.Mauve, bg));
            }

            row(s, i, top + 1 + r, bg);
        }

        if (preview is not null)
        {
            s.Rule(0, listBottom, s.W, P.A(P.Surface1));
            s.Put(2, listBottom, $" {_sel + 1}/{count} ", P.A(P.Overlay));
            preview(s, listBottom + 1, bottom);
        }
    }

    private static int AuthorW(Surface s) => Math.Clamp(s.W / 6, 12, 22);

    private void PostHeader(Surface s, int y)
    {
        var a = P.B(P.Overlay);
        s.Put(2, y, "WHEN", a);
        s.Put(8, y, "AUTHOR", a);
        s.Put(9 + AuthorW(s), y, "  POST", a);
        s.Put(s.W - 16, y, Text.Right("↩", 4) + Text.Right("↺", 5) + Text.Right("★", 5), a);
    }

    private void PostRow(Surface s, int i, int y, uint bg)
    {
        var p = Fixtures.Posts[i];
        var unread = i < 3;
        s.Put(2, y, Text.Right(p.Ago, 4), P.A(P.Overlay, bg));
        s.Put(8, y, Text.Pad(p.Name, AuthorW(s)), unread ? P.B(P.Text, bg) : P.A(P.Subtext, bg));

        var fx = 9 + AuthorW(s);
        s.Put(fx, y, p.BoostedBy is not null ? "↺" : " ", P.A(P.Green, bg));
        s.Put(fx + 1, y, p.Warning is not null ? "⚠" : p.Media is not null ? "▣" : p.Poll is not null ? "▤" : " ",
            P.A(p.Warning is not null ? P.Peach : P.Teal, bg));

        var textW = s.W - 16 - fx - 4;
        var text = p.Warning is not null && !p.Revealed ? $"CW: {p.Warning}" : p.FirstLine;
        s.Put(fx + 3, y, Text.Clip(text, textW), p.Warning is not null && !p.Revealed ? P.I(P.Peach, bg) : P.A(unread ? P.Text : P.Subtext, bg));

        var col = s.W - 16;
        col = s.Put(col, y, Text.Right(p.Replies.ToString(), 4), P.A(P.Overlay, bg));
        col = s.Put(col, y, Text.Right(p.Boosts.ToString(), 5), P.A(p.Boosted ? P.Green : P.Overlay, bg));
        s.Put(col, y, Text.Right(p.Favs.ToString(), 5), P.A(p.Faved ? P.Yellow : P.Overlay, bg));
    }

    private void NoticeHeader(Surface s, int y)
    {
        var a = P.B(P.Overlay);
        s.Put(2, y, "WHEN", a);
        s.Put(8, y, "KIND", a);
        s.Put(18, y, "WHO", a);
        s.Put(20 + AuthorW(s) + 6, y, "WHAT", a);
    }

    private void NoticeRow(Surface s, int i, int y, uint bg)
    {
        var n = Fixtures.Notices[i];
        s.Put(2, y, Text.Right(n.Ago, 4), P.A(P.Overlay, bg));
        s.Put(8, y, $"{Fixtures.Glyph(n.Kind)} {n.Kind}", P.A(Fixtures.Tint(n.Kind), bg));
        s.Put(18, y, Text.Pad(n.Who, AuthorW(s) + 6), P.B(P.Text, bg));
        s.Put(20 + AuthorW(s) + 6, y, n.What, P.A(P.Subtext, bg), s.W - AuthorW(s) - 28);
    }

    private void ConvoHeader(Surface s, int y)
    {
        var a = P.B(P.Overlay);
        s.Put(2, y, "WHEN", a);
        s.Put(8, y, "WITH", a);
        s.Put(10 + AuthorW(s) + 6, y, "LAST MESSAGE", a);
    }

    private void ConvoRow(Surface s, int i, int y, uint bg)
    {
        var c = Fixtures.Convos[i];
        s.Put(2, y, Text.Right(c.Ago, 4), P.A(P.Overlay, bg));
        s.Put(8, y, Text.Pad((c.Unread > 0 ? "● " : "  ") + c.With, AuthorW(s) + 6), c.Unread > 0 ? P.B(P.Text, bg) : P.A(P.Subtext, bg));
        s.Put(10 + AuthorW(s) + 6, y, c.Last, P.A(P.Subtext, bg), s.W - AuthorW(s) - 18);
    }

    /// <summary>Mail-style headers over the body, so who/when/where reads like From/Date/To.</summary>
    private void Preview(Surface s, int top, int bottom)
    {
        var p = Fixtures.Posts[_sel];
        var w = Math.Min(s.W - 4, 90);
        var y = top + 1;
        Header(s, y++, "From", $"{p.Name} <{p.Handle}>");
        Header(s, y++, "Date", $"{p.Ago} ago");
        Header(s, y++, "To", p.Audience + " " + p.AudienceMark);

        if (p.BoostedBy is not null)
        {
            Header(s, y++, "Via", $"boosted by {p.BoostedBy}");
        }

        y++;
        PostView.Draw(s, p, 2, y, w, bottom - y, P.Base, byline: false);
    }

    private static void Header(Surface s, int y, string key, string value)
    {
        s.Put(2, y, Text.Right(key, 5) + ": ", P.B(P.Mauve));
        s.Put(9, y, value, P.A(P.Text));
    }

    /// <summary>An opened post: the thread as a quoted mail chain, oldest first.</summary>
    private void Opened(Surface s, Post p, int top, int bottom)
    {
        var w = Math.Min(s.W - 6, 90);
        var y = top + 1;

        y += PostView.Draw(s, Fixtures.Ancestor, 4, y, w - 2, bottom - y, P.Base, stats: false);
        s.Put(4, y++, "↓ in reply to the above", P.I(P.Overlay));
        y++;

        s.Put(2, y, "▎", P.A(P.Mauve));
        y += PostView.Draw(s, p, 4, y, w - 2, bottom - y, P.Base);
        y++;

        foreach (var reply in Fixtures.Replies)
        {
            if (y >= bottom - 2)
            {
                break;
            }

            var h = PostView.Draw(s, reply, 8, y, w - 6, bottom - y, P.Base, stats: false);

            for (var r = 0; r < h; r++)
            {
                s.Put(5, y + r, "> ", P.A(P.Surface2));
            }

            y += h + 1;
        }
    }

    private void StatusLine(Surface s, int y)
    {
        s.Fill(0, y, s.W, 1, P.A(P.Text, P.Mantle));
        var open = _tabs[_tab].Open is not null;
        var mode = open ? " VIEW " : " NORMAL ";
        var col = s.Put(0, y, mode, P.B(P.Crust, open ? P.Blue : P.Green));
        col = s.Put(col + 1, y, Text.Clip(_tabs[_tab].Title.ToLowerInvariant(), 28), P.A(P.Text, P.Mantle));
        col = s.Put(col + 2, y, _preview ? "[preview]" : "", P.A(P.Overlay, P.Mantle));

        var right = $"↻ {Fixtures.Quota}   {Fixtures.Me} ";
        s.Put(s.W - right.Length - 8, y, right, P.A(P.Subtext, P.Mantle));
        s.Put(s.W - 7, y, $" {_tab + 1}/{_tabs.Count} ", P.B(P.Crust, P.Lavender));
    }

    public override bool OnKey(Key k)
    {
        var ch = Keys.Ch(k);
        var tab = _tabs[_tab];
        _message = "";

        if (Keys.Tab(k) || ch == 'L')
        {
            _tab = Keys.Wrap(_tab + 1, _tabs.Count);
            _sel = 0;
        }
        else if (Keys.ShiftTab(k) || ch == 'H')
        {
            _tab = Keys.Wrap(_tab - 1, _tabs.Count);
            _sel = 0;
        }
        else if (ch is >= '1' and <= '9' && ch - '1' < _tabs.Count)
        {
            _tab = ch - '1';
            _sel = 0;
        }
        else if (Keys.Down(k))
        {
            _sel++;
        }
        else if (Keys.Up(k))
        {
            _sel = Math.Max(0, _sel - 1);
        }
        else if (ch == 'p')
        {
            _preview = !_preview;
        }
        else if (Keys.Enter(k) && tab.Open is null && tab.Title is not ("Notifications" or "Messages"))
        {
            var p = Fixtures.Posts[_sel];
            _tabs.Add(new Tab($"{p.Handle.Split('@')[1]}: {p.FirstLine}", 0, p));
            _tab = _tabs.Count - 1;
        }
        else if ((ch == 'q' || Keys.Esc(k)) && tab.Open is not null)
        {
            _tabs.RemoveAt(_tab);
            _tab = Math.Min(_tab, _tabs.Count - 1);
        }
        else if (ch is 'f' or 'b' or 'x' && tab.Title is not ("Notifications" or "Messages"))
        {
            var p = tab.Open ?? Fixtures.Posts[_sel];

            if (ch == 'f')
            {
                p.ToggleFav();
                _message = p.Faved ? $"★ favourited {p.Handle}" : "unfavourited";
            }
            else if (ch == 'b')
            {
                p.ToggleBoost();
                _message = p.Boosted ? $"↺ boosted {p.Handle}" : "unboosted";
            }
            else
            {
                p.Revealed = !p.Revealed;
            }
        }
        else if (ch == ':')
        {
            _message = ":  (aerc-style command line — see variant C for a palette)";
        }
        else
        {
            _message = "j/k move · enter open as tab · q close tab · tab/1-9 tabs · p preview · f b x";

            return false;
        }

        return true;
    }
}
