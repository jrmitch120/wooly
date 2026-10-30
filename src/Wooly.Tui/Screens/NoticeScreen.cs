using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A screen that has something to say and nothing to do.
/// </summary>
/// <remarks>
///     A destination that swallowed a keypress and drew the last screen again would read as a bug, so a destination
///     with nothing to show landed here and said so. It held the four that arrived ahead of their screens, which is why
///     the rail could carry its whole shape from #28, and last the rail's Hashtag before a tag was named — which tag
///     sets replaced with no entry at all (#511). No destination lands here now.
/// </remarks>
public sealed class NoticeScreen(string crumb, string headline, string? aside = null) : Screen
{
    /// <inheritdoc />
    public override string Crumb => crumb;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys => [.. PostKeys.AlongTheRail, PostKeys.Asking];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>(TextWrap.Wrap(headline, drawing.Width).Select(row => Line.Of(row, Role.Body)));

        if (aside is null)
        {
            return lines;
        }

        lines.Add(Line.Blank);
        lines.AddRange(TextWrap.Wrap(aside, drawing.Width).Select(row => Line.Of(row, Role.Muted)));

        return lines;
    }
}
