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

    /// <summary>Timestamps, counts nobody acted on, hints. Carried without colour by position.</summary>
    Muted,

    /// <summary>A display name.</summary>
    BylineName,

    /// <summary>A <c>username@instance</c>. Carried without colour by the <c>@</c>.</summary>
    BylineHandle,

    /// <summary>The visibility mark. Carried without colour by <c>○ ◌ ● ✉</c>.</summary>
    Audience,

    /// <summary>A warning and its text. Carried without colour by <c>⚠</c>.</summary>
    ContentWarning,

    /// <summary>Image placeholders and attachment links. Carried without colour by <c>▒▒▒▒</c> and <c>⏵</c>.</summary>
    Media,

    /// <summary>A poll's options and their bars. Carried without colour by the bar itself.</summary>
    Poll,

    /// <summary>The boost mark. Carried without colour by <c>↺</c>.</summary>
    Boost,

    /// <summary>The boost mark where the boost is this profile's own.</summary>
    BoostMine,

    /// <summary>The favorite mark. Carried without colour by <c>★</c>.</summary>
    Favorite,

    /// <summary>The favorite mark where the favorite is this profile's own.</summary>
    FavoriteMine,

    /// <summary>The selected row. Carried without colour by <c>▌</c> in the gutter.</summary>
    Selection,

    /// <summary>A rail destination.</summary>
    Rail,

    /// <summary>The rail destination that is selected. Carried without colour by <c>▸</c>, with <c>▶</c> for the cursor.</summary>
    RailCurrent,

    /// <summary>An unread count on the rail. Carried without colour by the number's presence.</summary>
    RailUnread,

    /// <summary>Rate-limit budget left.</summary>
    Quota,

    /// <summary>Rate-limit budget nearly spent.</summary>
    QuotaLow,

    /// <summary>The breadcrumb and status rows. Carried without colour by position.</summary>
    Chrome,

    /// <summary>Stale content while a fetch lands. Carried without colour by the breadcrumb saying <c>fetching…</c>.</summary>
    Loading,

    /// <summary>A delete affordance and its confirmation. Carried without colour by the word.</summary>
    Destructive,

    /// <summary>A failure the shell has to say out loud. Carried without colour by the word.</summary>
    Error,
}
