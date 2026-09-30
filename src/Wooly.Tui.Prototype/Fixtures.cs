namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. Invented people and posts, held in memory; toggling a favourite changes nothing anywhere.

internal sealed class Post(string name, string handle, string ago, string text, int replies, int boosts, int favs)
{
    public string Name { get; } = name;
    public string Handle { get; } = handle;
    public string Ago { get; } = ago;
    public string Text { get; } = text;
    public int Replies { get; } = replies;
    public int Boosts { get; set; } = boosts;
    public int Favs { get; set; } = favs;
    public bool Faved { get; set; }
    public bool Boosted { get; set; }
    public bool Revealed { get; set; }
    public string? BoostedBy { get; init; }
    public string? Warning { get; init; }
    public string? Media { get; init; }
    public string Audience { get; init; } = "public";
    public string[]? Poll { get; init; }

    public string AudienceMark => Audience switch { "unlisted" => "◌", "followers" => "●", "direct" => "✉", _ => "○" };

    public string ShownText => Warning is not null && !Revealed ? $"CW: {Warning}" : Text;

    public string FirstLine => ShownText.Split('\n')[0];

    public void ToggleFav()
    {
        Faved = !Faved;
        Favs += Faved ? 1 : -1;
    }

    public void ToggleBoost()
    {
        Boosted = !Boosted;
        Boosts += Boosted ? 1 : -1;
    }
}

internal sealed record Notice(string Kind, string Who, string Ago, string What, string Day);

internal sealed record Convo(string With, string Ago, string Last, int Unread);

internal sealed record Place(string Glyph, string Name, int Unread, string Group);

internal static class Fixtures
{
    public const string Me = "@jeff@mastodon.social";
    public const string Instance = "mastodon.social";
    public const string Quota = "287/300";

    public static readonly List<Place> Places =
    [
        new("⌂", "Home", 3, "timelines"),
        new("◎", "Local", 0, "timelines"),
        new("◍", "Federated", 0, "timelines"),
        new("#", "rust", 7, "timelines"),
        new("◆", "Notifications", 12, "you"),
        new("✉", "Messages", 2, "you"),
        new("⚑", "Requests", 1, "you"),
        new("⌕", "Search", 0, "find"),
        new("✦", "Discover", 0, "find"),
        new("☺", "Profile", 0, "you"),
    ];

    public static readonly List<Post> Posts =
    [
        new("Mira Okafor", "@mira@hachyderm.io", "3m",
            "Finally got the new keyboard firmware building. Turns out the whole problem was one missing semicolon in a macro three files away. Computers are a delight and I would like to go and live in the woods now.",
            4, 12, 48),
        new("Tomás Reyes", "@tomas@fosstodon.org", "12m",
            "Release day! loom 2.0 ships with sixel previews, a rewritten config loader and roughly four hundred fewer allocations per frame. Changelog in the thread below. #rust #tui",
            9, 31, 102) { BoostedBy = "Sam Whitfield" },
        new("Priya Natarajan", "@priya@mastodon.social", "25m",
            "Long thread on the council vote last night and what it means for the bus routes on the east side. Short version: call your rep before Friday.",
            14, 40, 66) { Warning = "local politics" },
        new("Ada Lindqvist", "@ada@photog.social", "41m",
            "Fog rolling over the harbour this morning. Shot on a thirty year old lens that cost less than lunch. #photography #film",
            2, 18, 130) { Media = "Harbour at dawn, cranes fading into grey fog over still water" },
        new("Ben Carter", "@ben@mstdn.ca", "1h",
            "Hot take: the best terminal colour scheme is whichever one you stopped fiddling with.",
            22, 9, 57),
        new("Kenji Sato", "@kenji@social.coop", "1h",
            "When a service falls over at 3am, what do you reach for first?",
            6, 2, 11) { Poll = ["logs  ██████████░░ 48%", "metrics  ██████░░░░ 29%", "restart and pray  ████░░░░░░ 23%"] },
        new("Nadia Haddad", "@nadia@infosec.exchange", "2h",
            "PSA: rotate the token you pasted into that gist. Yes, that one. No, making the gist secret does not count. #infosec",
            3, 88, 140),
        new("Lou Bernard", "@lou@toot.cafe", "2h",
            "Spent the afternoon teaching my nephew to solder. He burned himself exactly once, said a word I did not teach him, and then built a working blinky LED board. Proud uncle moment.\n\nNext week: why the resistor matters.",
            5, 3, 72) { Audience = "unlisted" },
        new("Elle Park", "@elle@mastodon.art", "3h",
            "New linocut drying on the rack. Two colours, five hours, one sliced thumb. #printmaking #art",
            8, 25, 211) { Media = "A two-colour linocut of a heron standing in reeds, blue on cream paper" },
        new("Ravi Menon", "@ravi@hachyderm.io", "4h",
            "Reading about CRDTs again and I think I finally understand why every collaborative editor ends up reinventing half of them badly. @mira you were right.",
            7, 4, 39) { Audience = "followers" },
        new("Owen Brooks", "@owen@mastodon.scot", "5h",
            "The cat has learned to open the cupboard with the treats in it. I am no longer the smartest mammal in this flat.",
            19, 44, 390),
        new("Sam Whitfield", "@sam@fosstodon.org", "6h",
            "Reminder that the fediverse meetup is Thursday, same pub as last time, and yes there will be stickers. #fediverse",
            1, 6, 20),
    ];

    public static readonly List<Post> Replies =
    [
        new("Hana Ito", "@hana@hachyderm.io", "2m", "@mira the semicolon always wins. Always.", 0, 0, 6),
        new("Mira Okafor", "@mira@hachyderm.io", "1m", "@hana one day I will beat it. Not today.", 0, 0, 3),
        new("Dev Kapoor", "@dev@fosstodon.org", "1m", "@mira which firmware? QMK or ZMK? Asking because I am about to go down the same hole.", 1, 0, 2),
    ];

    public static readonly Post Ancestor = new("Mira Okafor", "@mira@hachyderm.io", "20m",
        "Keyboard project status: parts arrived, soldering done, firmware refuses to compile for reasons known only to the gods.",
        6, 2, 31);

    public static readonly List<Notice> Notices =
    [
        new("fav", "Mira Okafor and 4 others", "2m", "favourited your post \"Terminal clients are back, baby\"", "Today"),
        new("boost", "Tomás Reyes", "9m", "boosted your post \"Terminal clients are back, baby\"", "Today"),
        new("mention", "Ravi Menon", "14m", "@jeff did you ever get sixel working in tmux?", "Today"),
        new("follow", "Hana Ito", "1h", "followed you", "Today"),
        new("follow", "Dev Kapoor and 2 others", "2h", "followed you", "Today"),
        new("reply", "Lou Bernard", "3h", "replied: \"that is the most relatable thing I have read all week\"", "Today"),
        new("poll", "Kenji Sato", "5h", "a poll you voted in has ended", "Today"),
        new("fav", "Ada Lindqvist", "1d", "favourited your post \"Fog again. Ferry cancelled.\"", "Yesterday"),
        new("boost", "Elle Park and 11 others", "1d", "boosted your post \"Open call for zine submissions\"", "Yesterday"),
        new("request", "Quinn Albright", "1d", "wants to follow you", "Yesterday"),
    ];

    public static readonly List<Convo> Convos =
    [
        new("Ravi Menon", "14m", "you around this weekend? want to pair on the CRDT thing", 2),
        new("Mira Okafor", "2h", "ok the macro thing was genuinely cursed, I will show you", 0),
        new("Sam Whitfield", "1d", "stickers are ordered!", 0),
        new("Hana Ito, Dev Kapoor", "3d", "Dev: agreed, let's do the talk as a pair", 0),
    ];

    public static string Glyph(string kind) => kind switch
    {
        "fav" => "★",
        "boost" => "↺",
        "mention" => "@",
        "follow" => "+",
        "reply" => "↩",
        "poll" => "▤",
        "request" => "?",
        _ => "·",
    };

    public static uint Tint(string kind) => kind switch
    {
        "fav" => P.Yellow,
        "boost" => P.Green,
        "mention" or "reply" => P.Mauve,
        "follow" or "request" => P.Blue,
        _ => P.Subtext,
    };
}
