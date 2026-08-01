namespace Wooly.Tui.Media;

/// <summary>
///     How many pixels one terminal cell is, which is the only thing that turns a picture's shape into a number of rows
///     and columns. Terminals differ — the protocols report it, and 10×20 is what is assumed where one is asked and does
///     not say.
/// </summary>
/// <param name="Width">Pixels across one cell.</param>
/// <param name="Height">Pixels down one cell.</param>
public readonly record struct CellSize(int Width, int Height);
