using Wooly.Tui.Prototype.StatusRow;
using Wooly.Tui.Screens;

// THROWAWAY PROTOTYPE — issue #169: a status row that says less and truncates never.
//
// Draws the status row at the 80 columns the narrow case gives it — the row is full width, unlike the breadcrumb's 61 —
// across the six states the shell can be in, worst first, and the nine candidates for what the row should say. Nothing
// here is production: the overflow mark wants a role that does not exist in `Themes` yet, so `Ink` carries candidate
// colours in the same hex the built-ins are written in.
//
//   dotnet run --project src/Wooly.Tui.Prototype.StatusRow

const int width = 80;
const int roomy = 100;
const int wide = 120;

(string Name, Ink Ink)[] terminals = [("dark", Ink.Dark), ("light", Ink.Light), ("mono", Ink.Mono)];

Section($"THE ARITHMETIC — what the row wants, against the {width} columns it has");

Console.WriteLine($"  {"case",-52}{"hints",6}{"columns",9}{"fit",5}{"?",4}");
Console.WriteLine("  " + new string('─', 76));

foreach (var row in Cases.All)
{
    var wants = Candidates.Width(Hints(row.Keys));
    var today = Candidates.All[0].Draw(row, width);
    var fit = row.Keys.Count - today.Silent;

    Console.WriteLine(
        $"  {Short(row.Name),-52}{row.Keys.Count,6}{wants,9}{fit,5}{(today.Asks ? "yes" : "no"),4}");
}

Console.WriteLine();
Console.WriteLine($"  Nothing fits. The row holds six or seven hints; the worst screen announces {Cases.All[0].Keys.Count}.");
Console.WriteLine("  `?:keys` is last in the list, so on every one of these the reference itself is what falls off.");
Console.WriteLine();

Section($"THE CANDIDATES — {width} columns, dark, worst case first");

foreach (var row in Cases.All)
{
    Console.WriteLine($"  {row.Name}");
    Console.WriteLine($"  {row.What}");
    Console.WriteLine();

    foreach (var candidate in Candidates.All)
    {
        var drawn = candidate.Draw(row, width);

        Console.WriteLine($"  {candidate.Name}");
        Console.WriteLine("  " + Rule(width));

        foreach (var line in drawn.Rows)
        {
            Console.WriteLine("  " + Ink.Dark.Print(line));
        }

        Console.WriteLine($"  {Verdict(drawn)}");
        Console.WriteLine();
    }

    Console.WriteLine();
}

Section($"THE WORST CASE IN THREE TERMINALS — {width} columns");

foreach (var candidate in new[] { Candidates.All[0], Candidates.All[5], Candidates.All[7], Candidates.All[8] })
{
    Console.WriteLine($"  {candidate.Name}");
    Console.WriteLine("  " + Rule(width));

    foreach (var (name, ink) in terminals)
    {
        foreach (var line in candidate.Draw(Cases.All[0], width).Rows)
        {
            Console.WriteLine("  " + ink.Print(line) + $"   ({name})");
        }
    }

    Console.WriteLine();
}

Section("THE MARK'S SPELLING — how the row says there are more");

foreach (var mark in new[] { "…+9", "+9", "…", "…9 more", "…+9 in ?" })
{
    Console.WriteLine("  " + Rule(width));

    foreach (var (name, ink) in terminals)
    {
        Console.WriteLine("  " + ink.Print(Spelt(mark)) + $"   ({name})");
    }

    Console.WriteLine($"  as `{mark}`, {Candidates.Width(Spelt(mark))} columns");
    Console.WriteLine();
}

Section($"THE NOTICE AND THE CONFIRMATION — the two rows that displace the keys, {width} columns");

var rows = new (string What, IReadOnlyList<Ribbon> Row)[]
{
    ("a notice, today — the whole row",
        [new Ribbon(" Boosted.", Paint.Notice)]),
    ("a long notice, today",
        [new Ribbon(" That mention couldn't be resolved.", Paint.Notice)]),
    ("a notice with `?` kept at the right — a candidate",
        [
            new Ribbon(" That mention couldn't be resolved.", Paint.Notice),
            new Ribbon(new string(' ', 34), Paint.Sep), new Ribbon("?", Paint.Key), new Ribbon(":keys", Paint.Does),
        ]),
    ("a confirmation, today",
        [
            new Ribbon(" Delete post 114 8273 9902 1188 47? This cannot be undone.", Paint.Ask),
            new Ribbon("  y delete · esc keep", Paint.Does),
        ]),
    ("a vote confirmation, today — the longest the shell writes",
        [
            new Ribbon(" Vote for “Thursday” in this poll? This cannot be undone.", Paint.Ask),
            new Ribbon("  y vote · esc keep", Paint.Does),
        ]),
};

foreach (var (what, row) in rows)
{
    Console.WriteLine($"  {what} — {Candidates.Width(row)} columns");
    Console.WriteLine("  " + Rule(width));

    foreach (var (name, ink) in terminals)
    {
        Console.WriteLine("  " + ink.Print(row) + $"   ({name})");
    }

    Console.WriteLine();
}

Section("EVERY OTHER WIDTH — a terminal narrower than the narrow case, and two with room to spare");

foreach (var at in new[] { 40, 61, roomy, wide })
{
    Console.WriteLine($"  {at} columns");
    Console.WriteLine("  " + Rule(at));

    foreach (var candidate in new[] { Candidates.All[0], Candidates.All[9] })
    {
        foreach (var line in candidate.Draw(Cases.All[0], at).Rows)
        {
            Console.WriteLine("  " + Ink.Dark.Print(line) + $"   ({candidate.Name})");
        }
    }

    Console.WriteLine();
}

return;

static IReadOnlyList<Ribbon> Hints(IReadOnlyList<KeyHint> keys) =>
[
    new Ribbon(" ", Paint.Sep),
    .. keys.SelectMany(
        (key, i) => i == 0
            ? new[] { new Ribbon(key.Key, Paint.Key), new Ribbon($":{key.Does}", Paint.Does) }
            : [new Ribbon(" · ", Paint.Sep), new Ribbon(key.Key, Paint.Key), new Ribbon($":{key.Does}", Paint.Does)]),
];

static IReadOnlyList<Ribbon> Spelt(string mark) =>
[
    new Ribbon(" j/k", Paint.Key), new Ribbon(":thread", Paint.Does),
    new Ribbon(" · ", Paint.Sep), new Ribbon("1-0", Paint.Key), new Ribbon(":option", Paint.Does),
    new Ribbon(" · ", Paint.Sep), new Ribbon("v", Paint.Key), new Ribbon(":vote", Paint.Does),
    new Ribbon(" · ", Paint.Sep), new Ribbon("g", Paint.Key), new Ribbon(":refresh", Paint.Does),
    new Ribbon(" · ", Paint.Sep), new Ribbon("esc", Paint.Key), new Ribbon(":back", Paint.Does),
    new Ribbon(" · ", Paint.Sep), new Ribbon(mark, Paint.More),
    new Ribbon(" · ", Paint.Sep), new Ribbon("?", Paint.Key), new Ribbon(":keys", Paint.Does),
];

static string Verdict(Drawn drawn)
{
    var columns = drawn.Rows.Max(Candidates.Width);
    var said = drawn.Silent == 0 ? "nothing cut silently" : $"{drawn.Silent} hints cut with no sign of it";

    return $"  → {columns} columns, {drawn.Rows.Count} row{(drawn.Rows.Count == 1 ? "" : "s")}, {said}, "
        + $"`?` {(drawn.Asks ? "survives" : "is gone")}";
}

static string Short(string name) => name.Length <= 52 ? name : name[..51] + "…";

static string Rule(int at) => new('─', at);

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine(new string('=', title.Length));
    Console.WriteLine();
}
