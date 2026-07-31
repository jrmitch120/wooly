using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Prototype;

/// <summary>Where the rail can send you.</summary>
internal enum Stop
{
    Home,
    Local,
    Federated,
    Tag,
    Notifications,
    Messages,
    Requests,
    Search,
    Profile,
}

internal sealed record Destination(Stop Stop, string Label, string Key, string Badge = "");

/// <summary>What the content area is showing: the feed, one post drilled into, or one account.</summary>
internal abstract record Place;

internal sealed record FeedPlace : Place;

internal sealed record PostPlace(FeedItem Item) : Place;

internal sealed record AccountPlace(string Account, string Author) : Place;

/// <summary>
///     A fake instance with a fake latency, and — the point of this round — a count of how many times it was asked.
///     Every destination change costs one fetch; a selection model that walks through destinations on the way to the
///     one you wanted pays for every step. A fetch that is overtaken before it lands is discarded, which is the
///     flicker you are trying not to build.
/// </summary>
internal sealed class Fetcher
{
    private readonly List<string> _log = [];
    private int _token;

    public Stop Showing { get; private set; } = Stop.Home;

    public Stop Wanted { get; private set; } = Stop.Home;

    public bool Loading { get; private set; }

    /// <summary>How many times an instance was asked for something since the shell opened.</summary>
    public int Fetches { get; private set; }

    /// <summary>How many of those were thrown away because the user had already moved on.</summary>
    public int Wasted { get; private set; }

    public IReadOnlyList<string> Log => _log;

    public event EventHandler? Changed;

    /// <summary>Asks for a destination. Returns the token the answer has to come back with, or 0 for "already there".</summary>
    public int Begin(Stop stop, string label)
    {
        if (stop == Wanted && !Loading)
        {
            return 0;
        }

        if (Loading)
        {
            Wasted++;
        }

        Wanted = stop;
        Loading = true;
        Fetches++;

        _log.Insert(0, label.ToLowerInvariant());

        if (_log.Count > 4)
        {
            _log.RemoveAt(4);
        }

        Changed?.Invoke(this, EventArgs.Empty);

        return ++_token;
    }

    /// <summary>Lands an answer, unless the user has already asked for something else since.</summary>
    public void Land(int token, Stop stop)
    {
        if (token != _token)
        {
            return;
        }

        Showing = stop;
        Loading = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
///     The C family: the rail you liked, no right-hand column, and a post or an account opened by drilling into a list
///     item rather than by watching a pane change beside it.
///     <para>
///         All four of these are the same design. The one thing that differs is how you choose a destination — which is
///         the thing to judge, because a rail you walk through fetches everything you walk past. Watch the fetch count
///         on the bottom row.
///     </para>
/// </summary>
internal abstract class RailShell : VariantWindow
{
    protected static readonly Destination[] Stops =
    [
        new(Stop.Home, "Home", "1"),
        new(Stop.Local, "Local", "2"),
        new(Stop.Federated, "Federated", "3"),
        new(Stop.Tag, "#dotnet", "4"),
        new(Stop.Notifications, "Notifications", "n", "4"),
        new(Stop.Messages, "Direct messages", "d", "1"),
        new(Stop.Requests, "Follow requests", "q", "2"),
        new(Stop.Search, "Search", "s"),
        new(Stop.Profile, "@jeff", "p"),
    ];

    private readonly Stack<Place> _places = new();
    private readonly RailView _rail;
    private readonly HeaderLine _header;
    private readonly StatusLine _status;
    private readonly WideFeed _feed;
    private readonly PostScreen _post;
    private readonly AccountScreen _account;
    private readonly OverlayView _overlay;

    protected RailShell(int index) : base(index)
    {
        _places.Push(new FeedPlace());

        Fetch = new Fetcher();

        _rail = new RailView(this)
        {
            X = 0,
            Y = 0,
            Width = 18,
            Height = Dim.Fill(1),
        };

        _header = new HeaderLine(this)
        {
            X = 19,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        _feed = new WideFeed(Sample.Home, this)
        {
            X = 19,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        _post = new PostScreen(this)
        {
            X = 19,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };

        _account = new AccountScreen(this)
        {
            X = 19,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
        };

        _status = new StatusLine(this)
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };

        // An opaque panel added last, rather than a transparent full-size layer: subviews draw in the order they
        // were added, and a small opaque one is the reliable way to land on top.
        _overlay = new OverlayView(this)
        {
            X = 21,
            Y = 3,
            Width = 46,
            Height = 1,
            Visible = false,
        };

        Canvas.Add(_rail, _header, _feed, _post, _account, _status, _overlay);

        Fetch.Changed += (_, _) => Redraw();

        // Installed on the feed as well as the container: the feed is what has focus, and what it handles never
        // reaches an ancestor's KeyDown at all.
        _feed.Intercept = Keymap;

        Canvas.KeyDown += (_, key) =>
        {
            if (Keymap(key))
            {
                key.Handled = true;
            }
        };

        Initialized += (_, _) => _feed.SetFocus();
    }

    protected Fetcher Fetch { get; }

    /// <summary>What is on screen right now.</summary>
    protected Place Here => _places.Peek();

    /// <summary>The line under the rail that says how this shell is driven.</summary>
    protected abstract string Hint { get; }

    /// <summary>What this shell draws in front of a rail entry — a cursor, a key, an arrow.</summary>
    protected abstract string Prefix(int index);

    /// <summary>Whether this destination is under the cursor but not asked for yet — waiting on the user, not the wire.</summary>
    protected virtual bool Pending(int index) => false;

    /// <summary>An overlay this shell wants drawn over the content, or nothing.</summary>
    protected virtual IReadOnlyList<string> Overlay => [];

    /// <summary>The shell's own selection keys. Returns whether the key was one of them.</summary>
    protected abstract bool RailKey(Key key);

    /// <summary>Which destination is lit as current.</summary>
    protected int ShowingAt => Array.FindIndex(Stops, stop => stop.Stop == Fetch.Showing);

    /// <summary>Asks the fake instance for something, and lands the answer a fake network later.</summary>
    private void Ask(Stop stop, string label)
    {
        var token = Fetch.Begin(stop, label);

        if (token == 0)
        {
            return;
        }

        GetApp()?.AddTimeout(TimeSpan.FromMilliseconds(450), () =>
        {
            Fetch.Land(token, stop);

            return false;
        });
    }

    protected void Redraw()
    {
        _rail.SetNeedsDraw();
        _header.SetNeedsDraw();
        _feed.SetNeedsDraw();
        _post.SetNeedsDraw();
        _account.SetNeedsDraw();
        _status.SetNeedsDraw();
        var overlay = Overlay;
        _overlay.Visible = overlay.Count > 0;
        _overlay.Height = Math.Max(1, overlay.Count);
        _overlay.SetNeedsDraw();
        SetNeedsDraw();
    }

    /// <summary>Goes to a destination — which drops whatever you had drilled into, and costs a fetch.</summary>
    protected void Go(int at)
    {
        var stop = Stops[Math.Clamp(at, 0, Stops.Length - 1)];

        _places.Clear();
        _places.Push(new FeedPlace());
        _feed.Visible = true;
        _post.Visible = false;
        _account.Visible = false;

        Ask(stop.Stop, stop.Label);
        _feed.SetFocus();
        Redraw();
    }

    private bool Keymap(Key key) => RailKey(key) || ContentKey(key);

    private bool ContentKey(Key key)
    {
        if (key == Key.Enter && Here is FeedPlace)
        {
            Push(new PostPlace(_feed.Current));

            return true;
        }

        if (key == Key.A)
        {
            var post = Here switch
            {
                FeedPlace => _feed.Current.Readable,
                PostPlace place => place.Item.Readable,
                _ => null,
            };

            if (post is not null)
            {
                Push(new AccountPlace(post.Account, post.Author));

                return true;
            }
        }

        if (key == Key.Esc || key == Key.Backspace)
        {
            Pop();

            return true;
        }

        if (key == Key.B || key == Key.F || key == Key.R)
        {
            Pretend($"{key} on {(Here is AccountPlace account ? account.Account : _feed.Current.Readable.Id)}");

            return true;
        }

        return false;
    }

    private void Push(Place place)
    {
        _places.Push(place);
        _post.Place = place as PostPlace;
        _account.Place = place as AccountPlace;
        _feed.Visible = place is FeedPlace;
        _post.Visible = place is PostPlace;
        _account.Visible = place is AccountPlace;

        if (place is not FeedPlace)
        {
            // Drilling in is a fetch too — the thread and the account are things the instance has to be asked for.
            Ask(Fetch.Showing, place is PostPlace ? "thread" : "account");
        }

        Redraw();
    }

    private void Pop()
    {
        if (_places.Count > 1)
        {
            _places.Pop();
        }

        var place = _places.Peek();
        _post.Place = place as PostPlace;
        _account.Place = place as AccountPlace;
        _feed.Visible = place is FeedPlace;
        _post.Visible = place is PostPlace;
        _account.Visible = place is AccountPlace;

        if (place is FeedPlace)
        {
            _feed.SetFocus();
        }

        Redraw();
    }

    /// <summary>The trail of what you drilled through, which is what the Esc key walks back up.</summary>
    private string Breadcrumb()
    {
        var trail = _places.Reverse().Select(place => place switch
        {
            PostPlace post => $"post by @{post.Item.Readable.Account.Split('@')[0]}",
            AccountPlace account => $"@{account.Account}",
            _ => Stops[Math.Max(0, ShowingAt)].Label.ToLowerInvariant(),
        });

        return string.Join(" › ", trail);
    }

    private sealed class RailView(RailShell shell) : View
    {
        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Rail);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
            }

            var row2 = 0;

            for (var index = 0; index < Stops.Length && row2 < height - 3; index++)
            {
                if (index is 4 or 8)
                {
                    SetAttribute(Ink.Dim);
                    AddStr(0, row2++, new string('─', width));
                }

                var (stop, label, key, badge) = Stops[index];
                var current = stop == shell.Fetch.Showing;
                var waiting = shell.Fetch.Loading && stop == shell.Fetch.Wanted;

                SetAttribute(current ? Ink.RailOn : Ink.Rail);
                AddStr(0, row2, $"{shell.Prefix(index)}{Ink.Clip(label, width - 5)}".PadRight(width));

                if (shell.Pending(index))
                {
                    SetAttribute(Ink.Handle);
                    AddStr(width - 2, row2, "◌");
                }
                else if (waiting)
                {
                    SetAttribute(Ink.Warning);
                    AddStr(width - 2, row2, "◴");
                }
                else if (badge.Length > 0)
                {
                    SetAttribute(current ? Ink.RailOn : Ink.Badge);
                    AddStr(width - 2, row2, badge);
                }

                row2++;
            }

            SetAttribute(Ink.Dim);
            AddStr(0, height - 2, new string('─', width));
            AddStr(0, height - 1, Ink.Clip($" {Sample.QuotaLeft}/{Sample.QuotaTotal} left", width));

            return true;
        }
    }

    private sealed class HeaderLine(RailShell shell) : View
    {
        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;

            SetAttribute(Ink.Chrome);
            AddStr(0, 0, new string(' ', Math.Max(0, width)));
            AddStr(0, 0, Ink.Clip($" {shell.Breadcrumb()}", width));

            var state = shell.Fetch.Loading ? " fetching… " : " ";

            if (width > 30)
            {
                AddStr(width - state.Length, 0, state);
            }

            return true;
        }
    }

    private sealed class StatusLine(RailShell shell) : View
    {
        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;

            SetAttribute(Ink.Chrome);
            AddStr(0, 0, new string(' ', Math.Max(0, width)));
            AddStr(0, 0, Ink.Clip($" {shell.Hint}", width));

            var count = shell.Fetch.Wasted > 0
                ? $" fetches {shell.Fetch.Fetches} · {shell.Fetch.Wasted} thrown away "
                : $" fetches {shell.Fetch.Fetches} ";

            if (width > shell.Hint.Length + count.Length + 2)
            {
                SetAttribute(shell.Fetch.Wasted > 0 ? Ink.Badge : Ink.Chrome);
                AddStr(width - count.Length, 0, count);
            }

            return true;
        }
    }

    /// <summary>The feed with the whole width to itself, now that nothing sits to the right of it.</summary>
    private sealed class WideFeed(IReadOnlyList<FeedItem> items, RailShell shell) : LineFeed(items)
    {
        protected override IReadOnlyList<FeedLine> Compose(int width)
        {
            var lines = new List<FeedLine>();
            var text = Math.Max(10, width - 4);
            var loading = shell.Fetch.Loading;
            var body = loading ? Ink.Dim : Ink.Body;
            var author = loading ? Ink.Dim : Ink.Author;

            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                var post = item.Readable;
                var boost = item.Post.IsBoost ? $"↺{item.Post.Author.Split(' ')[0]} · " : string.Empty;
                var tail = $"{Ink.Audience(post.Visibility)} {Ink.Ago(post.PostedAt)} ";
                var head = $"{boost}{post.Author} @{post.Account}";

                lines.Add(new FeedLine(
                    Ink.Clip(head, Math.Max(1, text - tail.Length)).PadRight(Math.Max(0, width - 2 - tail.Length)) + tail,
                    author,
                    index));

                var content = post.ContentWarning is { } warning ? $"⚠ {warning}" : post.Content;

                foreach (var wrapped in Ink.Wrap(content, text).Take(3))
                {
                    lines.Add(new FeedLine(wrapped, post.ContentWarning is null ? body : Ink.Warning, index));
                }

                if (item.Images.Count > 0)
                {
                    lines.Add(new FeedLine($"▒▒▒▒ {Ink.Clip(item.Images[0], text - 6)}", loading ? Ink.Dim : Ink.Handle, index));
                }

                lines.Add(new FeedLine(
                    $"↺ {post.Boosts}   ★ {post.Favorites}   ↩ {post.Replies}    ⏎ read · a author",
                    item.Favorited || item.BoostedByMe ? Ink.Favorited : Ink.Dim,
                    index));

                lines.Add(new FeedLine(string.Empty, Ink.Dim, index));
            }

            return lines;
        }
    }

    /// <summary>Drilled into one post: the whole thing, then what was said back.</summary>
    private sealed class PostScreen(RailShell shell) : View
    {
        public PostPlace? Place { get; set; }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Body);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
            }

            if (Place is null)
            {
                return true;
            }

            var post = Place.Item.Readable;
            var text = width - 4;
            var row2 = 1;

            void Put(string content, Attribute attribute)
            {
                if (row2 >= height)
                {
                    return;
                }

                SetAttribute(shell.Fetch.Loading ? Ink.Dim : attribute);
                AddStr(2, row2++, Ink.Clip(content, text).PadRight(text));
            }

            Put($"{post.Author}  @{post.Account}", Ink.Author);
            Put($"{Ink.Ago(post.PostedAt)} ago · {post.Visibility.ToString().ToLowerInvariant()}", Ink.Dim);
            row2++;

            foreach (var wrapped in Ink.Wrap(post.ContentWarning is { } warning ? $"⚠ {warning}" : post.Content, text))
            {
                Put(wrapped, post.ContentWarning is null ? Ink.Body : Ink.Warning);
            }

            foreach (var image in Place.Item.Images)
            {
                row2++;
                Put("▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒", Ink.Handle);
                Put("▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒", Ink.Handle);
                Put($"alt: {image}", Ink.Dim);
            }

            row2++;
            Put($"↺ {post.Boosts} boosts   ★ {post.Favorites} favorites   ↩ {post.Replies} replies", Ink.Dim);
            row2++;
            Put("── replies ──", Ink.Dim);

            foreach (var (who, said) in Replies)
            {
                Put($"{who}", Ink.Author);
                Put($"  {said}", Ink.Body);
                row2++;
            }

            Put("a author · b boost · f favorite · r reply · esc back", Ink.Dim);

            return true;
        }

        private static (string Who, string Said)[] Replies =>
        [
            ("ben@hachyderm.io", "does it do sixel or is that still on the pile"),
            ("hazel@mastodon.art", "the instance-based model is the bit I keep telling people about"),
            ("theo@merveilles.town", "congratulations, genuinely"),
        ];
    }

    /// <summary>Drilled into one account: who they are, where you stand, what they have been saying.</summary>
    private sealed class AccountScreen(RailShell shell) : View
    {
        public AccountPlace? Place { get; set; }

        protected override bool OnDrawingContent(DrawContext? context)
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            SetAttribute(Ink.Body);

            for (var row = 0; row < height; row++)
            {
                AddStr(0, row, new string(' ', width));
            }

            if (Place is null)
            {
                return true;
            }

            var text = width - 4;
            var row2 = 1;

            void Put(string content, Attribute attribute)
            {
                if (row2 >= height)
                {
                    return;
                }

                SetAttribute(shell.Fetch.Loading ? Ink.Dim : attribute);
                AddStr(2, row2++, Ink.Clip(content, text).PadRight(text));
            }

            Put(Place.Author, Ink.Author);
            Put($"@{Place.Account}", Ink.Handle);
            row2++;
            Put("Writes about terminals, .NET and the slow art of making a CLI feel designed on purpose.", Ink.Body);
            row2++;
            Put("412 posts · 388 following · 1,204 followers", Ink.Dim);
            Put("follows you · you follow them", Ink.Boosted);
            row2++;
            Put("[f] unfollow    [m] mute    [b] block", Ink.Body);
            row2++;
            Put("── their posts ──", Ink.Dim);
            row2++;

            foreach (var item in Sample.Home.Where(entry => entry.Readable.Account == Place.Account).Take(3))
            {
                Put(Ink.Ago(item.Readable.PostedAt) + "  " + Ink.Clip(item.Readable.Content, text - 6), Ink.Body);
                row2++;
            }

            Put("esc back", Ink.Dim);

            return true;
        }
    }

    /// <summary>Draws whatever overlay the shell asked for, on top of everything else.</summary>
    private sealed class OverlayView(RailShell shell) : View
    {
        protected override bool OnDrawingContent(DrawContext? context)
        {
            var overlay = shell.Overlay;
            var width = Viewport.Width;

            for (var row = 0; row < overlay.Count && row < Viewport.Height; row++)
            {
                SetAttribute(row == 0 ? Ink.RailOn : Ink.Selected);
                AddStr(0, row, Ink.Clip(overlay[row], width).PadRight(width));
            }

            return true;
        }
    }
}
