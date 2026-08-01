using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A screen that has something to say and nothing to do: a destination whose screen belongs to a later ticket, or
///     a hashtag nobody has named yet.
/// </summary>
/// <remarks>
///     A destination that swallowed a keypress and drew the last screen again would read as a bug, so the four the
///     rail lists ahead of their screens land here and say so. Which is also why they are on the rail from the start —
///     the shape of the rail is what this ticket settles, and a rail that grows four entries later is a different rail.
/// </remarks>
public sealed class NoticeScreen(string crumb, string headline, string? aside = null) : Screen
{
    /// <inheritdoc />
    public override string Crumb => crumb;

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys => [new("tab", "destination"), new("?", "keys")];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var lines = new List<Line>(TextWrap.Wrap(headline, width).Select(row => Line.Of(row, Role.Body)));

        if (aside is null)
        {
            return lines;
        }

        lines.Add(Line.Blank);
        lines.AddRange(TextWrap.Wrap(aside, width).Select(row => Line.Of(row, Role.Muted)));

        return lines;
    }
}
