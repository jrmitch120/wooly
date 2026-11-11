using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui.Prototype;

/// <summary>PROTOTYPE (#602): prints each trends layout to /tmp/wooly-602 for eyeballing. Not a test.</summary>
public class SearchTrendsPrototypePrint
{
    [Fact]
    public void Print()
    {
        Directory.CreateDirectory("/tmp/wooly-602");

        for (var layout = 0; layout < 4; layout++)
        {
            var search = new SearchScreen();

            foreach (var width in new[] { 80, 100 })
            {
                var lines = search.Lines(new Drawing(Width: width, Now: AShell.Now));
                File.WriteAllLines($"/tmp/wooly-602/{(char)('A' + layout)}-{width}.txt",
                    [$"{lines.Count} rows", .. lines.Select(line => line.Text)]);
            }

            SearchTrendsPrototype.Next();
        }
    }
}
