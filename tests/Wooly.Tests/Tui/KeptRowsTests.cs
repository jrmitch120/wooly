using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A post's rows are kept from one frame to the next and laid out again only where something they are drawn from has
///     changed (#584): a frame laid out every post on the screen afresh, so a scroll slowed as the feed grew.
/// </summary>
public class KeptRowsTests
{
    private static readonly CustomEmoji[] SomeCustomEmoji =
    [
        .. Enumerable.Range(0, 8).Select(at => new CustomEmoji($"e{at}", $"https://files.example/emoji/e{at}.png")),
    ];

    private static readonly CustomEmoji Php = new("php", "https://files.mastodon.social/emoji/php.png");

    /// <summary>When every post on the feed was posted: half a minute before the first frame.</summary>
    private static readonly DateTimeOffset Posted = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     After any change, the rows a screen kept are exactly the rows a screen laying them out afresh draws: the one
    ///     drawn before the change and drawn again after it, against one that never saw them before the change. Every
    ///     thing a post's rows are drawn from is changed by one of these.
    /// </summary>
    [Theory]
    [InlineData("nothing")]
    [InlineData("the pick moves")]
    [InlineData("a warning is asked past")]
    [InlineData("a poll's answer is chosen")]
    [InlineData("a reference is walked to")]
    [InlineData("a picture lands")]
    [InlineData("a custom emoji lands")]
    [InlineData("a picture is refused")]
    [InlineData("a picture is let go of")]
    [InlineData("the panel is narrowed")]
    [InlineData("the terminal draws no pictures")]
    [InlineData("the cell changes")]
    [InlineData("the captions are hidden")]
    [InlineData("the minute turns")]
    [InlineData("the day turns")]
    public async Task TheRowsKeptAreTheRowsLaidOutAfresh(string change)
    {
        var pictures = new FakePictures().HoldingAvatarOf("ben@mastodon.social");

        if (change == "a picture is let go of")
        {
            pictures.HoldingPhotograph("m2", 80, 60);
        }

        var blurs = new Blurs();
        var before = new Drawing(60, Posted.AddSeconds(30), pictures, ARaster.Kitty(), Blurs: blurs, Height: 40);
        var after = change switch
        {
            "the panel is narrowed" => before with { Width = 44 },
            "the terminal draws no pictures" => before with { Raster = null },
            "the cell changes" => before with { Raster = ARaster.Kitty(new CellSize(10, 24)) },
            "the captions are hidden" => before with { HideDrawnCaption = true },
            "the minute turns" => before with { Now = Posted.AddSeconds(90) },
            "the day turns" => before with { Now = Posted.AddDays(1) },
            _ => before,
        };

        var kept = await Feed();

        Before(kept, change);
        kept.Screen.Lines(before);
        Change(kept, change, pictures);

        var fresh = await Feed();

        Before(fresh, change);
        Change(fresh, change, pictures);

        Assert.Equal(Described(fresh.Screen.Lines(after)), Described(kept.Screen.Lines(after)));
    }

    /// <summary>
    ///     A list that grows as it is read lays out its things once and keeps them: a long inbox or list of
    ///     conversations drawn a second time allocates a fraction of what drawing it the first time did.
    /// </summary>
    [Theory]
    [InlineData("notifications")]
    [InlineData("conversations")]
    public void ALongListDrawnAgainAllocatesAFractionOfDrawingItFirst(string list)
    {
        var screen = Long(list, things: 800);
        var drawing = new Drawing(60, Posted.AddMinutes(5), new FakePictures(), ARaster.Kitty());

        var before = GC.GetAllocatedBytesForCurrentThread();
        screen.Lines(drawing);
        var first = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        screen.Lines(drawing);
        var again = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(again < first / 5, $"Drawn first, {first / 1024} KB; drawn again, {again / 1024} KB.");
    }

    /// <summary>
    ///     On the inbox and the list of conversations too, the rows kept after a change are the rows laid out afresh.
    /// </summary>
    [Theory]
    [InlineData("notifications", "nothing")]
    [InlineData("notifications", "the pick moves")]
    [InlineData("notifications", "a reference is walked to")]
    [InlineData("notifications", "an avatar lands")]
    [InlineData("notifications", "the minute turns")]
    [InlineData("conversations", "nothing")]
    [InlineData("conversations", "the pick moves")]
    [InlineData("conversations", "a reference is walked to")]
    [InlineData("conversations", "an avatar lands")]
    [InlineData("conversations", "the minute turns")]
    public void TheRowsKeptOnAListThatGrowsAreTheRowsLaidOutAfresh(string list, string change)
    {
        var pictures = new FakePictures();
        var before = new Drawing(60, Posted.AddSeconds(30), pictures, ARaster.Kitty(), Height: 40);
        var after = change == "the minute turns" ? before with { Now = Posted.AddSeconds(90) } : before;

        void Changing(Screen screen)
        {
            switch (change)
            {
                case "the pick moves":
                    screen.Move(1);
                    break;
                case "a reference is walked to":
                    Assert.True(screen.WalkReference(1));
                    break;
                case "an avatar lands":
                    pictures.HoldingAvatarOf("ann@mastodon.social");
                    break;
            }
        }

        var kept = Long(list, things: 3);

        kept.Lines(before);
        Changing(kept);

        var fresh = Long(list, things: 3);

        Changing(fresh);

        Assert.Equal(Described(fresh.Lines(after)), Described(kept.Lines(after)));
    }

    /// <summary>
    ///     An inbox or a list of conversations <paramref name="things" /> long, every one about a post by Ann with a
    ///     link in it, posted when the feed's posts were.
    /// </summary>
    private static Screen Long(string list, int things)
    {
        Post Said(int at) =>
            APost.With(
                id: $"{at}0",
                account: "ann@mastodon.social",
                author: "Ann",
                content: $"See https://example.com/{at} about the flock",
                avatarUrl: "https://files.mastodon.social/avatars/ann.png") with { PostedAt = Posted };

        return list switch
        {
            "notifications" => new NotificationsScreen(
            [
                .. Enumerable.Range(1, things).Select(at => ANotification.Alone(
                    ANotification.With(id: $"{at}", account: "ann@mastodon.social", author: "Ann", post: Said(at)) with
                    {
                        ReceivedAt = Posted,
                    })),
            ]),
            _ => new DirectMessagesScreen(
            [
                .. Enumerable.Range(1, things).Select(at => AConversation.With(id: $"{at}", latest: Said(at))),
            ]),
        };
    }

    /// <summary>
    ///     A feed holding one of everything a post's rows can be drawn from: an avatar, custom emoji in the name and
    ///     the words, a photograph with a blur, a warning over its words, a poll, and a link preview.
    /// </summary>
    private static async Task<Shell> Feed()
    {
        Post Posting(string id, string account, string content, IReadOnlyList<PostMedia>? media = null) =>
            APost.With(
                id: id,
                account: account,
                author: "Ben :php:",
                content: content,
                avatarUrl: $"https://files.mastodon.social/avatars/{id}.png",
                media: media,
                customEmoji: [Php],
                authorCustomEmoji: [Php]) with { PostedAt = Posted };

        return await new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                Posting("1", "ben@mastodon.social", "Shearing :php: today"),
                Posting(
                    "2",
                    "ann@mastodon.social",
                    "The flock",
                    [APost.APicture("m2", shape: new PictureShape(800, 600), blurhash: "LEHV6nWB2yk8pyo0adR*.7kCMdnj")]),
                Posting("3", "cat@mastodon.social", "The ending") with { ContentWarning = "spoilers" },
                Posting("4", "dan@mastodon.social", "Which?") with { Poll = APost.APoll() },
                Posting("5", "eve@mastodon.social", "Read https://example.com/wool") with
                {
                    LinkPreview = APost.ALinkPreview(),
                }),
        }.Opened();
    }

    /// <summary>
    ///     Picks the post <paramref name="change" /> is made to, before the screen is first drawn — so that the pick
    ///     moving is not what lays it out again.
    /// </summary>
    private static void Before(Shell shell, string change)
    {
        var to = change switch
        {
            "a warning is asked past" => 2,
            "a poll's answer is chosen" => 3,
            "a reference is walked to" => 4,
            _ => 0,
        };

        shell.Screen.Move(to);
    }

    /// <summary>Makes <paramref name="change" /> to <paramref name="shell" />'s feed or to the pictures it draws.</summary>
    private static void Change(Shell shell, string change, FakePictures pictures)
    {
        switch (change)
        {
            case "the pick moves":
                shell.Screen.Move(1);
                break;
            case "a warning is asked past":
                Assert.True(shell.Screen.Reveal());
                break;
            case "a poll's answer is chosen":
                Assert.True(shell.Screen.Toggle(1));
                break;
            case "a reference is walked to":
                Assert.True(shell.Screen.WalkReference(1));
                break;
            case "a picture lands":
                pictures.HoldingPhotograph("m2", 80, 60);
                break;
            case "a custom emoji lands":
                pictures.HoldingCustomEmoji(Php);
                break;
            case "a picture is refused":
                pictures.Refusing("m2", Unshowable.Failed);
                break;
            case "a picture is let go of":
                pictures.Forgetting("m2");
                break;
        }
    }

    /// <summary>
    ///     Every row whole, as text: each thing it holds said through its public properties, all the way down — and a
    ///     picture by which picture it is rather than by its pixels, since a row kept is a row holding the very same
    ///     one. Said this way rather than compared as records, whose lists compare as the lists they are.
    /// </summary>
    private static string Described(IReadOnlyList<Line> lines)
    {
        var described = new StringBuilder();

        foreach (var line in lines)
        {
            Describe(line, described, depth: 0);
            described.AppendLine();
        }

        return described.ToString();
    }

    private static void Describe(object? value, StringBuilder described, int depth)
    {
        switch (value)
        {
            case null:
                described.Append("null");
                return;
            case Picture picture:
                described.Append("picture#").Append(RuntimeHelpers.GetHashCode(picture));
                return;
            case string or Enum or ValueType when value.GetType().IsPrimitive || value is string or Enum:
                described.Append(value);
                return;
            case IEnumerable many when depth < 8:
                described.Append('[');

                foreach (var each in many)
                {
                    Describe(each, described, depth + 1);
                    described.Append(", ");
                }

                described.Append(']');
                return;
        }

        if (depth >= 8)
        {
            described.Append(value);
            return;
        }

        described.Append(value.GetType().Name).Append(" { ");

        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            described.Append(property.Name).Append(" = ");
            Describe(property.GetValue(value), described, depth + 1);
            described.Append(", ");
        }

        described.Append('}');
    }

    /// <summary>
    ///     A notch of the wheel over a long feed allocates about what one over a short feed does: what a frame costs is
    ///     the page's, not the feed's. Allocation rather than time, since it says the same on every run.
    /// </summary>
    [Fact]
    public async Task ANotchOverALongFeedAllocatesAboutWhatOneOverAShortFeedDoes()
    {
        var short_ = await AllocatedANotch(posts: 40);
        var long_ = await AllocatedANotch(posts: 800);

        Assert.True(
            long_ < short_ * 2,
            $"A notch over 800 posts allocated {long_ / 1024} KB, over 40 posts {short_ / 1024} KB.");
    }

    /// <summary>
    ///     What a notch of the wheel allocates on the UI thread, on average, over a feed of <paramref name="posts" />
    ///     posts — each with an avatar and custom emoji in its words and name, and every third a photograph — once
    ///     every picture on the page has landed.
    /// </summary>
    private static async Task<long> AllocatedANotch(int posts)
    {
        var pictures = new FakePictures();

        foreach (var each in SomeCustomEmoji)
        {
            pictures.HoldingCustomEmoji(each);
        }

        var feed = Enumerable.Range(1, posts)
            .Select(at =>
            {
                var used = SomeCustomEmoji[at % SomeCustomEmoji.Length];
                var account = $"user{at}@mastodon.social";

                pictures.HoldingAvatarOf(account);

                if (at % 3 == 0)
                {
                    pictures.HoldingPhotograph($"m{at}", 80, 60);
                }

                return APost.With(
                    id: $"{at}0",
                    account: account,
                    author: $"User {used.Written}",
                    content: $"Post {at} with {used.Written} in it, and words enough to wrap",
                    avatarUrl: $"https://files.example/avatars/{at}.png",
                    media: at % 3 == 0 ? [APost.APicture($"m{at}", shape: new PictureShape(800, 600))] : null,
                    customEmoji: [used],
                    authorCustomEmoji: [used]);
            })
            .ToArray();

        using var drawn = await DrawnShell.Of(
            140,
            50,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(feed) },
            pictures: pictures,
            drawsPlaceholders: true);

        const int notches = 20;

        for (var notch = 0; notch < notches; notch++)
        {
            drawn.WheelUndrawn(RailLines.Width + 4, 3);
            drawn.DrawAsTheLoopDraws();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var notch = 0; notch < notches; notch++)
        {
            drawn.WheelUndrawn(RailLines.Width + 4, 3);
            drawn.DrawAsTheLoopDraws();
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / notches;
    }
}
