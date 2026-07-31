using Wooly.Core.Conversations;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tui.Prototype;

/// <summary>
///     What a post looks like to a screen that has to draw it. <see cref="Post" /> is the domain record as it stands
///     today; everything beside it here is something the TUI needs and cannot currently get — see the README's
///     "What this turned up".
/// </summary>
internal sealed record FeedItem
{
    public required Post Post { get; init; }

    /// <summary>Alt text of the images on the post. The domain <c>Post</c> carries no attachments read back.</summary>
    public IReadOnlyList<string> Images { get; init; } = [];

    /// <summary>Non-image attachments, which never render inline on either surface (spec story 51).</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>The poll on the post, as option/votes pairs.</summary>
    public IReadOnlyList<(string Option, int Votes)> Poll { get; init; } = [];

    /// <summary>Whether this profile's account has favorited it. Not on <c>Post</c> today.</summary>
    public bool Favorited { get; init; }

    /// <summary>Whether this profile's account has boosted it. Not on <c>Post</c> today.</summary>
    public bool BoostedByMe { get; init; }

    public bool Pinned { get; init; }

    /// <summary>Whether it is this profile's own post — which is what makes edit, delete and pin offerable.</summary>
    public bool Mine { get; init; }

    public Post Readable => Post.Boosted ?? Post;
}

/// <summary>Fake data. No network, no auth, no persistence — the prototype never talks to an instance.</summary>
internal static class Sample
{
    public static readonly DateTimeOffset Now = new(2026, 7, 30, 21, 40, 0, TimeSpan.Zero);

    public const string Me = "jeff@hachyderm.io";
    public const string MyName = "Jeff Mitchell";

    /// <summary>Remaining calls in this rate-limit window, for the quota indicator (spec story 54).</summary>
    public const int QuotaLeft = 213;

    public const int QuotaTotal = 300;

    public static IReadOnlyList<string> Timelines { get; } = ["Home", "Local", "Federated", "#dotnet"];

    public static IReadOnlyList<FeedItem> Home { get; } =
    [
        new()
        {
            Post = APost(
                "112900001",
                "maria@fosstodon.org",
                "Maria Ochoa",
                minutes: 12,
                "Finally shipped the terminal client rewrite. Terminal.Gui v2's instance-based application model made "
                + "the whole shell testable — no more static state leaking between test runs.",
                boosts: 12,
                favorites: 34,
                replies: 5),
            Favorited = true,
        },
        new()
        {
            Post = APost(
                "112900002",
                "ben@hachyderm.io",
                "Ben Whitlock",
                minutes: 41,
                "sixel in 2026 and it still comes down to whether your multiplexer passes the escape through 🙃",
                boosts: 3,
                favorites: 21,
                replies: 11),
            Images = ["Screenshot of a terminal showing an image rendered as coloured blocks"],
        },
        new()
        {
            Post = ABoost(
                "112900003",
                "jeff@hachyderm.io",
                "Jeff Mitchell",
                minutes: 55,
                APost(
                    "112899555",
                    "hazel@mastodon.art",
                    "Hazel",
                    minutes: 70,
                    "drew the little sheep again. he is thinking about federation.",
                    boosts: 340,
                    favorites: 1290,
                    replies: 44)),
            BoostedByMe = true,
            Images = ["A cartoon sheep in a wool jumper looking at a network diagram"],
            Mine = true,
        },
        new()
        {
            Post = APost(
                "112900004",
                "kev@mas.to",
                "Kev",
                minutes: 96,
                "The whole \"we should federate the moderation queue\" argument, again, from the top, with feeling. "
                + "I have been on this website for nine years and we do this every single summer.",
                boosts: 8,
                favorites: 16,
                replies: 62,
                contentWarning: "instance politics, long"),
        },
        new()
        {
            Post = APost(
                "112900005",
                "sam@chaos.social",
                "Sam ⚡",
                minutes: 140,
                "which do you actually use day to day?",
                boosts: 1,
                favorites: 4,
                replies: 9),
            Poll = [("tmux", 412), ("zellij", 88), ("screen", 31), ("neither, I have 40 windows", 260)],
        },
        new()
        {
            Post = APost(
                "112900006",
                "jeff@hachyderm.io",
                "Jeff Mitchell",
                minutes: 200,
                "Pinned: I write about .NET, terminals and the slow art of making a CLI feel like it was designed on "
                + "purpose. Wooly is my Mastodon client — issues welcome.",
                boosts: 2,
                favorites: 9,
                replies: 1),
            Mine = true,
            Pinned = true,
        },
        new()
        {
            Post = APost(
                "112900007",
                "rin@mstdn.jp",
                "りん",
                minutes: 260,
                "端末でマストドンを読むのが好き。フォントさえ合えばぜんぶ読める。",
                boosts: 5,
                favorites: 30,
                replies: 2),
        },
        new()
        {
            Post = APost(
                "112900008",
                "ops@fosstodon.org",
                "Fosstodon Ops",
                minutes: 400,
                "Maintenance window tonight 02:00–03:00 UTC. Media may 404 while the object store drains.",
                boosts: 44,
                favorites: 12,
                replies: 3,
                visibility: PostVisibility.Unlisted),
        },
        new()
        {
            Post = APost(
                "112900009",
                "dana@hachyderm.io",
                "Dana",
                minutes: 520,
                "followers-only: interviewing again next month, ask me anything about the process in replies",
                boosts: 0,
                favorites: 18,
                replies: 24,
                visibility: PostVisibility.Private),
        },
        new()
        {
            Post = APost(
                "112900010",
                "theo@merveilles.town",
                "Theo",
                minutes: 900,
                "a client that is good at reading is worth three that are good at posting",
                boosts: 61,
                favorites: 210,
                replies: 7),
            Favorited = true,
        },
        new()
        {
            Post = APost(
                "112900011",
                "lu@tech.lgbt",
                "Lu",
                minutes: 1500,
                "here is the mp4 of the whole talk if anyone wants it, 38 minutes, captions included",
                boosts: 90,
                favorites: 140,
                replies: 12),
            Links = ["video/mp4 — \"Terminals are a design surface\", 38:12, captioned"],
        },
        new()
        {
            Post = APost(
                "112900012",
                "gil@mastodon.social",
                "Gil",
                minutes: 2000,
                "hot take: the reason terminal apps feel fast is not that they are fast, it is that they never lie to "
                + "you about what they are doing",
                boosts: 120,
                favorites: 480,
                replies: 31),
        },
    ];

    public static IReadOnlyList<Notification> Notifications { get; } =
    [
        new()
        {
            Id = "n-1",
            Kind = NotificationKind.Mention,
            ReceivedAt = Now.AddMinutes(-8),
            Account = "maria@fosstodon.org",
            Author = "Maria Ochoa",
            Post = APost(
                "112900101",
                "maria@fosstodon.org",
                "Maria Ochoa",
                minutes: 8,
                "@jeff does Wooly do sixel yet or is that still on the pile?",
                boosts: 0,
                favorites: 1,
                replies: 1),
        },
        new()
        {
            Id = "n-2",
            Kind = NotificationKind.Favorite,
            ReceivedAt = Now.AddMinutes(-33),
            Account = "theo@merveilles.town",
            Author = "Theo",
            Post = Home[5].Post,
        },
        new()
        {
            Id = "n-3",
            Kind = NotificationKind.Follow,
            ReceivedAt = Now.AddHours(-2),
            Account = "newbie@mastodon.social",
            Author = "Priya",
        },
        new()
        {
            Id = "n-4",
            Kind = NotificationKind.Boost,
            ReceivedAt = Now.AddHours(-5),
            Account = "ben@hachyderm.io",
            Author = "Ben Whitlock",
            Post = Home[5].Post,
        },
    ];

    public static IReadOnlyList<Conversation> Conversations { get; } =
    [
        new()
        {
            Id = "c-1",
            With = ["maria@fosstodon.org"],
            Unread = true,
            Latest = APost(
                "112900201",
                "maria@fosstodon.org",
                "Maria Ochoa",
                minutes: 20,
                "sure — send it over whenever, no rush",
                boosts: 0,
                favorites: 0,
                replies: 0,
                visibility: PostVisibility.Direct),
        },
        new()
        {
            Id = "c-2",
            With = ["ben@hachyderm.io", "hazel@mastodon.art"],
            Unread = false,
            Latest = APost(
                "112900202",
                "jeff@hachyderm.io",
                "Jeff Mitchell",
                minutes: 300,
                "three of us on the same thread is exactly the case I keep getting wrong",
                boosts: 0,
                favorites: 0,
                replies: 0,
                visibility: PostVisibility.Direct),
        },
    ];

    /// <summary>Accounts waiting to be let in, for the follow-requests screen (spec story 47).</summary>
    public static IReadOnlyList<(string Account, string Author, string Note)> Requests { get; } =
    [
        ("priya@mastodon.social", "Priya", "infra, cats, 3 posts"),
        ("nobody@spam.example", "‌", "no posts, no avatar, joined today"),
    ];

    public static int Unread => Conversations.Count(conversation => conversation.Unread);

    private static Post APost(
        string id,
        string account,
        string author,
        int minutes,
        string content,
        long boosts,
        long favorites,
        long replies,
        string? contentWarning = null,
        PostVisibility visibility = PostVisibility.Public) =>
        new()
        {
            Id = id,
            Account = account,
            Author = author,
            PostedAt = Now.AddMinutes(-minutes),
            Content = content,
            ContentWarning = contentWarning,
            Visibility = visibility,
            Boosts = boosts,
            Favorites = favorites,
            Replies = replies,
            Url = $"https://{account.Split('@')[1]}/@{account.Split('@')[0]}/{id}",
        };

    private static Post ABoost(string id, string account, string author, int minutes, Post boosted) =>
        new()
        {
            Id = id,
            Account = account,
            Author = author,
            PostedAt = Now.AddMinutes(-minutes),
            Content = string.Empty,
            Visibility = boosted.Visibility,
            Boosts = boosted.Boosts,
            Favorites = boosted.Favorites,
            Replies = boosted.Replies,
            Boosted = boosted,
        };
}
