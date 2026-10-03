using Terminal.Gui.App;
using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Prototype;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui.Prototype;

// PROTOTYPE — throwaway. Renders the real shell headlessly with compose open, once per variant and state, to .ansi
// (ansi2png.py makes a picture of one). Does nothing unless WOOLY_COMPOSE_DUMP names a directory to write into.
public class ComposeDump
{
    private static Post Ago(Post post, int minutes) => post with { PostedAt = AShell.Now.AddMinutes(-minutes) };

    private static readonly Post[] Posts =
    [
        Ago(APost.With(id: "1", account: "mira@hachyderm.io", author: "Mira Okafor",
            content: "Finally got the new keyboard firmware building. Turns out the whole problem was one missing semicolon in a macro three files away. #qmk #keyboards"), 3),
        Ago(APost.With(id: "3", account: "priya@mastodon.social", author: "Priya Natarajan",
            content: "Long thread on the council vote.", contentWarning: "local politics"), 25),
        Ago(APost.With(id: "5", account: "kenji@social.coop", author: "Kenji Sato",
            content: "When a service falls over at 3am, what do you reach for first? Logs, metrics, or a restart and a prayer?"), 70),
        Ago(APost.With(id: "6", account: "ada@photog.social", author: "Ada Lindqvist",
            content: "Fog rolling over the harbour this morning, @mira you would love this."), 90),
    ];

    [Fact]
    public async Task Dump()
    {
        var into = Environment.GetEnvironmentVariable("WOOLY_COMPOSE_DUMP");

        if (into is null)
        {
            return;
        }

        var width = int.Parse(Environment.GetEnvironmentVariable("WOOLY_DUMP_WIDTH") ?? "120");
        var height = int.Parse(Environment.GetEnvironmentVariable("WOOLY_DUMP_HEIGHT") ?? "36");
        var built = new AShell { Timelines = FakeTimelineReader.Holding(Posts), RateLimit = FakeRateLimitReport.Of(212) };
        var shell = await built.Opened();

        using var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(width, height);

        using var window = new ShellWindow(shell, Themes.Dark, built.Clock, () => { }, FakePictures.DrawingNothing());
        application.Begin(window);
        Directory.CreateDirectory(into);

        void Snap(string name)
        {
            foreach (var variant in ComposeVariants.All)
            {
                ComposeVariants.Select(variant.Key);
                application.LayoutAndDraw(true);
                File.WriteAllText(Path.Combine(into, $"{variant.Key}-{name}.ansi"), application.Driver.ToAnsi());
            }
        }

        void Type(string text)
        {
            foreach (var letter in text)
            {
                window.NewKeyDownEvent(letter == '\n' ? Key.Enter : new Key(letter));
            }
        }

        // A fresh post, nothing written.
        shell.Compose();
        built.Host.Drain();
        Snap("empty");

        // Written, with a warning, then the warning taking the typing.
        Type("Spent the weekend rebuilding my terminal setup from scratch.\n\nThe thing that finally made it click: fewer boxes, more whitespace.");
        shell.WriteWarning();
        Type("terminal nerdery");
        Snap("warning");
        shell.WriteWarning();
        Snap("written");

        // A reply, from the feed's picked post.
        shell.Back();
        built.Host.Drain();
        window.NewKeyDownEvent(Key.CursorDown);
        shell.Reply();
        built.Host.Drain();
        Type("Same here, the missing-semicolon-three-files-away bug is a rite of passage.");
        Snap("reply");

        ComposeVariants.Select("A");
    }
}
