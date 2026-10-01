using Terminal.Gui.App;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Prototype;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui.Prototype;

// PROTOTYPE — throwaway. Renders the real shell headlessly, once per skin, to .txt and .ansi (ansi2png.py makes a
// picture of one). Does nothing unless WOOLY_SKIN_DUMP names a directory to write into.
public class SkinDump
{
    private static Post Ago(Post post, int minutes) => post with { PostedAt = AShell.Now.AddMinutes(-minutes) };

    private static readonly Post[] Posts =
    [
        Ago(APost.With(id: "1", account: "mira@hachyderm.io", author: "Mira Okafor",
            content: "Finally got the new keyboard firmware building. Turns out the whole problem was one missing semicolon in a macro three files away. #qmk #keyboards"), 3),
        Ago(APost.With(id: "2", account: "sam@fosstodon.org", author: "Sam Whitfield",
            boosted: Ago(APost.With(id: "20", account: "tomas@fosstodon.org", author: "Tomás Reyes",
                content: "Release day! loom 2.0 ships with sixel previews and a rewritten config loader. Changelog: https://loom.dev/2.0", marks: APost.Marked(favorited: true)), 12)), 12),
        Ago(APost.With(id: "3", account: "priya@mastodon.social", author: "Priya Natarajan",
            content: "Long thread on the council vote.", contentWarning: "local politics"), 25),
        Ago(APost.With(id: "4", account: "ada@photog.social", author: "Ada Lindqvist",
            content: "Fog rolling over the harbour this morning, @mira you would love this.",
            media: [APost.APicture(description: "Harbour at dawn, cranes fading into grey fog")]), 41),
        Ago(APost.With(id: "5", account: "kenji@social.coop", author: "Kenji Sato",
            content: "When a service falls over at 3am, what do you reach for first?", poll: APost.APoll(
                [APost.AnAnswer("logs", 48), APost.AnAnswer("metrics", 29), APost.AnAnswer("restart and pray", 23)], votes: 100)), 70),
    ];

    [Fact]
    public async Task Dump()
    {
        var into = Environment.GetEnvironmentVariable("WOOLY_SKIN_DUMP");

        if (into is null)
        {
            return;
        }

        var width = int.Parse(Environment.GetEnvironmentVariable("WOOLY_SKIN_WIDTH") ?? "110");
        var height = int.Parse(Environment.GetEnvironmentVariable("WOOLY_SKIN_HEIGHT") ?? "34");
        var built = new AShell { Timelines = FakeTimelineReader.Holding(Posts), RateLimit = FakeRateLimitReport.Of(212) };
        var shell = await built.Opened();
        shell.Walk(1, null);

        using var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        using var window = new ShellWindow(shell, Themes.Dark, built.Clock, () => { }, FakePictures.DrawingNothing());
        application.Begin(window);
        Directory.CreateDirectory(into);

        // The keys, through the window as a keypress would arrive.
        window.NewKeyDownEvent(new Terminal.Gui.Input.Key('>'));
        var cycled = Skins.Current.Key;
        window.NewKeyDownEvent(new Terminal.Gui.Input.Key('<'));
        File.WriteAllText(Path.Combine(into, "keys.txt"), $"> went to {cycled}, < came back to {Skins.Current.Key}");

        foreach (var skin in Skins.All)
        {
            Skins.Select(skin.Key);
            application.LayoutAndDraw(true);
            File.WriteAllText(Path.Combine(into, $"{skin.Key}.txt"), application.Driver.ToString());
            File.WriteAllText(Path.Combine(into, $"{skin.Key}.ansi"), application.Driver.ToAnsi());
        }

        // B2's Tab and Shift-Tab, through the window as presses arrive, and a link picked with → for its brackets.
        Skins.Select("B2");
        var walked = new List<int>();

        foreach (var press in new[] { Terminal.Gui.Input.Key.Tab, Terminal.Gui.Input.Key.Tab, Terminal.Gui.Input.Key.Tab.WithShift,
                     Terminal.Gui.Input.Key.Tab, Terminal.Gui.Input.Key.Tab.WithShift, Terminal.Gui.Input.Key.Tab.WithShift })
        {
            window.NewKeyDownEvent(press);
            walked.Add(shell.Rail.Cursor);
        }

        File.WriteAllText(Path.Combine(into, "b2-tabs.txt"), "tab tab ⇧tab tab ⇧tab ⇧tab from Home: " + string.Join(" ", walked));
        shell.Rail.GoTo(Wooly.Tui.Shell.DestinationKind.Home);
        built.Host.Drain();
        window.NewKeyDownEvent(Terminal.Gui.Input.Key.CursorRight);
        application.LayoutAndDraw(true);
        File.WriteAllText(Path.Combine(into, "B2-link.ansi"), application.Driver.ToAnsi());

        // And one level down, so the crumbs have a trail to show.
        shell.Do(Keymap.Means(ShellKey.Enter, shell.Screen), null);
        built.Host.Drain();

        foreach (var skin in Skins.All)
        {
            Skins.Select(skin.Key);
            application.LayoutAndDraw(true);
            File.WriteAllText(Path.Combine(into, $"{skin.Key}-post.ansi"), application.Driver.ToAnsi());
        }

        Skins.Select("0");
    }
}
