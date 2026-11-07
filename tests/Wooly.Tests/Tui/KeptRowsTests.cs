using Wooly.Core;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
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
