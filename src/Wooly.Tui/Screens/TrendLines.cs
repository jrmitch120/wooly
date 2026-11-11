using Wooly.Core;
using Wooly.Core.Search;
using Wooly.Core.Trends;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How a <b>Trend</b> is drawn under the empty search prompt (#605): a trending hashtag on one row, a
///     <b>Trending link</b> on three, and the heading over each kind's run. A trending post is the ordinary post block
///     and is drawn as one (<see cref="PostLines" />), which is why it has nothing here.
/// </summary>
/// <remarks>
///     Every column here is worked out from the room a row is given and never from what is in it, so that nothing on
///     the page moves as the trends change under it: a hashtag's count stands against the right edge whatever the tag
///     is called, and a link's rows are clipped to the room rather than measured against one another.
/// </remarks>
internal static class TrendLines
{
    /// <summary>How far a hashtag's name stands off its count, at the least, so the two never run together.</summary>
    private const int Gap = 2;

    /// <summary>The heading over the run of <paramref name="kind" />: what it is, and no count (#602).</summary>
    /// <remarks>
    ///     No count, unlike a search's headings: the instance was asked for a fixed number of each, so a count would say
    ///     the number asked for back, and nothing about whether there is more.
    /// </remarks>
    public static Line Heading(TrendKind kind, int room) =>
        Line.Of(TextWrap.Clip($"── {Called(kind)} ──", room), Role.Muted).Heading();

    /// <summary>
    ///     What a run of <paramref name="kind" /> is called — <c>Trending hashtags</c> rather than the CLI's
    ///     <c>tags</c>, which is a spelling for <c>--type</c> rather than a word to read — and what a screen of that kind
    ///     alone would be crumbed with.
    /// </summary>
    public static string Called(TrendKind kind) => kind switch
    {
        TrendKind.Hashtags => "Trending hashtags",
        TrendKind.Links => "Trending links",
        _ => "Trending posts",
    };

    /// <summary>
    ///     A trending hashtag: the tag, and how many people used it over the days the instance reported — the count
    ///     against the right edge of <paramref name="room" />, where a digit lines up with the one above it.
    /// </summary>
    /// <remarks>
    ///     <c>person</c> and <c>people</c> being as long as each other, every count's last digit stands in the same
    ///     column, so two tags compare at a glance down the run. The tag's name gives way to the count where the room
    ///     runs short, being the longer and the less needed of the two once it has been started.
    /// </remarks>
    public static Line Hashtag(Hashtag hashtag, int room)
    {
        var people = hashtag.RecentAccounts == 1 ? "person" : "people";
        var used = $"{Number.Of(hashtag.RecentAccounts)} {people} in recent days";
        var usedWidth = Glyphs.Columns(used);

        if (usedWidth + Gap >= room)
        {
            return Line.Of(TextWrap.Clip($"#{hashtag.Name}", room), Role.BylineHandle);
        }

        var name = TextWrap.Clip($"#{hashtag.Name}", room - usedWidth - Gap);
        var pad = new string(' ', room - usedWidth - Glyphs.Columns(name));

        return Line.Of([new Span(name, Role.BylineHandle), new Span(pad + used, Role.Muted)]);
    }

    /// <summary>
    ///     A trending link on three rows: its title, what it says of itself (muted), then whose site it is on and how
    ///     many people are sharing it — the second row left out where the article said nothing of itself, rather than
    ///     standing blank. No picture: a list of links read down is read for their titles (#602).
    /// </summary>
    public static IReadOnlyList<Line> Link(TrendingLink link, int room)
    {
        var sharing = $"{Number.Of(link.PeopleSharing)} {(link.PeopleSharing == 1 ? "person" : "people")} sharing";
        var said = link.Site is { Length: > 0 } site ? $"{OneRow(site)} · {sharing}" : sharing;

        return
        [
            Line.Of(TextWrap.Clip(OneRow(link.Title), room), Role.Body),
            .. link.Description is { } description && !string.IsNullOrWhiteSpace(description)
                ? [Line.Of(TextWrap.Clip(OneRow(description), room), Role.Muted)]
                : Array.Empty<Line>(),
            Line.Of([new Span(TextWrap.Clip(said, room), Role.Link)]),
        ];
    }

    /// <summary><paramref name="text" /> on one row, whatever breaks the instance passed on from the article.</summary>
    private static string OneRow(string text) => text.ReplaceLineEndings(" ").Trim();
}
