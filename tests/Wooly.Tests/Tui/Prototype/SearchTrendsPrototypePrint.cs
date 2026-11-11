using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui.Prototype;

/// <summary>PROTOTYPE (#602): prints the top of the screen and each links style to /tmp/wooly-602. Not a test.</summary>
public class SearchTrendsPrototypePrint
{
    [Fact]
    public void Print()
    {
        Directory.CreateDirectory("/tmp/wooly-602");
        SearchTrendsPrototype.Instance = "mastodon.social";
        var lines = new List<string>();

        for (var style = 0; style < 4; style++)
        {
            var drawn = new SearchScreen().Lines(new Drawing(Width: 80, Now: AShell.Now)).Select(line => line.Text).ToList();

            if (style == 0)
            {
                lines.AddRange(drawn.Take(8));
            }

            lines.Add($"=== links style {style + 1} ===");
            lines.AddRange(drawn.SkipWhile(text => !text.Contains("Trending links")).Take(14));
            SearchTrendsPrototype.NextLinks();
        }

        File.WriteAllLines("/tmp/wooly-602/links.txt", lines);
    }
}
