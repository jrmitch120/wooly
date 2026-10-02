namespace Wooly.Tui.Theme;

/// <summary>
///     What a drawn thing <em>is</em>, said in a way a theme can answer. Nothing in the TUI constructs a colour: a view
///     names one of these and the theme resolves it to an attribute (ADR-0014, CONTEXT.md). The table in
///     <c>docs/tui-shell.md</c> is the contract, and every member here is a row of it.
///     <para>
///         Distinct from Terminal.Gui's own <c>VisualRole</c>, which describes what a widget is doing —
///         <c>Normal</c>, <c>Focus</c>, <c>Disabled</c> — and has no word for what a boost is.
///     </para>
/// </summary>
public enum Role
{
    /// <summary>A post's text.</summary>
    Body,

    /// <summary>A tag inside a post's text. Carried without colour by the <c>#</c>.</summary>
    Hashtag,

    /// <summary>
    ///     An account named inside a post's text. Carried without colour by the <c>@</c>. Kept apart from
    ///     <see cref="BylineHandle" />: a byline is who wrote this, a mention is somebody else being named.
    /// </summary>
    Mention,

    /// <summary>
    ///     An address inside a post's text. Carried without colour by the scheme. Kept apart from
    ///     <see cref="Media" />, which paints the address of something attached.
    /// </summary>
    Link,

    /// <summary>
    ///     Timestamps, counts nobody acted on, hints, and the crumbs walked through on the content panel's title.
    ///     Carried without colour by position.
    /// </summary>
    Muted,

    /// <summary>A display name.</summary>
    BylineName,

    /// <summary>A <c>username@instance</c>. Carried without colour by the <c>@</c>.</summary>
    BylineHandle,

    /// <summary>The visibility mark. Carried without colour by <c>○ ◌ ● ✉</c>.</summary>
    Audience,

    /// <summary>A warning and its text. Carried without colour by <c>⚠</c>.</summary>
    ContentWarning,

    /// <summary>
    ///     Image placeholders, attachment links, and the columns a byline holds open for an author's avatar. Carried
    ///     without colour by <c>▒▒▒▒</c> and <c>⏵</c>. Also the role a <c>Video</c>/<c>Animation</c>/<c>Audio</c>/
    ///     <c>Unknown</c> attachment's own <see cref="Rendering.Reference" /> draws and is opened by, and a link
    ///     preview's own since #116 — the same vocabulary <see cref="Rendering.Reference" /> already struck for
    ///     <c>Hashtag</c>, <c>Mention</c> and <c>Link</c> (ADR-0017, ADR-0018).
    /// </summary>
    Media,

    /// <summary>A poll's options and their bars. Carried without colour by the bar itself.</summary>
    Poll,

    /// <summary>
    ///     The brackets around a picked reference — a hashtag, mention, or address picked inside a post's text.
    ///     Independent of whatever role the bracketed text already carries. Carried without colour by <c>‹ ›</c>,
    ///     always drawn.
    /// </summary>
    ReferencePicked,

    /// <summary>The boost mark. Carried without colour by <c>↺</c>.</summary>
    Boost,

    /// <summary>The boost mark where the boost is this profile's own.</summary>
    BoostMine,

    /// <summary>The favorite mark. Carried without colour by <c>★</c>.</summary>
    Favorite,

    /// <summary>The favorite mark where the favorite is this profile's own.</summary>
    FavoriteMine,

    /// <summary>
    ///     The reply count under a post. Its own role rather than <see cref="Muted" />, so that it reads as a count
    ///     beside the boosts and favorites rather than as a timestamp. Carried without colour by <c>↩</c>.
    /// </summary>
    Replies,

    /// <summary>The selected row. Carried without colour by <c>▌</c> in the gutter.</summary>
    Selection,

    /// <summary>
    ///     Behind every row of the picked thing, not only the one its <see cref="Selection" /> mark is on. A
    ///     background role. Carried without colour by the <c>▌</c> beside each row.
    /// </summary>
    Band,

    /// <summary>A rail destination.</summary>
    Rail,

    /// <summary>
    ///     The rail destination that is selected: in colour its band, with no mark (ADR-0021). Carried without colour
    ///     by <c>▷</c> while it differs from the cursor's row, which takes <c>▶</c>; the two coincide at rest, so only
    ///     <c>▶</c> shows.
    /// </summary>
    RailCurrent,

    /// <summary>
    ///     The rail entry the tabbing has got to, while the selection has not yet followed it. In colour its band;
    ///     without, <c>▶</c> (ADR-0021).
    /// </summary>
    RailCursor,

    /// <summary>An unread count on the rail. Carried without colour by the number's presence.</summary>
    RailUnread,

    /// <summary>Rate-limit budget left.</summary>
    Quota,

    /// <summary>Rate-limit budget nearly spent.</summary>
    QuotaLow,

    /// <summary>The API budget's filled cells. Carried without colour by <c>█</c>, and the percentage.</summary>
    Gauge,

    /// <summary>The API budget's empty cells. Carried without colour by <c>░</c>, and the percentage.</summary>
    GaugeEmpty,

    /// <summary>
    ///     The frame's furniture: the status row's leading space and its <c> | </c> separators.
    ///     Meant to recede. Carried without colour by position.
    /// </summary>
    Chrome,

    /// <summary>
    ///     A key you press, wherever one is drawn as a key: the status row's, the help screen's key column, and a
    ///     confirmation's answer. Never prose that names a key, and never the padding beside one. Kept apart from
    ///     <see cref="Chrome" />, which is furniture, because a key is the most actionable token on the row (#221).
    ///     Carried without colour by position: first in its pair, before the colon; first column on the help screen.
    /// </summary>
    Key,

    /// <summary>
    ///     A panel's frame. Drawn by this client rather than by Terminal.Gui's <c>Border</c> (ADR-0021). Carried
    ///     without colour by the box characters.
    /// </summary>
    PanelBorder,

    /// <summary>
    ///     The frame of the panel you are in: the content panel, and the rail group holding the selected destination.
    ///     Which group is active is carried in colour by the current entry's band, and without colour by <c>▶</c> on
    ///     it.
    /// </summary>
    PanelBorderActive,

    /// <summary>
    ///     A panel's title on its top edge: a rail group's name, and the crumb you are standing on at the end of the
    ///     content panel's trail — told from the crumbs walked through, which are <see cref="Muted" />. Carried without
    ///     colour by position, on the edge, and the current crumb by being the last.
    /// </summary>
    PanelTitle,

    /// <summary>
    ///     Stale content while a fetch lands: the fetch mark at the end of the content panel's title. Carried without
    ///     colour by the mark saying <c>fetching.</c>, and a dot more each tick.
    /// </summary>
    Loading,

    /// <summary>A delete affordance and its confirmation. Carried without colour by the word.</summary>
    Destructive,

    /// <summary>A failure the shell has to say out loud. Carried without colour by the word.</summary>
    Error,
}
