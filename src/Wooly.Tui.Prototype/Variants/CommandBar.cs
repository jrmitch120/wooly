using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Prototype;

/// <summary>
///     D — Command bar. No chrome but the bottom two lines: the posts get the whole terminal, and everything you do
///     you do with a key or by typing the CLI's own verb after a colon (<c>:timeline federated</c>, <c>:boost 3</c>,
///     <c>:dm send maria@fosstodon.org</c>). One vocabulary across both surfaces (spec story 63), nothing to discover
///     twice — and nothing on screen to tell a newcomer any of it exists.
/// </summary>
internal sealed class CommandBar : VariantWindow
{
    private readonly NumberedFeed _feed;
    private readonly Line _line;
    private string _timeline = "home";
    private string _typing = string.Empty;
    private bool _commanding;

    public CommandBar() : base(3)
    {
        _feed = new NumberedFeed(Sample.Home)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };

        _line = new Line
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 2,
            Owner = this,
        };

        Canvas.Add(_feed, _line);

        Canvas.KeyDown += (_, key) =>
        {
            if (_commanding)
            {
                Typing(key);
                key.Handled = true;

                return;
            }

            var rune = key.AsRune.Value;

            if (rune == ':')
            {
                _commanding = true;
                _typing = string.Empty;
                _line.SetNeedsDraw();
                key.Handled = true;
            }
            else if (rune == '/')
            {
                _commanding = true;
                _typing = "search ";
                _line.SetNeedsDraw();
                key.Handled = true;
            }
            else if (rune is >= '1' and <= '9')
            {
                _feed.Select(rune - '1');
                key.Handled = true;
            }
            else if (key == Key.B)
            {
                Ran($"boost {_feed.Current.Readable.Id}");
                key.Handled = true;
            }
            else if (key == Key.F)
            {
                Ran($"favorite {_feed.Current.Readable.Id}");
                key.Handled = true;
            }
            else if (key == Key.R)
            {
                Ran($"reply {_feed.Current.Readable.Id}");
                key.Handled = true;
            }
            else if (key == Key.D)
            {
                ConfirmDelete($"post delete {_feed.Current.Readable.Id}");
                key.Handled = true;
            }
        };

        Initialized += (_, _) => _feed.SetFocus();
    }

    public string Status => _commanding ? $":{_typing}" : $"[{_timeline}] {Sample.Home.Count} posts · post {_feed.Selected + 1} · quota {Sample.QuotaLeft}/{Sample.QuotaTotal}";

    public bool Commanding => _commanding;

    private void Typing(Key key)
    {
        if (key == Key.Esc)
        {
            _commanding = false;
        }
        else if (key == Key.Enter)
        {
            _commanding = false;
            Run(_typing.Trim());
        }
        else if (key == Key.Backspace)
        {
            _typing = _typing.Length > 0 ? _typing[..^1] : _typing;
        }
        else if (key.AsRune.Value >= 32)
        {
            _typing += (char)key.AsRune.Value;
        }

        _line.SetNeedsDraw();
    }

    private void Run(string command)
    {
        var word = command.Split(' ');

        if (word[0] == "timeline" && word.Length > 1)
        {
            _timeline = word[1];
            _feed.Invalidate();
            _line.SetNeedsDraw();

            return;
        }

        Ran(command);
    }

    private void Ran(string command) => Pretend($"mastodon-cli {command}");

    /// <summary>The bottom two rows — the whole of this shell's chrome.</summary>
    private sealed class Line : View
    {
        public Line() => CanFocus = false;

        public CommandBar? Owner { get; set; }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;

            SetAttribute(Ink.Dim);
            AddStr(0, 0, new string('─', Math.Max(0, width)));

            SetAttribute(Owner?.Commanding == true ? Ink.Author : Ink.Dim);
            AddStr(0, 1, new string(' ', Math.Max(0, width)));
            AddStr(0, 1, Ink.Clip(Owner?.Status ?? string.Empty, width));

            if (Owner?.Commanding == true)
            {
                var at = Math.Min(width - 1, (Owner.Status).Length);
                SetAttribute(Ink.RailOn);
                AddStr(at, 1, "▏");
            }

            return true;
        }
    }

    /// <summary>Posts with a number in the gutter, because <c>:boost 3</c> needs something to mean.</summary>
    private sealed class NumberedFeed : LineFeed
    {
        public NumberedFeed(IReadOnlyList<FeedItem> items) : base(items)
        {
        }

        protected override bool MarkSelection => false;

        protected override IReadOnlyList<FeedLine> Compose(int width)
        {
            var lines = new List<FeedLine>();
            var text = Math.Max(10, width - 6);

            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                var post = item.Readable;
                var number = index == Selected ? $"{index + 1,2}▌" : $"{index + 1,2} ";
                var boost = item.Post.IsBoost ? $"↺{item.Post.Account.Split('@')[0]} " : string.Empty;

                lines.Add(new FeedLine(
                    $"{number} {boost}{post.Account} · {post.Author} · {Ink.Ago(post.PostedAt)} · {post.Visibility.ToString().ToLowerInvariant()}",
                    index == Selected ? Ink.Selected : Ink.Handle,
                    index));

                var body = post.ContentWarning is { } warning ? $"⚠ {warning} (:show {index + 1})" : post.Content;

                foreach (var wrapped in Ink.Wrap(body, text))
                {
                    lines.Add(new FeedLine($"    {wrapped}", post.ContentWarning is null ? Ink.Body : Ink.Warning, index));
                }

                foreach (var image in item.Images)
                {
                    lines.Add(new FeedLine($"    ▒▒▒▒ {Ink.Clip(image, text - 8)}", Ink.Handle, index));
                }

                foreach (var (option, votes) in item.Poll)
                {
                    lines.Add(new FeedLine($"    · {votes,4}  {option}", Ink.Handle, index));
                }

                lines.Add(new FeedLine(
                    $"    ↺ {post.Boosts}  ★ {post.Favorites}  ↩ {post.Replies}",
                    item.Favorited || item.BoostedByMe ? Ink.Favorited : Ink.Dim,
                    index));

                lines.Add(new FeedLine(string.Empty, Ink.Dim, index));
            }

            return lines;
        }
    }
}
