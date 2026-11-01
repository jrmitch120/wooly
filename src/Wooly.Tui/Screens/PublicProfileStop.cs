using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     What one place on the edit public profile form is: a field typed into, a picture chosen (#407), or a toggle
///     flipped (#406).
/// </summary>
public enum PublicProfilePart
{
    /// <summary>The name the account is shown as.</summary>
    DisplayName,

    /// <summary>The bio, as written: several lines, in the editor a post is written in.</summary>
    Bio,

    /// <summary>The avatar: a picture, chosen from a file or pasted rather than typed (#407).</summary>
    Avatar,

    /// <summary>The header: a picture, chosen as the avatar is.</summary>
    Header,

    /// <summary>A custom field's label, the left half of its row.</summary>
    Label,

    /// <summary>A custom field's value, the right half of its row.</summary>
    Value,

    /// <summary>Whether followers are approved by hand.</summary>
    Locked,

    /// <summary>Whether the account says it posts automatically.</summary>
    Bot,

    /// <summary>Whether the account is suggested and listed in the directory.</summary>
    Discoverable,
}

/// <summary>
///     One place the arrows walk to on the edit public profile form, top to bottom (#406): which part it is, and for a
///     custom field's label or value, which of the rows.
/// </summary>
/// <param name="Part">What it is.</param>
/// <param name="Row">Which custom-field row, counted from nought; nought for every other part.</param>
public readonly record struct PublicProfileStop(PublicProfilePart Part, int Row = 0)
{
    /// <summary>Whether it is one of the three toggles, which are flipped rather than typed into.</summary>
    public bool IsAToggle => PublicProfileToggle.Of(Part) is not null;

    /// <summary>Whether it is the avatar or the header, which are chosen from a file or pasted rather than typed (#407).</summary>
    public bool IsAPicture => PublicProfilePicture.Of(Part) is not null;
}

/// <summary>
///     One of the edit public profile form's two pictures (#407): which place on the form it is, the word its row is
///     labelled with, and how its box is drawn on a terminal that draws.
/// </summary>
/// <param name="Part">Which place on the form.</param>
/// <param name="Label">What its row is labelled, and in lower case what the file browser opened for it is crumbed with.</param>
public sealed record PublicProfilePicture(PublicProfilePart Part, string Label)
{
    /// <summary>The avatar.</summary>
    public static PublicProfilePicture Avatar { get; } = new(PublicProfilePart.Avatar, "Avatar");

    /// <summary>The header.</summary>
    public static PublicProfilePicture Header { get; } = new(PublicProfilePart.Header, "Header");

    /// <summary>
    ///     How a picture takes this one's box, whether the instance's or a file chosen for it: the avatar fitted whole,
    ///     as every avatar is (#532), and the header cropped from the middle, as the instance crops one (#438).
    /// </summary>
    internal BoxFill Fill => Part == PublicProfilePart.Avatar ? BoxFill.Fitted : BoxFill.Cropped;

    /// <summary>
    ///     How wide its box is on <paramref name="raster" />'s cell: the avatar as near square for the form's rows as
    ///     whole columns allow, as every avatar is (<see cref="Rendering.Avatar.Columns" />), and the header
    ///     <see cref="PublicProfilePictures.HeaderColumns" /> whatever the font.
    /// </summary>
    internal int Columns(Raster? raster) =>
        Part == PublicProfilePart.Avatar
            ? Rendering.Avatar.Columns(PublicProfilePictures.Rows, raster)
            : PublicProfilePictures.HeaderColumns;

    /// <summary>Both, in the order the form draws and walks them.</summary>
    public static IReadOnlyList<PublicProfilePicture> Both { get; } = [Avatar, Header];

    /// <summary>The picture <paramref name="part" /> is, or <see langword="null" /> where it is none.</summary>
    public static PublicProfilePicture? Of(PublicProfilePart part) => Both.FirstOrDefault(picture => picture.Part == part);
}

/// <summary>
///     One of the edit public profile form's three toggles (#406): which place on the form it is, its name, and what
///     it says it does beside its box.
/// </summary>
/// <param name="Part">Which place on the form.</param>
/// <param name="Name">What it is called beside its box.</param>
/// <param name="Says">What it does, said dimly after its name.</param>
public sealed record PublicProfileToggle(PublicProfilePart Part, string Name, string Says)
{
    /// <summary>All three, in the order the form draws and walks them.</summary>
    public static IReadOnlyList<PublicProfileToggle> All { get; } =
    [
        new(PublicProfilePart.Locked, "Locked", "approve followers by hand"),
        new(PublicProfilePart.Bot, "Bot", "posts automatically"),
        new(PublicProfilePart.Discoverable, "Discoverable", "in the directory and suggestions"),
    ];

    /// <summary>The toggle <paramref name="part" /> is, or <see langword="null" /> where it is none.</summary>
    public static PublicProfileToggle? Of(PublicProfilePart part) => All.FirstOrDefault(toggle => toggle.Part == part);
}
