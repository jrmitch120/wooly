using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where the pointer tests click in the content, and the listing screens they click on: a column clear of the
///     pick's gutter, the rows things are drawn on, and each screen drawn tall with two things on it (#290, #291).
/// </summary>
internal static class ContentClicks
{
    /// <summary>A column inside the content panel, clear of its left edge and the pick's gutter.</summary>
    public const int OverContent = RailLines.Width + 4;

    /// <summary>Tall enough that every screen these tests draw has its first two things on the page.</summary>
    public const int Tall = 50;

    /// <summary>The terminal row the first row reading <paramref name="text" /> is drawn on, below <paramref name="after" />.</summary>
    public static int RowOf(DrawnShell drawn, string text, int after = -1)
    {
        var rows = drawn.Rows();
        var row = Array.FindIndex(
            rows,
            after + 1,
            line => line[RailLines.Width..].Contains(text, StringComparison.Ordinal));

        Assert.True(row >= 0, $"Nothing in the content reads {text}:\n{string.Join('\n', rows)}");

        return row;
    }

    /// <summary>The first terminal row the content draws as part of the <paramref name="item" />th thing.</summary>
    public static int ContentRowOf(DrawnShell drawn, int item)
    {
        var row = Enumerable.Range(0, Tall).FirstOrDefault(
            at => drawn.Content.ItemAt(new Point(OverContent, at)) == item,
            -1);

        Assert.True(row >= 0, $"No row is part of thing {item}:\n{string.Join('\n', drawn.Rows())}");

        return row;
    }

    /// <summary>The shell drawn tall on the listing screen <paramref name="screen" /> names, with two things on it.</summary>
    public static async Task<DrawnShell> On(string screen)
    {
        if (screen == "search")
        {
            return await Searched();
        }

        var ann = AnAccount.With(id: "1", address: "ann@hachyderm.io", author: "Ann");
        var bea = AnAccount.With(id: "2", address: "bea@hachyderm.io", author: "Bea");

        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", account: "ben@hachyderm.io"),
                APost.With(id: "220", content: "Two sheep")),
            Engagement = FakePostEngagement.Answered(APost.With(id: "110"), APost.With(id: "120", content: "A reply")),
            Accounts = FakeAccountRelationships.Holding(AnAccount.With(address: "ben@hachyderm.io"), ann, bea),
            Notifications = FakeNotificationInbox.Holding(ANotification.With(id: "1"), ANotification.With(id: "2")),
            Messages = FakeDirectMessages.Holding(AConversation.With(id: "7"), AConversation.With(id: "8")),
            Suggestions = FakeFollowSuggestions.Offering(ASuggestion.With(ann), ASuggestion.With(bea)),
            Profiles = FakeProfileRegistry.Holding(
                "personal",
                FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
                FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
        };

        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        switch (screen)
        {
            case "post":
                drawn.Press(Key.Enter);
                break;

            case "notifications":
                drawn.Shell.Rail.GoTo(DestinationKind.Notifications);
                break;

            case "messages":
                drawn.Shell.Rail.GoTo(DestinationKind.Messages);
                break;

            case "requests":
                drawn.Shell.Rail.GoTo(DestinationKind.Requests);
                break;

            case "discover":
                drawn.Shell.Rail.GoTo(DestinationKind.Discover);
                break;

            case "follows":
                await drawn.Shell.OpenAuthor();
                built.Host.Drain();
                drawn.Press(Key.W);
                break;

            case "account":
                await drawn.Shell.OpenAuthor();
                break;

            case "profiles":
                drawn.Shell.Profiles();
                break;
        }

        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }

    /// <summary>The shell drawn tall over what a search for sheep found: two accounts, two hashtags and two posts.</summary>
    public static async Task<DrawnShell> Searched()
    {
        var built = new AShell
        {
            Search = FakeInstanceSearch.Finding(
                accounts: [AnAccount.With(address: "ann@hachyderm.io"), AnAccount.With(address: "bea@hachyderm.io")],
                hashtags: [AHashtag.With(name: "sheep"), AHashtag.With(name: "wool")],
                posts: [APost.With(id: "110"), APost.With(id: "220")]),
        };

        var drawn = await DrawnShell.Of(80, Tall, Themes.Plain, built);

        drawn.Shell.Search();
        built.Host.Drain();

        foreach (var letter in "sheep")
        {
            drawn.Shell.Type(letter);
        }

        drawn.Shell.Press(ShellKey.Enter);
        built.Host.Drain();
        drawn.Redraw();

        return drawn;
    }
}
