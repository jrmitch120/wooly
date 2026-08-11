using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A screen that has something to say and nothing to do, which is now only the rail's hashtag before anybody has
///     named one.
/// </summary>
/// <remarks>
///     A destination that swallowed a keypress and drew the last screen again would read as a bug, so a destination
///     with nothing to show lands here and says so. Every other one of the nine has a list of its own; this held the
///     four of them that arrived ahead of their screens, which is why the rail could carry its whole shape from #28.
/// </remarks>
public sealed class NoticeScreen(string crumb, string headline, string? aside = null) : Screen
{
    /// <inheritdoc />
    public override string Crumb => crumb;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys => [new("tab", "destination"), new("?", "keys")];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
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
