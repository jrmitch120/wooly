using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui.Prototype;

/// <summary>PROTOTYPE (#602): prints each prompt style, typing and not, to /tmp/wooly-602 for eyeballing. Not a test.</summary>
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
            foreach (var query in new[] { "", "cats" })
            {
                var search = new SearchScreen();

                foreach (var letter in query)
                {
                    search.Type(letter);
                }

                lines.Add($"=== style {style + 1}, '{query}', typing ===");
                lines.AddRange(search.Lines(new Drawing(Width: 72, Now: AShell.Now)).Take(5).Select(line => line.Text));
                search.Move(1);
                lines.Add($"=== style {style + 1}, '{query}', on a trend (IsTyping={search.IsTyping}) ===");
                lines.AddRange(search.Lines(new Drawing(Width: 72, Now: AShell.Now)).Take(5).Select(line => line.Text));
            }

            SearchTrendsPrototype.NextPrompt();
        }

        File.WriteAllLines("/tmp/wooly-602/prompts.txt", lines);
    }
}
