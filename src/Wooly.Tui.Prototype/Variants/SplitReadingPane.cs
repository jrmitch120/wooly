using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Wooly.Tui.Prototype;

/// <summary>
///     B — Split reading pane. A dense one-line-per-post index on the left, the whole selected post on the right. The
///     shape of a mail reader: you can see forty posts at once and still read one properly, and the right pane is
///     where a thread, an account, or a compose form opens — so nothing has to be modal.
/// </summary>
internal sealed class SplitReadingPane : VariantWindow
{
    private readonly PostPane _pane;

    public SplitReadingPane() : base(1)
    {
        var index = new IndexFeed(Sample.Home)
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(46),
            Height = Dim.Fill(1),
        };

        _pane = new PostPane
        {
            X = Pos.Right(index),
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Item = Sample.Home[0],
        };

        var header = new Header
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        index.SelectionChanged += (_, at) =>
        {
            _pane.Item = Sample.Home[at];
            _pane.SetNeedsDraw();
        };

        var keys = new StatusBar([
            new Shortcut(Key.Enter, "Thread", () => Pretend("Replaced the right pane with the thread")),
            new Shortcut(Key.C, "Compose", () => Pretend("Opened compose in the right pane")),
            new Shortcut(Key.R, "Reply", () => Pretend($"Replying to {_pane.Item!.Readable.Account} in the right pane")),
            new Shortcut(Key.B, "Boost", () => Pretend("Boosted")),
            new Shortcut(Key.F, "Fav", () => Pretend("Favorited")),
            new Shortcut(Key.A, "Account", () => Pretend($"Showed {_pane.Item!.Readable.Account} in the right pane")),
        ])
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
        };

        Canvas.Add(header, index, _pane, keys);

        Canvas.KeyDown += (_, key) =>
        {
            if (key == Key.Tab)
            {
                header.Step(1);
                index.Invalidate();
                key.Handled = true;
            }
            else if (key == Key.D)
            {
                ConfirmDelete(Ink.Clip(_pane.Item!.Readable.Content, 44));
                key.Handled = true;
            }
        };

        Initialized += (_, _) => index.SetFocus();
    }

    private sealed class Header : View
    {
        private int _at;

        public Header() => CanFocus = false;

        public void Step(int by)
        {
            _at = (_at + by + Sample.Timelines.Count) % Sample.Timelines.Count;
            SetNeedsDraw();
        }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;

            SetAttribute(Ink.Chrome);
            AddStr(0, 0, new string(' ', Math.Max(0, width)));
            AddStr(0, 0, Ink.Clip($" Wooly  {Sample.Me}  │  {Sample.Timelines[_at]}  (tab switches)", width));

            var quota = $" quota {Sample.QuotaLeft}/{Sample.QuotaTotal} ";

            if (width > 50)
            {
                AddStr(width - quota.Length, 0, quota);
            }

            return true;
        }
    }
}

/// <summary>The left index: one post per row, so a screenful is forty posts rather than four.</summary>
internal sealed class IndexFeed : LineFeed
{
    public IndexFeed(IReadOnlyList<FeedItem> items) : base(items)
    {
    }

    protected override IReadOnlyList<FeedLine> Compose(int width)
    {
        var lines = new List<FeedLine>();

        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            var post = item.Readable;
            var flags = string.Concat(
                item.Post.IsBoost ? "↺" : " ",
                post.ContentWarning is null ? " " : "⚠",
                item.Images.Count > 0 ? "▒" : " ",
                item.Favorited ? "★" : " ");

            var handle = post.Account.Split('@')[0];
            var head = $"{Ink.Ago(post.PostedAt),3} {flags} {Ink.Clip(handle, 12),-12} ";
            var body = Ink.Clip(post.ContentWarning ?? post.Content, Math.Max(4, width - head.Length - 2));

            lines.Add(new FeedLine(head + body, item.Favorited ? Ink.Favorited : Ink.Body, index));
        }

        return lines;
    }
}

/// <summary>The right pane: one post, whole — warning honoured, media described, counts spelled out.</summary>
internal sealed class PostPane : View
{
    public PostPane() => CanFocus = false;

    public FeedItem? Item { get; set; }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        SetAttribute(Ink.Body);

        for (var row = 0; row < height; row++)
        {
            AddStr(0, row, new string(' ', Math.Max(0, width)));
        }

        SetAttribute(Ink.Dim);

        for (var row = 0; row < height; row++)
        {
            AddStr(0, row, "│");
        }

        if (Item is null || width < 12)
        {
            return true;
        }

        var post = Item.Readable;
        var text = width - 4;
        var line = 1;

        void Put(string content, Attribute attribute)
        {
            if (line >= height)
            {
                return;
            }

            SetAttribute(attribute);

            // Padded, not just clipped: a shorter line has to erase the longer one it replaced.
            AddStr(2, line++, Ink.Clip(content, text).PadRight(text));
        }

        if (Item.Post.IsBoost)
        {
            Put($"↺ boosted by {Item.Post.Author}", Ink.Boosted);
        }

        Put(post.Author, Ink.Author);
        Put($"@{post.Account}", Ink.Handle);
        Put($"{Ink.Ago(post.PostedAt)} ago · {post.Visibility.ToString().ToLowerInvariant()}", Ink.Dim);
        line++;

        if (post.ContentWarning is { } warning)
        {
            Put($"⚠ {warning}", Ink.Warning);
            Put("[x] read it anyway", Ink.Dim);
        }
        else
        {
            foreach (var wrapped in Ink.Wrap(post.Content, text))
            {
                Put(wrapped, Ink.Body);
            }
        }

        foreach (var image in Item.Images)
        {
            line++;
            Put("▒▒▒▒▒▒▒▒▒▒▒▒", Ink.Handle);
            Put("▒▒▒▒▒▒▒▒▒▒▒▒", Ink.Handle);

            foreach (var wrapped in Ink.Wrap($"alt: {image}", text))
            {
                Put(wrapped, Ink.Dim);
            }
        }

        foreach (var link in Item.Links)
        {
            line++;

            foreach (var wrapped in Ink.Wrap($"⏵ {link}", text))
            {
                Put(wrapped, Ink.Handle);
            }
        }

        foreach (var (option, votes) in Item.Poll)
        {
            var share = votes * Math.Max(4, text - 24) / Math.Max(1, Item.Poll.Max(entry => entry.Votes));
            Put($"{new string('█', share)} {votes} · {option}", Ink.Handle);
        }

        line = Math.Max(line, height - 3);
        Put($"↺ {post.Boosts} boosts   ★ {post.Favorites} favorites   ↩ {post.Replies} replies", Ink.Dim);

        return true;
    }
}
