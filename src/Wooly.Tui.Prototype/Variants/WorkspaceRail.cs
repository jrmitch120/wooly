using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Prototype;

/// <summary>
///     C — Workspace rail. A permanent rail of destinations down the left, the feed in the middle, and a context pane
///     on the right that follows the selection. Nothing is ever modal: notifications, DMs, follow requests and search
///     are rail entries rather than screens you leave the timeline for, and unread counts and the rate-limit quota are
///     always on screen. The most room to grow (#29, #30 are rail entries) and the most chrome to pay for it.
/// </summary>
internal sealed class WorkspaceRail : VariantWindow
{
    private readonly Rail _rail;
    private readonly RailFeed _feed;
    private readonly SectionPane _section;
    private readonly ContextPane _context;

    public WorkspaceRail() : base(2)
    {
        _rail = new Rail
        {
            X = 0,
            Y = 0,
            Width = 18,
            Height = Dim.Fill(1),
        };

        _feed = new RailFeed(Sample.Home)
        {
            X = 19,
            Y = 0,
            Width = Dim.Fill(24),
            Height = Dim.Fill(1),
        };

        _section = new SectionPane
        {
            X = 19,
            Y = 0,
            Width = Dim.Fill(24),
            Height = Dim.Fill(1),
            Visible = false,
        };

        _context = new ContextPane
        {
            X = Pos.AnchorEnd(23),
            Y = 0,
            Width = 23,
            Height = Dim.Fill(1),
            Item = Sample.Home[0],
        };

        var keys = new KeyLine
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };

        _feed.SelectionChanged += (_, at) =>
        {
            _context.Item = Sample.Home[at];
            _context.SetNeedsDraw();
        };

        _rail.Changed += (_, _) =>
        {
            var timeline = _rail.At < 4;
            _feed.Visible = timeline;
            _section.Visible = !timeline;
            _section.Destination = _rail.Label;
            _context.Destination = _rail.Label;

            if (timeline)
            {
                _feed.SetFocus();
            }

            SetNeedsDraw();
        };

        Canvas.Add(_rail, _feed, _section, _context, keys);

        Canvas.KeyDown += (_, key) =>
        {
            if (key == Key.Tab)
            {
                _rail.Step(1);
                key.Handled = true;
            }
            else if (key == Key.Tab.WithShift)
            {
                _rail.Step(-1);
                key.Handled = true;
            }
            else if (key == Key.C)
            {
                Pretend("Compose opened in the right-hand pane — the rail and feed stay put");
                key.Handled = true;
            }
            else if (key == Key.D)
            {
                ConfirmDelete(_feed.Current.Mine
                    ? Ink.Clip(_feed.Current.Readable.Content, 44)
                    : "Not your post — the rail would not offer this.");

                key.Handled = true;
            }
            else if (key == Key.R || key == Key.B || key == Key.F)
            {
                Pretend($"{key} on post {_feed.Current.Readable.Id}");
                key.Handled = true;
            }
        };

        Initialized += (_, _) => _feed.SetFocus();
    }

    /// <summary>The destinations, always visible, carrying their own unread counts.</summary>
    private sealed class Rail : View
    {
        private static readonly (string Label, string Badge)[] Entries =
        [
            ("Home", ""),
            ("Local", ""),
            ("Federated", ""),
            ("#dotnet", ""),
            ("─", ""),
            ("Notifications", "4"),
            ("Direct messages", "1"),
            ("Follow requests", "2"),
            ("Search", ""),
            ("─", ""),
            ($"@{Sample.Me.Split('@')[0]}", ""),
        ];

        public Rail() => CanFocus = false;

        public int At { get; private set; }

        public string Label => Entries[At].Label;

        public event EventHandler<int>? Changed;

        public void Step(int by)
        {
            do
            {
                At = (At + by + Entries.Length) % Entries.Length;
            }
            while (Entries[At].Label == "─");

            Changed?.Invoke(this, At);
            SetNeedsDraw();
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Rail);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
            }

            for (var index = 0; index < Entries.Length && index < height; index++)
            {
                var (label, badge) = Entries[index];

                if (label == "─")
                {
                    SetAttribute(Ink.Dim);
                    AddStr(0, index, new string('─', width));

                    continue;
                }

                SetAttribute(index == At ? Ink.RailOn : Ink.Rail);
                AddStr(0, index, $"{(index == At ? "▸" : " ")}{Ink.Clip(label, width - 5)}".PadRight(width));

                if (badge.Length > 0)
                {
                    SetAttribute(index == At ? Ink.RailOn : Ink.Badge);
                    AddStr(width - 2, index, badge);
                }
            }

            SetAttribute(Ink.Dim);
            AddStr(0, height - 2, new string('─', width));
            SetAttribute(Sample.QuotaLeft < 60 ? Ink.Badge : Ink.Dim);
            AddStr(0, height - 1, $" {Sample.QuotaLeft}/{Sample.QuotaTotal} left");

            return true;
        }
    }

    /// <summary>Compact cards — two lines and the counts, so the middle column still shows a dozen posts.</summary>
    private sealed class RailFeed : LineFeed
    {
        public RailFeed(IReadOnlyList<FeedItem> items) : base(items)
        {
        }

        protected override IReadOnlyList<FeedLine> Compose(int width)
        {
            var lines = new List<FeedLine>();
            var text = Math.Max(10, width - 4);

            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                var post = item.Readable;
                var boost = item.Post.IsBoost ? $"↺{item.Post.Author.Split(' ')[0]} " : string.Empty;
                var head = $"{boost}{post.Author} @{post.Account.Split('@')[0]}";
                var tail = $"{Ink.Ago(post.PostedAt)} ";

                lines.Add(new FeedLine(
                    Ink.Clip(head, Math.Max(1, text - tail.Length)).PadRight(Math.Max(0, width - 2 - tail.Length)) + tail,
                    Ink.Author,
                    index));

                var body = post.ContentWarning is { } warning ? $"⚠ {warning}" : post.Content;

                foreach (var wrapped in Ink.Wrap(body, text).Take(2))
                {
                    lines.Add(new FeedLine(wrapped, post.ContentWarning is null ? Ink.Body : Ink.Warning, index));
                }

                if (item.Images.Count > 0)
                {
                    lines.Add(new FeedLine($"▒▒▒▒ {Ink.Clip(item.Images[0], text - 6)}", Ink.Handle, index));
                }

                lines.Add(new FeedLine(
                    $"↺{post.Boosts} ★{post.Favorites} ↩{post.Replies}",
                    item.Favorited || item.BoostedByMe ? Ink.Favorited : Ink.Dim,
                    index));

                lines.Add(new FeedLine(string.Empty, Ink.Dim, index));
            }

            return lines;
        }
    }

    /// <summary>What the middle column shows when the rail is on something that is not a timeline.</summary>
    private sealed class SectionPane : View
    {
        public SectionPane() => CanFocus = false;

        public string Destination { get; set; } = "Home";

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Body);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
            }

            var row2 = 0;

            void Put(string content, Attribute attribute)
            {
                if (row2 >= height)
                {
                    return;
                }

                SetAttribute(attribute);
                AddStr(1, row2++, Ink.Clip(content, width - 2));
            }

            Put(Destination.ToUpperInvariant(), Ink.Author);
            Put(string.Empty, Ink.Dim);

            switch (Destination)
            {
                case "Notifications":
                    foreach (var notification in Sample.Notifications)
                    {
                        Put($"{Glyph(notification.Kind.Name)} {notification.Author}  ·  {Ink.Ago(notification.ReceivedAt)}", Ink.Author);
                        Put($"   {Ink.Clip(notification.Post?.Content ?? "started following you", width - 6)}", Ink.Dim);
                        Put(string.Empty, Ink.Dim);
                    }

                    Put("d dismiss one · D clear all", Ink.Dim);

                    break;

                case "Direct messages":
                    foreach (var conversation in Sample.Conversations)
                    {
                        Put($"{(conversation.Unread ? "●" : " ")} {string.Join(", ", conversation.With)}", conversation.Unread ? Ink.Author : Ink.Body);
                        Put($"   {Ink.Ago(conversation.Latest!.PostedAt)}  {Ink.Clip(conversation.Latest!.Content, width - 10)}", Ink.Dim);
                        Put(string.Empty, Ink.Dim);
                    }

                    break;

                case "Follow requests":
                    foreach (var (account, author, note) in Sample.Requests)
                    {
                        Put($"{author}  @{account}", Ink.Author);
                        Put($"   {note}", Ink.Dim);
                        Put("   [a]ccept   [r]eject", Ink.Handle);
                        Put(string.Empty, Ink.Dim);
                    }

                    break;

                case "Search":
                    Put("┌────────────────────────────────────┐", Ink.Dim);
                    Put("│ sixel                              │", Ink.Body);
                    Put("└────────────────────────────────────┘", Ink.Dim);
                    Put(string.Empty, Ink.Dim);
                    Put("accounts · hashtags · posts (tab to filter)", Ink.Dim);

                    break;

                default:
                    Put($"{Sample.MyName}  @{Sample.Me}", Ink.Author);
                    Put("412 posts · 388 following · 1,204 followers", Ink.Dim);

                    break;
            }

            return true;
        }

        private static string Glyph(string kind) => kind switch
        {
            "mention" => "@",
            "follow" => "+",
            "boost" => "↺",
            "favorite" => "★",
            _ => "·",
        };
    }

    /// <summary>The right-hand pane: who wrote the selected post, where you stand with them, what you can do.</summary>
    private sealed class ContextPane : View
    {
        public ContextPane() => CanFocus = false;

        public FeedItem? Item { get; set; }

        public string Destination { get; set; } = "Home";

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Body);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
                SetAttribute(Ink.Dim);
                AddStr(0, row, "│");
                SetAttribute(Ink.Body);
            }

            if (Item is null)
            {
                return true;
            }

            var post = Item.Readable;
            var row2 = 1;

            void Put(string content, Attribute attribute)
            {
                if (row2 >= height)
                {
                    return;
                }

                SetAttribute(attribute);
                AddStr(2, row2++, Ink.Clip(content, width - 3));
            }

            Put(post.Author, Ink.Author);
            Put($"@{post.Account}", Ink.Handle);
            Put(string.Empty, Ink.Dim);
            Put("follows you", Ink.Boosted);
            Put("you follow them", Ink.Boosted);
            Put(string.Empty, Ink.Dim);
            Put("[F] unfollow", Ink.Body);
            Put("[M] mute", Ink.Body);
            Put("[B] block", Ink.Body);
            Put(string.Empty, Ink.Dim);
            Put("── this post ──", Ink.Dim);
            Put($"{Ink.Ago(post.PostedAt)} · {post.Visibility.ToString().ToLowerInvariant()}", Ink.Dim);
            Put($"↺ {post.Boosts}", Item.BoostedByMe ? Ink.Boosted : Ink.Dim);
            Put($"★ {post.Favorites}", Item.Favorited ? Ink.Favorited : Ink.Dim);
            Put($"↩ {post.Replies}", Ink.Dim);

            if (Item.Mine)
            {
                Put(string.Empty, Ink.Dim);
                Put("[e] edit", Ink.Body);
                Put("[p] pin", Ink.Body);
                Put("[d] delete", Ink.Warning);
            }

            return true;
        }
    }

    private sealed class KeyLine : View
    {
        public KeyLine() => CanFocus = false;

        protected override bool OnDrawingContent(DrawContext? context)
        {
            SetAttribute(Ink.Chrome);
            AddStr(0, 0, new string(' ', Viewport.Width));
            AddStr(0, 0, Ink.Clip(" tab destination · j/k post · c compose · r reply · b boost · f fav · d delete", Viewport.Width));

            return true;
        }
    }
}
