using Wooly.Tui.Prototype.Breadcrumb;

// THROWAWAY PROTOTYPE — issue #168: what the breadcrumb says, and how it stops blending in.
//
// Prints the breadcrumb row at the 61 columns of content an 80-column terminal leaves, in dark, light and mono,
// across the seven candidates for telling the current crumb from its ancestors and the three for what a long trail
// loses. Nothing here is production: the roles the candidates want do not exist in `Themes` yet, so `Ink` carries
// candidate colours in the same hex the built-ins are written in.
//
//   dotnet run --project src/Wooly.Tui.Prototype.Breadcrumb
//
// THROWAWAY PROTOTYPE — issue #213: the fetch mark, whose dots arrive one at a time. Those scenes move, so they are
// drawn in place until a key is pressed rather than printed.
//
//   dotnet run --project src/Wooly.Tui.Prototype.Breadcrumb -- move

// `-- move` runs #213's scenes instead: the mark is a question about motion, and a printed row cannot answer one.
if (args.Contains("move"))
{
    Moving.Play();

    return;
}

const int width = 61;
const int wide = 100;

// The stack as the shell spells it today: `FeedScreen` lowercases the rail's own label, `DiscoverScreen` does not.
string[] two = ["home", "post by @maria"];

Section("CASING — the same stacks, spelled three ways");

Casing("today", ["home", "Discover", "search cats", "post by @maria", "follow requests", "keys", "reply to @maria"]);
Casing("rail labels as the rail spells them, the rest prose",
    ["Home", "Discover", "search cats", "post by @maria", "follow requests", "keys", "reply to @maria"]);
Casing("sentence case throughout",
    ["Home", "Discover", "Search cats", "Post by @maria", "Follow requests", "Keys", "Reply to @maria"]);

Section($"THE CURRENT CRUMB — {width} columns, the stack four deep");

foreach (var style in Trail.Styles)
{
    Console.WriteLine($"{style.Name} — {style.Cost}");
    Console.WriteLine();

    foreach (var (name, ink) in Terminals)
    {
        Console.WriteLine($"  {name}");
        Console.WriteLine("  " + Rule(width));
        Console.WriteLine("  " + Draw(["Home", "post by @maria", "@maria"], style, Elide.FromLeft, false, width, ink));
        Console.WriteLine("  " + Draw(["Home"], style, Elide.FromLeft, false, width, ink));
        Console.WriteLine();
    }
}

Section($"A TRAIL TOO LONG — five deep at {width} columns, drawn in candidate 5");

var five = new[]
{
    "Home", "post by @maria@mastodon.social", "@maria@mastodon.social", "@maria@mastodon.social following",
    "keys",
};

foreach (var elide in new[] { Elide.ClipRight, Elide.FromLeft, Elide.DropMiddle })
{
    Console.WriteLine($"  {elide}");
    Console.WriteLine("  " + Rule(width));

    foreach (var (name, ink) in Terminals)
    {
        Console.WriteLine("  " + Draw(five, Trail.Styles[4], elide, false, width, ink) + $"   ({name})");
    }

    Console.WriteLine();
}

Section($"THE FETCH MARK — `fetching…` at the right, {width} columns");

foreach (var style in new[] { Trail.Styles[0], Trail.Styles[4], Trail.Styles[6] })
{
    Console.WriteLine($"  {style.Name}");
    Console.WriteLine("  " + Rule(width));
    Console.WriteLine("  " + Draw(two, style, Elide.FromLeft, true, width, Ink.Dark));
    Console.WriteLine("  " + Draw(five, style, Elide.FromLeft, true, width, Ink.Dark));
    Console.WriteLine("  " + Draw(five, style, Elide.FromLeft, true, width, Ink.Mono));
    Console.WriteLine();
}

Section($"WIDER — {wide} columns, where nothing has to be cut");

foreach (var style in Trail.Styles)
{
    Console.WriteLine($"  {Draw(five, style, Elide.FromLeft, false, wide, Ink.Dark)}  {style.Name}");
}

Console.WriteLine();

return;

static string Draw(
    IReadOnlyList<string> crumbs,
    Style style,
    Elide elide,
    bool fetching,
    int width,
    Ink ink) =>
    ink.Print(Trail.Row(crumbs, style, elide, fetching, width), style.Banded);

static string Rule(int width) => new('─', width);

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 78));
    Console.WriteLine();
}

static void Casing(string name, IReadOnlyList<string> crumbs)
{
    Console.WriteLine($"  {name}");

    foreach (var crumb in crumbs)
    {
        Console.WriteLine($"    Home › {crumb}");
    }

    Console.WriteLine();
}

public partial class Program
{
    /// <summary>The three terminals every candidate has to hold in.</summary>
    private static readonly IReadOnlyList<(string Name, Ink Ink)> Terminals =
        [("dark", Ink.Dark), ("light", Ink.Light), ("mono", Ink.Mono)];
}
