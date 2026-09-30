using Terminal.Gui.Input;

namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Variant C: glow / helix / posting. No rail, no status row, no chrome: one centred reading
// column and nothing else until you ask. Counts appear only on the post you are on. Navigation lives in a command
// palette (ctrl-p or :) with fuzzy matching, and in helix-style which-key popups (space for actions on this post,
// g for places) that teach the keys as you use them. Actions answer with a toast rather than a status row.
// Notifications are grouped by day and folded ("Mira and 4 others").

internal sealed class ZenVariant : Variant
{
    private sealed record Command(string Name, string Key, Action Run);

    private readonly List<Command> _commands;
    private string _place = "Home";
    private int _sel;
    private int _top;
    private string? _popup; // null, "space", "g"
    private bool _palette;
    private string _query = "";
    private int _paletteSel;
    private string? _toast;

    public ZenVariant()
    {
        _commands =
        [
            new("Go to Home", "g h", () => Go("Home")),
            new("Go to Local", "g l", () => Go("Local")),
            new("Go to Federated", "g f", () => Go("Federated")),
            new("Go to #rust", "g t", () => Go("#rust")),
            new("Go to Notifications", "g n", () => Go("Notifications")),
            new("Go to Messages", "g m", () => Go("Messages")),
            new("Compose a post", "c", () => _toast = "✎ compose opens here"),
            new("Search posts, people and tags", "/", () => _toast = "⌕ search opens here"),
            new("Discover people to follow", "g d", () => _toast = "✦ discover opens here"),
            new("Switch profile", "g p", () => _toast = "☺ profiles: jeff@mastodon.social, jeff@hachyderm.io"),
            new("Change theme", "", () => _toast = "◐ themes: mocha, latte, dracula, nord"),
            new("Favourite this post", "space f", () => Act('f')),
            new("Boost this post", "space b", () => Act('b')),
            new("Show content warning", "space x", () => Act('x')),
            new("Quit", "ctrl-q", () => _toast = "ctrl-q quits"),
        ];
    }

    public override string Key => "C";
    public override string Name => "Zen reader";
    public override string Inspiration => "glow / helix / posting";
    public override bool Typing => _palette;

    private void Go(string place)
    {
        _place = place;
        _sel = 0;
        _top = 0;
    }

    public override void Draw(Surface s)
    {
        s.Fill(0, 0, s.W, s.H, P.A(P.Text));

        var cw = Math.Min(72, s.W - 4);
        var x0 = (s.W - cw) / 2;

        var title = $"{_place.ToLowerInvariant()}  ·  {Fixtures.Instance}";
        s.Put((s.W - title.Length) / 2, 0, title, P.A(P.Overlay));
        s.Put(s.W - 6, 0, "◆ 12", P.A(P.Peach));

        switch (_place)
        {
            case "Notifications":
                Notifications(s, x0, cw);
                break;
            case "Messages":
                Messages(s, x0, cw);
                break;
            default:
                Feed(s, x0, cw);
                break;
        }

        s.Fill(0, s.H - 1, s.W, 1, P.A(P.Text));
        var hint = "ctrl-p commands   space actions   g go";
        s.Put((s.W - hint.Length) / 2, s.H - 1, hint, P.A(P.Surface2));

        if (_popup is not null)
        {
            WhichKey(s);
        }

        if (_palette)
        {
            Palette(s);
        }

        if (_toast is not null)
        {
            var tw = _toast.Length + 4;
            s.Box(s.W - tw - 2, 1, tw, 3, P.A(P.Green), P.A(P.Text));
            s.Put(s.W - tw, 2, _toast, P.A(P.Text));
        }
    }

    /// <summary>Posts laid one after another; the selected one gets a rounded frame and its counts.</summary>
    private void Feed(Surface s, int x0, int cw)
    {
        var posts = Fixtures.Posts;
        var inner = cw - 4;
        var heights = posts.Select((p, i) => PostView.Height(p, inner, stats: i == _sel) + 2).ToList();

        // Scroll so the whole selected card is visible.
        var area = s.H - 3;
        var start = heights.Take(_sel).Sum();
        var end = start + heights[_sel];
        if (start < _top)
        {
            _top = start;
        }
        else if (end > _top + area)
        {
            _top = end - area;
        }

        var y = 2 - _top;

        for (var i = 0; i < posts.Count; i++)
        {
            var h = heights[i];

            if (y + h > 1 && y < s.H - 1)
            {
                if (i == _sel)
                {
                    s.Box(x0, y, cw, h, P.A(P.Mauve), P.A(P.Text));
                }

                PostView.Draw(s, posts[i], x0 + 2, y + 1, inner, h - 2, P.Base, stats: i == _sel);
            }

            y += h;
        }
    }

    private void Notifications(Surface s, int x0, int cw)
    {
        var y = 2;
        string? day = null;

        // Crude scrolling: drop the ones well above the selection.
        for (var i = Math.Max(0, _sel - (s.H - 6) / 3 + 1); i < Fixtures.Notices.Count && y < s.H - 3; i++)
        {
            var n = Fixtures.Notices[i];

            if (n.Day != day)
            {
                day = n.Day;
                y++;
                s.Put(x0, y, day.ToUpperInvariant(), P.B(P.Overlay));
                s.Rule(x0 + day.Length + 1, y, cw - day.Length - 1, P.A(P.Surface0));
                y += 2;
            }

            var sel = i == _sel;
            var bg = sel ? P.Surface0 : P.Base;
            s.Fill(x0, y, cw, 2, P.A(P.Text, bg));
            s.Put(x0 + 1, y, Fixtures.Glyph(n.Kind), P.B(Fixtures.Tint(n.Kind), bg));
            var col = s.Put(x0 + 4, y, n.Who, P.B(P.Text, bg));
            s.Put(x0 + cw - n.Ago.Length - 1, y, n.Ago, P.A(P.Overlay, bg));
            s.Put(x0 + 4, y + 1, n.What, P.A(P.Subtext, bg), cw - 5);
            y += 3;
            _ = col;
        }
    }

    private void Messages(Surface s, int x0, int cw)
    {
        var y = 3;

        for (var i = 0; i < Fixtures.Convos.Count; i++)
        {
            var c = Fixtures.Convos[i];
            var sel = i == _sel;
            var bg = sel ? P.Surface0 : P.Base;
            s.Fill(x0, y, cw, 2, P.A(P.Text, bg));
            s.Put(x0 + 1, y, c.Unread > 0 ? "●" : " ", P.A(P.Red, bg));
            s.Put(x0 + 3, y, c.With, P.B(P.Text, bg));
            s.Put(x0 + cw - c.Ago.Length - 1, y, c.Ago, P.A(P.Overlay, bg));
            s.Put(x0 + 3, y + 1, c.Last, P.A(P.Subtext, bg), cw - 4);
            y += 3;
        }
    }

    private void WhichKey(Surface s)
    {
        (string Key, string What)[] entries = _popup == "g"
            ?
            [
                ("h", "home"), ("l", "local"), ("f", "federated"), ("t", "#rust"), ("n", "notifications"),
                ("m", "messages"), ("d", "discover"), ("p", "profiles"),
            ]
            :
            [
                ("r", "reply"), ("f", "favourite"), ("b", "boost"), ("x", "show warning"), ("o", "open thread"),
                ("a", "author's profile"), ("y", "copy link"), ("e", "open in browser"),
            ];

        var w = 28;
        var h = entries.Length + 2;
        var x = s.W - w - 2;
        var y = s.H - h - 4;
        s.Box(x, y, w, h, P.A(P.Blue), P.A(P.Text, P.Mantle), _popup == "g" ? " Go " : " Post ", P.B(P.Blue, P.Mantle), rounded: false);

        for (var i = 0; i < entries.Length; i++)
        {
            s.Put(x + 2, y + 1 + i, entries[i].Key, P.B(P.Peach, P.Mantle));
            s.Put(x + 5, y + 1 + i, entries[i].What, P.A(P.Text, P.Mantle));
        }
    }

    private List<Command> Matches() =>
        _commands.Where(c => Fuzzy(c.Name, _query)).ToList();

    private static bool Fuzzy(string text, string query)
    {
        var at = 0;

        foreach (var q in query.ToLowerInvariant())
        {
            at = text.ToLowerInvariant().IndexOf(q, at);

            if (at < 0)
            {
                return false;
            }

            at++;
        }

        return true;
    }

    private void Palette(Surface s)
    {
        var matches = Matches();
        var w = Math.Min(64, s.W - 6);
        var h = Math.Min(matches.Count, 10) + 4;
        var x = (s.W - w) / 2;
        var y = Math.Max(1, s.H / 6);
        s.Box(x, y, w, h, P.A(P.Mauve), P.A(P.Text, P.Mantle), " Commands ", P.B(P.Mauve, P.Mantle));
        var col = s.Put(x + 2, y + 1, "› ", P.B(P.Mauve, P.Mantle));
        col = s.Put(col, y + 1, _query, P.A(P.Text, P.Mantle));
        s.Put(col, y + 1, "▏", P.A(P.Mauve, P.Mantle));
        s.Put(x + w - 8, y + 1, $"{matches.Count}/{_commands.Count}", P.A(P.Overlay, P.Mantle));
        s.Rule(x + 1, y + 2, w - 2, P.A(P.Surface0, P.Mantle));

        _paletteSel = Math.Clamp(_paletteSel, 0, Math.Max(0, matches.Count - 1));

        for (var i = 0; i < Math.Min(matches.Count, 10); i++)
        {
            var sel = i == _paletteSel;
            var bg = sel ? P.Surface0 : P.Mantle;
            s.Fill(x + 1, y + 3 + i, w - 2, 1, P.A(P.Text, bg));
            s.Put(x + 2, y + 3 + i, sel ? "▌" : " ", P.A(P.Mauve, bg));
            s.Put(x + 4, y + 3 + i, matches[i].Name, sel ? P.B(P.Text, bg) : P.A(P.Subtext, bg));
            s.Put(x + w - 2 - matches[i].Key.Length - 1, y + 3 + i, matches[i].Key, P.A(P.Overlay, bg));
        }
    }

    private void Act(char ch)
    {
        if (_place is "Notifications" or "Messages")
        {
            return;
        }

        var p = Fixtures.Posts[_sel];

        switch (ch)
        {
            case 'f':
                p.ToggleFav();
                _toast = p.Faved ? $"★ Favourited {p.Name}'s post" : "Unfavourited";
                break;
            case 'b':
                p.ToggleBoost();
                _toast = p.Boosted ? $"↺ Boosted {p.Name}'s post" : "Unboosted";
                break;
            case 'x':
                p.Revealed = !p.Revealed;
                break;
            default:
                _toast = $"{ch}: not in the prototype";
                break;
        }
    }

    public override bool OnKey(Key k)
    {
        var ch = Keys.Ch(k);
        _toast = null;

        if (_palette)
        {
            var matches = Matches();

            if (Keys.Esc(k))
            {
                _palette = false;
            }
            else if (Keys.Enter(k))
            {
                _palette = false;

                if (matches.Count > 0)
                {
                    matches[_paletteSel].Run();
                }
            }
            else if (k.KeyCode == Terminal.Gui.Input.Key.CursorDown.KeyCode || k.KeyCode == Terminal.Gui.Input.Key.N.WithCtrl.KeyCode)
            {
                _paletteSel++;
            }
            else if (k.KeyCode == Terminal.Gui.Input.Key.CursorUp.KeyCode || k.KeyCode == Terminal.Gui.Input.Key.P.WithCtrl.KeyCode)
            {
                _paletteSel = Math.Max(0, _paletteSel - 1);
            }
            else if (Keys.Backspace(k))
            {
                _query = _query.Length > 0 ? _query[..^1] : _query;
                _paletteSel = 0;
            }
            else if (ch >= ' ')
            {
                _query += ch;
                _paletteSel = 0;
            }

            return true;
        }

        if (_popup is not null)
        {
            var popup = _popup;
            _popup = null;

            if (popup == "g")
            {
                var target = ch switch
                {
                    'h' => "Home", 'l' => "Local", 'f' => "Federated", 't' => "#rust", 'n' => "Notifications", 'm' => "Messages",
                    _ => null,
                };

                if (target is not null)
                {
                    Go(target);
                }
                else if (ch is 'd' or 'p')
                {
                    _commands.First(c => c.Key == $"g {ch}").Run();
                }
            }
            else if (ch != '\0' && !Keys.Esc(k))
            {
                Act(ch);
            }

            return true;
        }

        var count = _place switch
        {
            "Notifications" => Fixtures.Notices.Count,
            "Messages" => Fixtures.Convos.Count,
            _ => Fixtures.Posts.Count,
        };

        if (Keys.CtrlP(k) || ch == ':')
        {
            _palette = true;
            _query = "";
            _paletteSel = 0;
        }
        else if (ch == ' ')
        {
            _popup = "space";
        }
        else if (ch == 'g')
        {
            _popup = "g";
        }
        else if (Keys.Down(k))
        {
            _sel = Math.Min(count - 1, _sel + 1);
        }
        else if (Keys.Up(k))
        {
            _sel = Math.Max(0, _sel - 1);
        }
        else if (ch is 'f' or 'b' or 'x')
        {
            Act(ch);
        }
        else
        {
            return false;
        }

        return true;
    }
}
