// PROTOTYPE (#450): prints each layout as text, to check the shapes without a terminal.
using Wooly.Core.Notifications;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Prototype;

public class NotificationGroupsPrototypePrint(ITestOutputHelper output)
{
    [Fact]
    public void Print()
    {
        var mine = APost.With(id: "900", content: "Spent the weekend teaching Wooly to group notifications the way the web app does, and it already reads so much calmer than eight copies of one post.", isMine: true);
        var pictures = new FakePictures();
        var people = new[] { ("alice@hachyderm.io", "Alice"), ("bob@mastodon.social", "Bob"), ("cara@fosstodon.org", "Cara"), ("dee@mas.to", "Dee"), ("eli@tech.lgbt", "Eli") };
        var notifications = new List<Notification>();
        var id = 100;

        foreach (var (account, author) in people)
        {
            pictures.HoldingAvatarOf(account);
            notifications.Add(ANotification.With(id: $"{id++}", kind: NotificationKind.Favorite, account: account, author: author, post: mine) with { AvatarUrl = $"https://x/{author}.png" });
        }

        notifications.Add(ANotification.With(id: $"{id++}", account: "bob@mastodon.social", author: "Bob") with { AvatarUrl = "https://x/Bob.png" });

        var screen = new NotificationsScreen(notifications);

        for (var variant = 0; variant < NotificationGroupsPrototype.Variants.Length; variant++)
        {
            output.WriteLine($"===== {NotificationGroupsPrototype.Variants[NotificationGroupsPrototype.Variant]}");

            foreach (var line in screen.Lines(new Drawing(80, DateTimeOffset.UtcNow, pictures, ARaster.Sixel())))
            {
                var text = string.Concat(line.Spans.Select(span => span.Text));
                output.WriteLine($"|{text}|" + (line.Insets.Count > 0 ? $"   insets:{line.Insets.Count} wants:{line.AlsoWants.Count}" : ""));
            }

            NotificationGroupsPrototype.Next();
        }
    }
}
