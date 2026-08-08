using Wooly.Tui.Prototype.Separator;
using Wooly.Tui.Rendering;

// THROWAWAY PROTOTYPE — issue #62: what marks the boundary between one post and the next in a feed?
//
// Renders the real Picked<T>/PostLines pipeline through the five candidates the ticket names, at 61 columns, with
// the roles stripped off (Line.Text) since the answer has to hold with no colour in the room. Post #2 carries a
// picture, which is where a rule risks reading as part of the image.
//
//   dotnet run --project src/Wooly.Tui.Prototype.Separator

const int width = 61;
const int pickedIndex = 1;

var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
var posts = Feed.Posts(now);
var pictures = new FakePictures();

foreach (var variant in Variants.All)
{
    Console.WriteLine(new string('=', width));
    Console.WriteLine(variant.Name);
    Console.WriteLine(variant.Cost);
    Console.WriteLine(new string('=', width));
    Console.WriteLine();

    var pictureRowsLeft = 0;

    foreach (var line in variant.Rows(posts, pickedIndex, width, now, pictures))
    {
        if (line.Insets.Count > 0)
        {
            pictureRowsLeft = line.Insets[0].Rows - 1;
        }
        else if (pictureRowsLeft > 0 && line.Spans.Count == 0)
        {
            // A blank row that is really a row of the picture's box (Screens/Picked.cs's Box only tags the top row
            // with an Inset) — filled in so the prototype shows what a sixel/Kitty terminal would paint here, rather
            // than a blank a reader could mistake for the separator itself.
            Console.WriteLine(new string('▓', width));
            pictureRowsLeft--;

            continue;
        }
        else
        {
            pictureRowsLeft = 0;
        }

        Console.WriteLine(Printed(line, width));
    }

    Console.WriteLine();
}

return;

static string Printed(Line line, int width)
{
    if (line.Insets.Count == 0)
    {
        return line.Text;
    }

    var inset = line.Insets[0];
    var text = line.Text.PadRight(width);

    return text[..inset.Column] + new string('▓', inset.Columns) + text[(inset.Column + inset.Columns)..];
}
