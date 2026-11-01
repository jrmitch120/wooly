namespace Wooly.Tui.Rendering;

/// <summary>
///     The boxes the edit public profile form draws the avatar and the header in, on a terminal that draws (#407): each
///     two rows tall, the avatar as near square as the font allows (<see cref="Avatar.Columns" />, #532) and the header
///     three times as wide as the avatar is on a cell twice as tall as it is wide — the shape Mastodon crops a header
///     to. Said here, beside <see cref="Avatar" />'s, since a picture is decoded no larger than the box it is drawn in.
/// </summary>
public static class PublicProfilePictures
{
    /// <summary>
    ///     How many rows the avatar and the header each take: enough to see what it is, and few enough that the bio keeps
    ///     its rows on a short terminal.
    /// </summary>
    public const int Rows = 2;

    /// <summary>
    ///     How wide the header's box is: three times the avatar's on a cell twice as tall as it is wide, and wider than
    ///     the avatar's on any cell. The words beside both pictures start after it, so that they line up and neither a
    ///     picture arriving nor the font moves them.
    /// </summary>
    public const int HeaderColumns = 12;
}
