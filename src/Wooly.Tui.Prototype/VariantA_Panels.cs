using Terminal.Gui.Input;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Variant A: lazygit / k9s. Everything is on screen at once in numbered, bordered panels down
// the left; whichever panel has focus grows (lazygit's accordion) and drives a big detail pane on the right. Tabs live
// in a panel's own title ("Home - Local - Federated - #rust") and [ ] walks them. A command log shows what just
// happened, and the bottom row is the keybinding strip.

internal sealed class PanelsVariant : Variant
{
    private static readonly string[] PostTabs = ["Home", "Local", "Federated", "#rust"];
    private static readonly string[] NoticeTabs = ["Notifications", "Requests"];

    private readonly List<string> _log =
    [
        "GET /api/v1/timelines/home  50 posts  0.41s",
        "GET /api/v1/notifications  12 new  0.22s",
        "GET /api/v1/conversations  4  0.18s",
    ];

    private int _focus = 1; // 0 status, 1 posts, 2 notifications, 3 messages
    private readonly int[] _sel = new int[4];
    private readonly int[] _top = new int[4];
    private int _postTab;
    private int _noticeTab;

    public override string Key => "A";
    public override string Name => "Panels";
    public override string Inspiration => "lazygit / k9s";

    public override void Draw(Surface s)
    {
        s.Fill(0, 0, s.W, s.H, P.A(P.Text));

        var leftW = Math.Clamp(s.W * 2 / 5, 30, 60);
        var rightX = leftW;
        var rightW = s.W - leftW;
        var bodyH = s.H - 1;

        // Accordion: the focused panel takes whatever the others leave it.
        int[] want = [3, 6, 6, 6];
        var fixedH = want.Where((_, i) => i != _focus).Sum();
        var heights = want.Select((h, i) => i == _focus ? Math.Max(h, bodyH - fixedH) : h).ToArray();

        var y = 0;
        StatusPanel(s, 0, y, leftW, heights[0]);
        y += heights[0];
        PostsPanel(s, 0, y, leftW, heights[1]);
        y += heights[1];
        NoticesPanel(s, 0, y, leftW, heights[2]);
        y += heights[2];
        MessagesPanel(s, 0, y, leftW, heights[3]);

        var logH = Math.Min(7, bodyH / 3);
        DetailPanel(s, rightX, 0, rightW, bodyH - logH);
        LogPanel(s, rightX, bodyH - logH, rightW, logH);

        Strip(s, s.H - 1);
    }

    private (Terminal.Gui.Drawing.Attribute Line, Terminal.Gui.Drawing.Attribute Title) Frame(int panel) =>
        panel == _focus ? (P.B(P.Green), P.B(P.Green)) : (P.A(P.Surface2), P.A(P.Subtext));

    private static string Tabs(string[] tabs, int at) =>
        string.Join(" - ", tabs.Select((t, i) => i == at ? t : t.ToLowerInvariant()));

    private void StatusPanel(Surface s, int x, int y, int w, int h)
    {
        var (line, title) = Frame(0);
        s.Box(x, y, w, h, line, P.A(P.Text), "[1]─Status", title);
        var col = s.Put(x + 2, y + 1, "✓ ", P.A(P.Green));
        col = s.Put(col, y + 1, Fixtures.Me, P.B(P.Text));
        s.Put(col + 2, y + 1, $"↻ {Fixtures.Quota}", P.A(P.Overlay), x + w - col - 3);
    }

    private void PostsPanel(Surface s, int x, int y, int w, int h)
    {
        var (line, title) = Frame(1);
        var posts = Fixtures.Posts;
        s.Box(x, y, w, h, line, P.A(P.Text), $"[2]─{Tabs(PostTabs, _postTab)}", title, $"{_sel[1] + 1} of {posts.Count}");

        Rows(s, 1, x, y, w, h, posts.Count, (i, row, sel, bg) =>
        {
            var p = posts[i];
            var flag = p.Warning is not null ? "⚠" : p.Media is not null ? "▣" : p.BoostedBy is not null ? "↺" : p.Poll is not null ? "▤" : " ";
            var flagColour = p.Warning is not null ? P.Peach : p.BoostedBy is not null ? P.Green : P.Teal;
            var col = s.Put(x + 1, row, Text.Right(p.Ago, 3) + " ", P.A(P.Overlay, bg));
            col = s.Put(col, row, flag + " ", P.A(flagColour, bg));
            var who = Text.Pad(p.Handle.Split('@')[1], 8);
            col = s.Put(col, row, who + " ", P.A(sel ? P.Mauve : P.Blue, bg));
            s.Put(col, row, p.FirstLine, P.A(P.Text, bg), x + w - 1 - col);
        });
    }

    private void NoticesPanel(Surface s, int x, int y, int w, int h)
    {
        var (line, title) = Frame(2);
        var notices = Fixtures.Notices;
        s.Box(x, y, w, h, line, P.A(P.Text), $"[3]─{Tabs(NoticeTabs, _noticeTab)}", title, $"{_sel[2] + 1} of {notices.Count}");

        Rows(s, 2, x, y, w, h, notices.Count, (i, row, _, bg) =>
        {
            var n = notices[i];
            var col = s.Put(x + 1, row, Text.Right(n.Ago, 3) + " ", P.A(P.Overlay, bg));
            col = s.Put(col, row, Fixtures.Glyph(n.Kind) + " ", P.A(Fixtures.Tint(n.Kind), bg));
            col = s.Put(col, row, n.Who + " ", P.A(P.Text, bg), x + w - 1 - col);
            s.Put(col, row, n.What, P.A(P.Subtext, bg), x + w - 1 - col);
        });
    }

    private void MessagesPanel(Surface s, int x, int y, int w, int h)
    {
        var (line, title) = Frame(3);
        var convos = Fixtures.Convos;
        s.Box(x, y, w, h, line, P.A(P.Text), "[4]─Messages", title, $"{_sel[3] + 1} of {convos.Count}");

        Rows(s, 3, x, y, w, h, convos.Count, (i, row, _, bg) =>
        {
            var c = convos[i];
            var col = s.Put(x + 1, row, Text.Right(c.Ago, 3) + " ", P.A(P.Overlay, bg));
            col = s.Put(col, row, c.Unread > 0 ? "● " : "  ", P.A(P.Red, bg));
            col = s.Put(col, row, c.With + " ", P.B(P.Text, bg), x + w - 1 - col);
            s.Put(col, row, c.Last, P.A(P.Subtext, bg), x + w - 1 - col);
        });
    }

    /// <summary>A list inside a panel, scrolled to keep its selection in view; the selection shows even unfocused.</summary>
    private void Rows(Surface s, int panel, int x, int y, int w, int h, int count, Action<int, int, bool, uint> row)
    {
        var inner = h - 2;
        _top[panel] = Text.Follow(_sel[panel], _top[panel], inner);

        for (var r = 0; r < inner && _top[panel] + r < count; r++)
        {
            var i = _top[panel] + r;
            var sel = i == _sel[panel];
            var bg = sel ? (panel == _focus ? P.Surface1 : P.Surface0) : P.Base;

            if (sel)
            {
                s.Fill(x + 1, y + 1 + r, w - 2, 1, P.A(P.Text, bg));
            }

            row(i, y + 1 + r, sel, bg);
        }
    }

    private void DetailPanel(Surface s, int x, int y, int w, int h)
    {
        s.Box(x, y, w, h, P.A(P.Surface2), P.A(P.Text), "[0]─" + DetailTitle(), P.A(P.Subtext));
        var ix = x + 2;
        var iw = w - 4;
        var bottom = y + h - 1;

        switch (_focus)
        {
            case 2:
            {
                var n = Fixtures.Notices[_sel[2]];
                var col = s.Put(ix, y + 2, Fixtures.Glyph(n.Kind) + " ", P.B(Fixtures.Tint(n.Kind)));
                col = s.Put(col, y + 2, n.Who, P.B(P.Text));
                s.Put(ix, y + 3, n.What, P.A(P.Subtext), iw);
                s.Put(ix, y + 4, n.Ago + " ago", P.A(P.Overlay));

                if (n.Kind is "fav" or "boost" or "reply")
                {
                    s.Rule(ix, y + 6, iw, P.A(P.Surface1));
                    s.Put(ix, y + 7, "your post", P.A(P.Overlay));
                    PostView.Draw(s, new Post("Jeff Mitchell", Fixtures.Me, "4h",
                        "Terminal clients are back, baby. Writing a Mastodon one in C# and having far too much fun with sixel.", 3, 8, 41), ix, y + 8, iw, bottom - y - 8, P.Base);
                }

                break;
            }
            case 3:
            {
                var c = Fixtures.Convos[_sel[3]];
                var row = y + 2;
                string[] lines = [$"{c.With}: hey, quick question", "you: sure, what's up", $"{c.With}: {c.Last}"];

                foreach (var line in lines)
                {
                    var mine = line.StartsWith("you:");
                    var text = line[(line.IndexOf(':') + 2)..];
                    var bw = Math.Min(iw - 6, text.Length + 4);
                    var bx = mine ? ix + iw - bw : ix;
                    s.Box(bx, row, bw, 3, P.A(mine ? P.Mauve : P.Surface2), P.A(P.Text), null);
                    s.Put(bx + 2, row + 1, text, P.A(P.Text), bw - 4);
                    row += 3;
                }

                s.Put(ix, bottom - 2, "▏ write a message… (enter)", P.A(P.Overlay));
                break;
            }
            default:
            {
                var p = Fixtures.Posts[_sel[1]];
                var row = y + 1;

                if (_sel[1] == 0)
                {
                    row += PostView.Draw(s, Fixtures.Ancestor, ix + 2, row, iw - 2, 4, P.Base, stats: false);
                    s.Put(ix, row - 2, "┆", P.A(P.Surface2));
                    s.Put(ix, row - 1, "┆", P.A(P.Surface2));
                }

                s.Put(ix - 1, row, "▌", P.A(P.Green));
                row += PostView.Draw(s, p, ix, row, iw, bottom - row, P.Base);

                if (row + 2 < bottom)
                {
                    s.Rule(ix, row, iw, P.A(P.Surface1));
                    s.Put(ix + 2, row, $" {p.Replies} replies ", P.A(P.Overlay));
                    row += 2;

                    foreach (var reply in Fixtures.Replies)
                    {
                        if (row >= bottom - 2)
                        {
                            break;
                        }

                        s.Put(ix, row, "└", P.A(P.Surface2));
                        row += PostView.Draw(s, reply, ix + 2, row, iw - 2, bottom - row, P.Base, stats: false);
                    }
                }

                break;
            }
        }
    }

    private string DetailTitle() => _focus switch
    {
        2 => "Notification",
        3 => "Conversation",
        _ => "Post - Thread - Boosted by - Favourited by",
    };

    private void LogPanel(Surface s, int x, int y, int w, int h)
    {
        s.Box(x, y, w, h, P.A(P.Surface2), P.A(P.Text), "Command log", P.A(P.Subtext));
        var shown = _log.TakeLast(h - 2).ToList();

        for (var i = 0; i < shown.Count; i++)
        {
            var entry = shown[i];
            var colour = entry.StartsWith("GET") ? P.Overlay : P.Sapphire;
            s.Put(x + 2, y + 1 + i, entry, P.A(colour), w - 4);
        }
    }

    private void Strip(Surface s, int y)
    {
        s.Fill(0, y, s.W, 1, P.A(P.Blue));

        (string Action, string Key)[] pairs = _focus switch
        {
            2 => [("Open", "enter"), ("Dismiss", "d"), ("Follow back", "F"), ("Tabs", "[ ]")],
            3 => [("Open", "enter"), ("New", "n"), ("Mute", "m")],
            _ => [("Reply", "r"), ("Boost", "b"), ("Favourite", "f"), ("Show", "x"), ("Tabs", "[ ]"), ("Compose", "c")],
        };

        var col = 0;

        foreach (var (action, key) in pairs.Concat([("Panels", "1-4/tab"), ("Search", "/"), ("Keybindings", "?")]))
        {
            col = s.Put(col, y, action + ": ", P.A(P.Blue));
            col = s.Put(col, y, key, P.A(P.Sapphire));
            col = s.Put(col, y, " | ", P.A(P.Surface2));
        }

        s.Put(s.W - 7, y, "wooly ♥", P.A(P.Pink));
    }

    public override bool OnKey(Key k)
    {
        var ch = Keys.Ch(k);
        var counts = new[] { 1, Fixtures.Posts.Count, Fixtures.Notices.Count, Fixtures.Convos.Count };

        if (ch is >= '1' and <= '4')
        {
            _focus = ch - '1';
        }
        else if (Keys.Tab(k) || Keys.Right(k))
        {
            _focus = Keys.Wrap(_focus + 1, 4);
        }
        else if (Keys.ShiftTab(k) || Keys.Left(k))
        {
            _focus = Keys.Wrap(_focus - 1, 4);
        }
        else if (Keys.Down(k))
        {
            _sel[_focus] = Math.Min(counts[_focus] - 1, _sel[_focus] + 1);
        }
        else if (Keys.Up(k))
        {
            _sel[_focus] = Math.Max(0, _sel[_focus] - 1);
        }
        else if (ch is '[' or ']')
        {
            var by = ch == ']' ? 1 : -1;

            if (_focus == 1)
            {
                _postTab = Keys.Wrap(_postTab + by, PostTabs.Length);
                _log.Add($"GET /api/v1/timelines/{PostTabs[_postTab].ToLowerInvariant().TrimStart('#')}  50 posts  0.3s");
            }
            else if (_focus == 2)
            {
                _noticeTab = Keys.Wrap(_noticeTab + by, NoticeTabs.Length);
            }
        }
        else if (_focus == 1 && ch is 'f' or 'b' or 'x')
        {
            var p = Fixtures.Posts[_sel[1]];

            if (ch == 'f')
            {
                p.ToggleFav();
                _log.Add($"{(p.Faved ? "Favourited" : "Unfavourited")} {p.Handle}");
            }
            else if (ch == 'b')
            {
                p.ToggleBoost();
                _log.Add($"{(p.Boosted ? "Boosted" : "Unboosted")} {p.Handle}");
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
