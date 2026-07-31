using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Wooly.Tui.Prototype;

/// <summary>
///     A — Tabbed reader. One column of full-width post cards, timelines as tabs across the top, and every other
///     screen (notifications, DMs, search, an account) arriving as a modal window over the top of it. The shape most
///     terminal clients land on, and the cheapest to grow: #29 and #30 each become another modal.
/// </summary>
internal sealed class TabbedReader : VariantWindow
{
    private readonly Label _title;

    public TabbedReader() : base(0)
    {
        _title = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = $" Wooly — {Sample.Me}   ·   home",
            SchemeName = "Menu",
        };

        var strip = new TabStrip
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
        };

        var feed = new CardFeed(Sample.Home)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        strip.Changed += (_, _) =>
        {
            _title.Text = $" Wooly — {Sample.Me}   ·   {Sample.Timelines[strip.Index].ToLowerInvariant()}";
            feed.Invalidate();
        };

        var keys = new StatusBar([
            new Shortcut(Key.Enter, "Thread", () => Pretend($"Opened the thread on post {feed.Current.Readable.Id}")),
            new Shortcut(Key.C, "Compose", () => Pretend("Opened the compose window")),
            new Shortcut(Key.R, "Reply", () => Pretend($"Replied to {feed.Current.Readable.Account}")),
            new Shortcut(Key.B, "Boost", () => Pretend("Boosted")),
            new Shortcut(Key.F, "Fav", () => Pretend("Favorited")),
            new Shortcut(Key.N, "Notifs", ShowNotifications),
            new Shortcut(Key.M, "DMs", ShowDirectMessages),
        ])
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
        };

        Canvas.Add(_title, strip, feed, keys);

        Canvas.KeyDown += (_, key) =>
        {
            if (key == Key.Tab.WithShift)
            {
                strip.Step(-1);
                key.Handled = true;
            }
            else if (key == Key.Tab)
            {
                strip.Step(1);
                key.Handled = true;
            }
            else if (key == Key.D)
            {
                ConfirmDelete(feed.Current.Mine
                    ? Ink.Clip(feed.Current.Readable.Content, 44)
                    : "Not your post — the shell would not offer this.");

                key.Handled = true;
            }
            else if (key.AsRune.Value == '/')
            {
                Pretend("Opened the search prompt as a modal window");
                key.Handled = true;
            }
        };

        Initialized += (_, _) => feed.SetFocus();
    }

    private void ShowNotifications()
    {
        var lines = Sample.Notifications.Select(notification =>
            $"{notification.Kind.Name,-9} {notification.Author,-14} {Ink.Ago(notification.ReceivedAt),3}  "
            + Ink.Clip(notification.Post?.Content ?? "started following you", 30));

        MessageBox.Query(GetApp()!, 70, 12, "Notifications (modal)", string.Join('\n', lines) + "\n\nd dismiss · D clear all", "Close");
    }

    private void ShowDirectMessages()
    {
        var lines = Sample.Conversations.Select(conversation =>
            $"{(conversation.Unread ? "●" : " ")} {string.Join(", ", conversation.With),-38} "
            + $"{Ink.Ago(conversation.Latest!.PostedAt),3}  {Ink.Clip(conversation.Latest!.Content, 20)}");

        MessageBox.Query(GetApp()!, 70, 10, "Direct messages (modal)", string.Join('\n', lines), "Close");
    }
}

/// <summary>The timeline switcher: tabs, drawn as tabs, with the current one lit.</summary>
internal sealed class TabStrip : View
{
    public TabStrip() => CanFocus = false;

    public int Index { get; private set; }

    public event EventHandler<int>? Changed;

    public void Step(int by)
    {
        Index = (Index + by + Sample.Timelines.Count) % Sample.Timelines.Count;
        Changed?.Invoke(this, Index);
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;

        SetAttribute(Ink.Dim);
        AddStr(0, 0, new string('─', Math.Max(0, width)));

        var column = 1;

        for (var index = 0; index < Sample.Timelines.Count; index++)
        {
            var label = index == Index ? $"▸{Sample.Timelines[index]} " : $" {Sample.Timelines[index]} ";

            SetAttribute(index == Index ? Ink.RailOn : Ink.Dim);
            AddStr(column, 0, label);
            column += label.Length + 1;
        }

        var hint = " tab / shift-tab ";

        if (width > column + hint.Length)
        {
            SetAttribute(Ink.Dim);
            AddStr(width - hint.Length, 0, hint);
        }

        return true;
    }
}

/// <summary>Full-width cards: header, wrapped body, then the counts. What a phone client looks like in a terminal.</summary>
internal sealed class CardFeed : LineFeed
{
    public CardFeed(IReadOnlyList<FeedItem> items) : base(items)
    {
    }

    protected override IReadOnlyList<FeedLine> Compose(int width)
    {
        var lines = new List<FeedLine>();
        var text = Math.Max(10, width - 5);

        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            var post = item.Readable;

            if (item.Post.IsBoost)
            {
                lines.Add(new FeedLine($"  ↺ {item.Post.Author} boosted", Ink.Boosted, index));
            }

            if (item.Pinned)
            {
                lines.Add(new FeedLine("  📌 pinned", Ink.Warning, index));
            }

            var head = $" {post.Author}  @{post.Account}";
            var tail = $"{Ink.Audience(post.Visibility)} {Ink.Ago(post.PostedAt)} ";
            lines.Add(new FeedLine(head.PadRight(Math.Max(0, width - 2 - tail.Length)) + tail, Ink.Author, index));

            if (post.ContentWarning is { } warning)
            {
                lines.Add(new FeedLine($"  ⚠ {warning}  — press x to read", Ink.Warning, index));
            }
            else
            {
                foreach (var line in Ink.Wrap(post.Content, text))
                {
                    lines.Add(new FeedLine($"  {line}", Ink.Body, index));
                }
            }

            foreach (var image in item.Images)
            {
                lines.Add(new FeedLine($"  ▒▒▒▒ {Ink.Clip(image, text - 8)}", Ink.Handle, index));
            }

            foreach (var link in item.Links)
            {
                lines.Add(new FeedLine($"  ⏵ {Ink.Clip(link, text - 4)}", Ink.Handle, index));
            }

            foreach (var (option, votes) in item.Poll)
            {
                var share = votes * 18 / Math.Max(1, item.Poll.Max(entry => entry.Votes));
                lines.Add(new FeedLine($"  {new string('█', share).PadRight(18, '·')} {votes,4}  {option}", Ink.Handle, index));
            }

            var marks = $"  ↺ {post.Boosts}   ★ {post.Favorites}   ↩ {post.Replies}";
            lines.Add(new FeedLine(marks, item.Favorited || item.BoostedByMe ? Ink.Favorited : Ink.Dim, index));
            lines.Add(new FeedLine(new string('─', Math.Max(0, width - 2)), Ink.Dim, index));
        }

        return lines;
    }
}
