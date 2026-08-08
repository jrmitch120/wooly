using Terminal.Gui.Drawing;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Prototype.Separator;

/// <summary>Four fake posts standing in for a real timeline: short, pictured, warned, and long.</summary>
internal static class Feed
{
    public static IReadOnlyList<Post> Posts(DateTimeOffset now) =>
    [
        new Post
        {
            Id = "1",
            Account = "priya@dair.social",
            Author = "Priya Chen",
            PostedAt = now.AddMinutes(-4),
            Content = "Ship's out. Coffee's on.",
            Visibility = PostVisibility.Public,
            Boosts = 3,
            Favorites = 11,
            Replies = 2,
            Marks = PostMarks.None,
        },
        new Post
        {
            Id = "2",
            Account = "nkosi@mastodon.social",
            Author = "Nkosi Dlamini",
            PostedAt = now.AddMinutes(-52),
            Content = "The office cat has opinions about the deploy schedule.",
            Visibility = PostVisibility.Public,
            Boosts = 40,
            Favorites = 128,
            Replies = 9,
            Marks = PostMarks.None,
            Media =
            [
                new PostMedia
                {
                    Id = "m1",
                    Kind = MediaKind.Image,
                    Url = "https://dair.social/media/original/cat-keyboard.png",
                    Description = "A tabby cat asleep on a keyboard, one paw over the space bar.",
                },
            ],
        },
        new Post
        {
            Id = "3",
            Account = "finale@watch.party",
            Author = "Finale Watch Party",
            PostedAt = now.AddHours(-3),
            Content = "She was the imposter the whole time. Also the ending is a dream sequence, again.",
            ContentWarning = "spoilers: the finale",
            Visibility = PostVisibility.Public,
            Boosts = 6,
            Favorites = 14,
            Replies = 21,
            Marks = PostMarks.None,
        },
        new Post
        {
            Id = "4",
            Account = "morgan@fosstodon.org",
            Author = "Morgan Reyes",
            PostedAt = now.AddDays(-1),
            Content =
                "Spent the weekend rewriting the scroll clamp so it works out the far end when the rows are drawn " +
                "rather than when a key is pressed. Laying out every post twice for one keypress is a cost a reader " +
                "feels as a slow scroll, and it turns out that's most of what 'the TUI feels laggy' meant.",
            Visibility = PostVisibility.Unlisted,
            Boosts = 2,
            Favorites = 19,
            Replies = 4,
            Marks = PostMarks.None,
        },
    ];
}

/// <summary>
///     A stand-in for a terminal that draws pictures: every attachment gets a picture, sized so <see cref="Inset.For" />
///     works out real rows and columns from it, the same as it would for a decoded preview.
/// </summary>
internal sealed class FakePictures : IPictures
{
    public CellSize? Cell => new(10, 20);

    public Picture? Of(PostMedia media) => new(new Color[400, 300]);

    public void Want(PostMedia media)
    {
    }
}
