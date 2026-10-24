using Wooly.Core.Notifications;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A <b>Notification group</b>'s avatar strip: its actors' faces at a byline's size, side by side on two rows of
///     their own, then a muted <c>+N</c> on the top row counting the rest of the group (layout A7b of #450, #454).
/// </summary>
/// <remarks>
///     Where each face goes depends on the width alone: face <c>n</c> starts <c>n × </c><see cref="Step" /> columns
///     into the strip whatever the group holds, and how many there is room for is <see cref="Fits" /> of the width —
///     so two groups of different sizes put their faces in the same columns, and nothing jumps as a group grows. A
///     group shows as many as fit, up to the sample the instance sent, in the instance's order.
///     <para>
///         Where this terminal draws no pictures the strip takes no rows at all, as a byline's avatar takes no columns
///         (ADR-0016): the names line already says who. An actor the instance named no avatar for keeps its slot,
///         blank, so that face <c>n</c> is always actor <c>n</c>: each face's cells, on both rows, stand for its slot
///         (<see cref="Span.Item" />), which is how a click finds whose face it hit (#456). The gaps and the
///         <c>+N</c> stand for nothing.
///     </para>
/// </remarks>
public static class AvatarStrip
{
    /// <summary>How many columns stand between one face and the next (#450: one was too close).</summary>
    public const int Gap = 2;

    /// <summary>How many columns from one face's left edge to the next's.</summary>
    public const int Step = Avatar.BylineColumns + Gap;

    /// <summary>
    ///     The columns held for the <c>+N</c> after the last face, whether or not there is one, so how many faces fit
    ///     cannot depend on how many actors are left over: enough for <c>+9999</c>, and for any count at all once it
    ///     is said in thousands, millions or billions (<see cref="More" />).
    /// </summary>
    private const int Rest = 5;

    /// <summary>The units a count too long for <see cref="Rest" /> is said in, smallest first.</summary>
    private static readonly (long Of, string Unit)[] Units = [(1_000, "k"), (1_000_000, "M"), (1_000_000_000, "G")];

    /// <summary>
    ///     The <c>+N</c> for <paramref name="rest" /> actors left over, never wider than <see cref="Rest" />: exact while
    ///     it fits, then in the smallest unit that fits, rounded down so it never says more than there are.
    /// </summary>
    private static string More(int rest)
    {
        var exact = $"+{rest}";

        return exact.Length <= Rest
            ? exact
            : Units.Select(unit => $"+{rest / unit.Of}{unit.Unit}").First(said => said.Length <= Rest);
    }

    /// <summary>How many faces a strip <paramref name="width" /> wide has room for.</summary>
    public static int Fits(int width) => Math.Max(0, (width - Rest) / Step);

    /// <summary>How many of <paramref name="group" />'s actors a strip <paramref name="width" /> wide shows.</summary>
    public static int Shown(NotificationGroup group, int width) => Math.Min(group.Actors.Count, Fits(width));

    /// <summary>
    ///     The strip's rows, <paramref name="width" /> wide at most, or none where this terminal draws no pictures or
    ///     no actor has a face to draw. The top row names every face it holds, in order, for the frame to send for.
    /// </summary>
    public static IReadOnlyList<Line> Of(NotificationGroup group, Drawing drawing, int width)
    {
        if (!Drawing.Draws(drawing.Pictures, drawing.Raster, out _))
        {
            return [];
        }

        var boxes = group.Actors
            .Take(Shown(group, width))
            .Select(actor => Avatar.Byline(actor.Address, actor.AvatarUrl, drawing.Pictures, drawing.Raster).Box)
            .ToList();

        var wanted = boxes.Select(box => box?.Wanted).OfType<Media.Drawn>().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var gap = new Span(new string(' ', Gap), Role.Body);
        var blank = new Span(new string(' ', Avatar.BylineColumns), Role.Body);
        var rest = group.Count - boxes.Count;

        return
        [
            .. Enumerable.Range(0, Avatar.BylineRows).Select(row =>
            {
                List<Span> spans =
                [
                    .. boxes.SelectMany((box, at) => new[] { (box?.Cells(row) ?? blank) with { Item = at }, gap }),
                ];

                if (row > 0)
                {
                    return new Line(spans);
                }

                if (rest > 0)
                {
                    spans.Add(new Span(TextWrap.Clip(More(rest), Math.Max(0, width - boxes.Count * Step)), Role.Muted));
                }

                return new Line(spans)
                {
                    Insets = [.. boxes.SelectMany((box, at) => box?.Insets(row, at * Step) ?? [])],
                    Wants = wanted[0],
                    AlsoWants = [.. wanted.Skip(1)],
                };
            }),
        ];
    }
}
