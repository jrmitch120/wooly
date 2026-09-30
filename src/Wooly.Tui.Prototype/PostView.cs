namespace Wooly.Tui.Prototype;

// PROTOTYPE — throwaway. The one piece the variants share: a post written out in full inside whatever box a variant
// gives it. Everything around it — where it sits, what frames it — is each variant's own.

internal static class PostView
{
    /// <summary>Draws a post from (x, y) within w columns and at most h rows. Returns the rows it used.</summary>
    public static int Draw(Surface s, Post p, int x, int y, int w, int h, uint bg, bool byline = true, bool stats = true)
    {
        var row = y;
        var bottom = y + h;

        if (p.BoostedBy is not null && row < bottom)
        {
            s.Put(x, row++, $"↺ {p.BoostedBy} boosted", P.A(P.Green, bg), w);
        }

        if (byline && row < bottom)
        {
            var col = s.Put(x, row, p.Name, P.B(P.Text, bg), w);
            col = s.Put(col + 1, row, p.Handle, P.A(P.Overlay, bg), x + w - col - 1);
            var right = $"{p.AudienceMark} {p.Ago}";
            s.Put(x + w - right.Length, row, right, P.A(P.Overlay, bg));
            row++;
            row++;
        }

        if (p.Warning is not null && row < bottom)
        {
            s.Put(x, row++, $"⚠ {p.Warning}", P.B(P.Peach, bg), w);

            if (!p.Revealed)
            {
                if (row < bottom)
                {
                    s.Put(x, row++, "press x to show", P.I(P.Overlay, bg), w);
                }

                return Stats(s, p, x, row, w, bottom, bg, stats) - y;
            }

            row++;
        }

        foreach (var line in Text.Wrap(p.Text, w))
        {
            if (row >= bottom)
            {
                return row - y;
            }

            Text.Rich(s, x, row++, line, bg);
        }

        if (p.Media is not null)
        {
            row++;

            // A stand-in for the sixel/kitty picture the real client draws here.
            var pw = Math.Min(w, 36);
            for (var r = 0; r < 5 && row < bottom; r++)
            {
                s.Put(x, row++, new string('▒', pw), P.A(P.Surface2, bg));
            }

            foreach (var line in Text.Wrap("▣ " + p.Media, w))
            {
                if (row < bottom)
                {
                    s.Put(x, row++, line, P.I(P.Teal, bg), w);
                }
            }
        }

        if (p.Poll is not null)
        {
            row++;

            foreach (var option in p.Poll)
            {
                if (row < bottom)
                {
                    s.Put(x, row++, option, P.A(P.Lavender, bg), w);
                }
            }
        }

        return Stats(s, p, x, row, w, bottom, bg, stats) - y;
    }

    private static int Stats(Surface s, Post p, int x, int row, int w, int bottom, uint bg, bool stats)
    {
        if (!stats || row + 1 >= bottom)
        {
            return row;
        }

        row++;
        var col = s.Put(x, row, $"↩ {p.Replies}", P.A(P.Overlay, bg));
        col = s.Put(col + 3, row, $"↺ {p.Boosts}", P.A(p.Boosted ? P.Green : P.Overlay, bg));
        s.Put(col + 3, row, $"★ {p.Favs}", P.A(p.Faved ? P.Yellow : P.Overlay, bg));

        return row + 1;
    }

    /// <summary>How many rows <see cref="Draw" /> would use at this width, for variants that lay posts out one after another.</summary>
    public static int Height(Post p, int w, bool byline = true, bool stats = true)
    {
        var rows = (p.BoostedBy is not null ? 1 : 0) + (byline ? 2 : 0);

        if (p.Warning is not null)
        {
            rows++;

            if (!p.Revealed)
            {
                return rows + 1 + (stats ? 2 : 0);
            }

            rows++;
        }

        rows += Text.Wrap(p.Text, w).Count;

        if (p.Media is not null)
        {
            rows += 1 + 5 + Text.Wrap("▣ " + p.Media, w).Count;
        }

        if (p.Poll is not null)
        {
            rows += 1 + p.Poll.Length;
        }

        return rows + (stats ? 2 : 0);
    }
}
