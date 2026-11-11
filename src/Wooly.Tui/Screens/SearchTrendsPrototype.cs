using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     PROTOTYPE (#602), throwaway. Four layouts of an empty search prompt with the instance's trends under it, on the real
///     Search screen with hard-coded trends: F2 cycles the layout, F3 whether typing a letter takes the pick back to the
///     prompt. Asks (1) whether letters-into-the-prompt with arrows walking the trends feels natural, and (2) whether
///     ten of each kind fit under a prompt, or posts want to be fewer or compact.
/// </summary>
public static class SearchTrendsPrototype
{
    /// <summary>A trending link, as the real one would carry it.</summary>
    public sealed record Link(string Url, string Title, string Site, long People, string Description);

    /// <summary>One layout: how many of each kind, in what order, and whether posts are one row apiece.</summary>
    private sealed record Layout(string Name, int Tags, int Posts, int Links, bool CompactPosts, bool PostsLast);

    private static readonly Layout[] Layouts =
    [
        new("A — as specced: 10 tags, 10 whole posts, 10 links", 10, 10, 10, CompactPosts: false, PostsLast: false),
        new("B — compact: 10 of each, every row one line", 10, 10, 10, CompactPosts: true, PostsLast: false),
        new("C — posts last: 10 tags, 10 links, 3 whole posts", 10, 3, 10, CompactPosts: false, PostsLast: true),
        new("D — five each: 5 tags, 5 whole posts, 5 links", 5, 5, 5, CompactPosts: false, PostsLast: false),
    ];

    private static int _at;

    private static readonly string[] LinkStyles =
    [
        "1 two rows: title, then site · people",
        "2 three rows: title, its description, then site · people",
        "3 two rows behind an accent bar",
        "4 ranked: the number, then title over site · people",
        "5 three rows behind an accent bar: title, description, site · people",
    ];

    private static int _links = 4;

    /// <summary>The instance acted as, which the shell sets as it begins.</summary>
    public static string? Instance { get; set; }

    /// <summary>The placeholder an empty field shows.</summary>
    private const string Placeholder = "A word, a #hashtag, an @account, or a link";

    /// <summary>Whether trending posts are drawn one row apiece.</summary>
    public static bool CompactPosts => Layouts[_at].CompactPosts;

    /// <summary>What the status row says on a switch.</summary>
    public static string Label =>
        $"PROTOTYPE {Layouts[_at].Name} · links {LinkStyles[_links]} (F2 layout, F3 links)";

    public static void Next() => _at = (_at + 1) % Layouts.Length;

    public static void NextLinks() => _links = (_links + 1) % LinkStyles.Length;

    /// <summary>
    ///     The boxed search field with the title F3 has reached: <paramref name="focused" /> where the pick is on it and
    ///     letters go into it, and dim with no caret where the pick is on a trend and letters are keys.
    /// </summary>
    public static IReadOnlyList<Line> Prompt(string query, int width, bool focused)
    {
        var inner = Math.Max(0, width - 2);
        var border = focused ? Role.PanelBorderActive : Role.PanelBorder;
        var caret = focused ? new Span("▌", Role.Selection) : new Span("", Role.Body);
        var typed = query.Length == 0
            ? new Span(TextWrap.Clip(Placeholder, Math.Max(0, inner - 5)), Role.Muted)
            : new Span(TextWrap.Clip(query, Math.Max(0, inner - 5)), focused ? Role.Body : Role.Muted);
        Span[] field = query.Length == 0 ? [caret, typed] : [typed, caret];
        var used = 3 + field.Sum(span => Glyphs.Columns(span.Text));

        // The instance that answers, since who asks is part of the answer (ADR-0011).
        var title = Instance is { } instance ? $"─ {instance} " : "";

        return
        [
            Line.Of([
                new Span("╭" + title, border),
                new Span(new string('─', Math.Max(0, inner - Glyphs.Columns(title))) + "╮", border),
            ]),
            Line.Of([
                new Span("│", border), new Span(" ⌕ ", Role.Muted), .. field,
                new Span(new string(' ', Math.Max(0, inner - used)), Role.Body), new Span("│", border),
            ]),
            Line.Of([new Span("╰" + new string('─', inner) + "╯", border)]),
        ];
    }

    /// <summary>The prompt, then the trends in this layout's order and numbers.</summary>
    public static IReadOnlyList<Result> Rows()
    {
        var layout = Layouts[_at];
        IEnumerable<Result> tags = Tags.Take(layout.Tags).Select(Result (tag) => new Result.OfHashtag(tag));
        IEnumerable<Result> posts = Posts.Take(layout.Posts).Select(Result (post) => new Result.OfPost(post));
        IEnumerable<Result> links = Links.Take(layout.Links).Select(Result (link) => new Result.OfLink(link));

        return
        [
            new Result.OfPrompt(),
            .. tags,
            .. layout.PostsLast ? links : posts,
            .. layout.PostsLast ? posts : links,
        ];
    }

    /// <summary>A trending post on one row: who, the start of what they said, and its counts, in fixed columns.</summary>
    public static Line CompactPost(Post post, int width)
    {
        const int handle = 14;
        const int counts = 18;
        var who = TextWrap.Clip($"@{post.Account.Split('@')[0]}", handle - 2).PadRight(handle);
        var tally = $"↺ {Number.Of(post.Boosts),5}  ☆ {Number.Of(post.Favorites),5}".PadLeft(counts);
        var room = Math.Max(0, width - handle - counts - 2);
        var said = TextWrap.Clip(post.Content.ReplaceLineEndings(" "), room);

        return Line.Of([
            new Span(who, Role.BylineHandle),
            new Span(said + new string(' ', Math.Max(0, room - Glyphs.Columns(said)) + 2), Role.Body),
            new Span(tally, Role.Muted),
        ]);
    }

    /// <summary>Whether two trending links want a blank between them.</summary>
    public static bool LinksApart => true;

    /// <summary>A trending link, in the style F3 has reached; <paramref name="rank" /> counts from one.</summary>
    public static IReadOnlyList<Line> LinkRows(Link link, int width, int rank)
    {
        var meta = $"{link.Site} · {Number.Of(link.People)} people sharing";

        switch (_links)
        {
            case 1:
                return
                [
                    Line.Of(TextWrap.Clip(link.Title, width), Role.Body),
                    Line.Of(TextWrap.Clip(link.Description, width), Role.Muted),
                    Line.Of([new Span(TextWrap.Clip(meta, width), Role.Link)]),
                ];

            case 2:
                return
                [
                    Line.Of([new Span("┃ ", Role.Link), new Span(TextWrap.Clip(link.Title, Math.Max(0, width - 2)), Role.Body)]),
                    Line.Of([new Span("┃ ", Role.Link), new Span(TextWrap.Clip(meta, Math.Max(0, width - 2)), Role.Muted)]),
                ];

            case 4:
                return
                [
                    Line.Of([new Span("┃ ", Role.Link), new Span(TextWrap.Clip(link.Title, Math.Max(0, width - 2)), Role.Body)]),
                    Line.Of([new Span("┃ ", Role.Link), new Span(TextWrap.Clip(link.Description, Math.Max(0, width - 2)), Role.Muted)]),
                    Line.Of([new Span("┃ ", Role.Link), new Span(TextWrap.Clip(meta, Math.Max(0, width - 2)), Role.Link)]),
                ];

            case 3:
            {
                var number = $"{rank,2}  ";
                var room = Math.Max(0, width - number.Length);

                return
                [
                    Line.Of([new Span(number, Role.Muted), new Span(TextWrap.Clip(link.Title, room), Role.Body)]),
                    Line.Of([new Span(new string(' ', number.Length), Role.Body), new Span(TextWrap.Clip(meta, room), Role.Muted)]),
                ];
            }

            default:
                return
                [
                    Line.Of(TextWrap.Clip(link.Title, width), Role.Body),
                    Line.Of(TextWrap.Clip(meta, width), Role.Muted),
                ];
        }
    }

    /// <summary>A trending tag on one row: the tag, and how many people used it over the last days.</summary>
    public static Line TagRow(Hashtag tag, int width, int nameColumn)
    {
        var name = TextWrap.Clip($"#{tag.Name}", width);
        var gap = new string(' ', Math.Max(2, nameColumn - Glyphs.Columns(name) + 2));
        var used = $"{Number.Of(tag.RecentAccounts),5} people · last 2 days";

        return Line.Of([
            new Span(name, Role.BylineHandle),
            new Span(TextWrap.Clip(gap + used, Math.Max(0, width - Glyphs.Columns(name))), Role.Muted),
        ]);
    }

    public static int NameColumn => Tags.Max(tag => Glyphs.Columns(tag.Name) + 1);

    private static Hashtag Tag(string name, long accounts) => new()
    {
        Name = name, RecentPosts = accounts * 3, RecentAccounts = accounts, Url = $"https://mastodon.social/tags/{name}",
    };

    private static readonly Hashtag[] Tags =
    [
        Tag("FediTips", 1_842), Tag("caturday", 1_517), Tag("SilentSunday", 1_203), Tag("NowPlaying", 988),
        Tag("photography", 845), Tag("Halloween", 802), Tag("OpenSource", 611), Tag("dotnet", 402),
        Tag("TerminalApps", 288), Tag("sheep", 97),
    ];

    private static Post Post(string id, string account, string author, string content, int boosts, int favorites,
        int replies) => new()
    {
        Id = id,
        Account = account,
        Author = author,
        AuthorCustomEmoji = [],
        PostedAt = DateTimeOffset.Now.AddHours(-int.Parse(id) % 9 - 1),
        Content = content,
        ContentWarning = null,
        Sensitive = false,
        Visibility = PostVisibility.Public,
        Boosts = boosts,
        Favorites = favorites,
        Replies = replies,
        Marks = PostMarks.None,
        IsMine = false,
        Media = [],
        Mentions = [],
        CustomEmoji = [],
        Boosted = null,
        Url = $"https://mastodon.social/@{account.Split('@')[0]}/{id}",
        AvatarUrl = null,
        InReplyTo = null,
        Poll = null,
        LinkPreview = null,
    };

    private static readonly Post[] Posts =
    [
        Post("1", "eloise@mastodon.social", "Eloise", "Reminder that you can follow hashtags on Mastodon. Search for one, open it, and press follow. It'll show up in your home timeline like a person would. #FediTips", 1_204, 2_310, 88),
        Post("2", "marcus@hachyderm.io", "Marcus Okafor", "Three years in, I finally rewrote our build in plain make and it's 4x faster. I am not taking questions.", 845, 3_120, 214),
        Post("3", "sheepish@toot.cafe", "the sheep account", "baa", 2_001, 5_402, 301),
        Post("4", "ana@fosstodon.org", "Ana Lima", "Our open source project just hit 10,000 stars. Thank you to every single person who filed an issue, sent a patch, or just told a friend. This is wild.\n\nRelease notes in the thread 🧵", 402, 1_880, 97),
        Post("5", "photobot@pixelfed.social", "Weekend Photos", "Fog over the valley this morning, about 20 minutes before sunrise. #SilentSunday #photography", 311, 1_402, 12),
        Post("6", "kim@infosec.exchange", "Kim", "PSA: if your router's admin page is reachable from the internet, today is a great day to fix that.", 1_530, 1_210, 76),
        Post("7", "dev@mastodon.online", "Devi", "Hot take: a terminal app with mouse support is just a GUI that respects you.", 288, 990, 143),
        Post("8", "news@journa.host", "Evening Desk", "Tonight's long read: how three villages kept their library open for a century without a single paid librarian.", 640, 1_105, 54),
        Post("9", "cat@mastodon.social", "Biscuit", "I have knocked the pen off the desk. The human has picked it up. I have knocked the pen off the desk. #caturday", 1_980, 6_210, 167),
        Post("10", "jo@chaos.social", "Jo", "If you're new here: write an #introduction post. People really do read them, and it's the fastest way to find your corner.", 720, 1_640, 39),
    ];

    private static readonly Link[] Links =
    [
        new("https://example.org/library", "The library that ran itself for a hundred years", "The Long Read", 1_420, "Three villages, one building, and a rota that has never once missed a Saturday."),
        new("https://example.org/fog", "Why valley fog forms before sunrise, explained", "Weather Weekly", 812, "Cold air pools in the valley overnight, and the first sun lifts it into a sea of cloud."),
        new("https://example.org/router", "Thousands of home routers expose their admin pages", "Security Now", 790, "Researchers found admin pages open to the internet on routers from a dozen vendors."),
        new("https://example.org/make", "In praise of boring build tools", "Dev Notes", 655, "The case for build systems nobody has to think about."),
        new("https://example.org/stars", "What 10,000 GitHub stars actually means for a maintainer", "Open Source Weekly", 512, "Stars bring attention, and attention brings issues. A maintainer on what changes."),
        new("https://example.org/cats", "Cats knock things over on purpose, study finds", "Science Daily", 488, "Researchers watched 120 cats and a great many pens."),
        new("https://example.org/fedi", "Two years after the migration: who stayed on the fediverse", "The Verge", 420, "We asked a thousand people who joined in the great migration where they are now."),
        new("https://example.org/tui", "The quiet return of terminal user interfaces", "Ars Technica", 301, "From mail to music, the terminal is having a moment again."),
        new("https://example.org/halloween", "The best homemade Halloween costumes of 2026", "Craft Corner", 255, "Cardboard, felt, and a lot of hot glue."),
        new("https://example.org/sheep", "A field guide to sheep, for the absolutely unprepared", "Countryside", 96, "Everything you need to know before your first lambing season, and some things you don't."),
    ];
}
