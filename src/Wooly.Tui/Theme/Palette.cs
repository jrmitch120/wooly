using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Theme;

/// <summary>
///     A theme as a table: the page everything is drawn on, and what each role is drawn in. The only implementation of
///     <see cref="ITheme" /> that holds colours — the built-ins are one of these, and so is a theme somebody wrote,
///     which is the same table with their own colours laid over it.
/// </summary>
/// <remarks>
///     A role is a foreground, and a background only where the role has one of its own: the row somebody is standing
///     on is told apart by the band it is drawn in rather than by the text on it, and everything else belongs on the
///     page. Keeping those apart is what lets a theme move the page — <see cref="Overlaid" /> — without having to
///     restate every role that was only ever sitting on it.
/// </remarks>
internal sealed class Palette : ITheme
{
    private readonly Color _background;
    private readonly IReadOnlyDictionary<Role, (Color Foreground, Color? Own)> _roles;

    private Palette(Color background, IReadOnlyDictionary<Role, (Color, Color?)> roles)
    {
        _background = background;
        _roles = roles;
    }

    /// <summary>
    ///     A palette written as the colours are written in a theme file, so that the built-ins are read the same way a
    ///     user's theme is — and a colour this client would refuse from somebody else is one it cannot ship either.
    /// </summary>
    /// <param name="background">The page.</param>
    /// <param name="foregrounds">What each role is drawn in.</param>
    /// <param name="backgrounds">The roles drawn on something other than the page.</param>
    public static Palette Of(
        string background,
        IReadOnlyDictionary<Role, string> foregrounds,
        IReadOnlyDictionary<Role, string>? backgrounds = null)
    {
        var roles = foregrounds.ToDictionary(
            role => role.Key,
            role => (Colour(role.Value), backgrounds?.TryGetValue(role.Key, out var own) == true
                ? Colour(own)
                : (Color?)null));

        return new Palette(Colour(background), roles);
    }

    /// <inheritdoc />
    public Attribute For(Role role) => _roles.TryGetValue(role, out var colours)
        ? new Attribute(colours.Foreground, colours.Own ?? _background)

        // A role the built-in forgot is a defect in this file rather than something to paint grey and hope nobody
        // notices — which is precisely the failure a role table exists to make impossible.
        : throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            $"This theme answers no role called '{RoleName.Of(role)}'.");

    /// <summary>
    ///     This palette with somebody's own colours laid over it: the roles they named take what they named, and
    ///     everything they left out stays as it was.
    /// </summary>
    /// <remarks>
    ///     A role given a foreground and no background keeps whichever background it already had — the page, or its
    ///     own band where it had one. A page moved under a palette moves every role that was sitting on it and no
    ///     others, which is what makes <c>background = "…"</c> on its own a theme worth writing.
    /// </remarks>
    /// <param name="background">The page they named, or <see langword="null" /> to keep this one.</param>
    /// <param name="named">What they said about each role they named.</param>
    public Palette Overlaid(Color? background, IReadOnlyDictionary<Role, (Color? Foreground, Color? Background)> named)
    {
        var roles = _roles.ToDictionary(
            role => role.Key,
            role => named.TryGetValue(role.Key, out var over)
                ? (over.Foreground ?? role.Value.Foreground, over.Background ?? role.Value.Own)
                : role.Value);

        return new Palette(background ?? _background, roles);
    }

    /// <summary>
    ///     A colour this client ships, read by the same parser a user's is. Anything it cannot read is a typo in a
    ///     built-in table, which is a defect rather than a config file to be told about.
    /// </summary>
    private static Color Colour(string written) =>
        ColourName.Parse(written) ?? throw new InvalidOperationException(ColourName.Rejection(written));
}
