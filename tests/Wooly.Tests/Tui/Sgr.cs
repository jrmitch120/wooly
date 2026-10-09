namespace Wooly.Tests.Tui;

/// <summary>
///     The mouse reports a terminal writes with any-motion tracking (mode 1003) in SGR form (mode 1006), which is how
///     Terminal.Gui asks for them: <c>ESC[&lt;b;x;yM</c> for a button going down or the pointer moving, <c>m</c> for one
///     let go, the cell counted from one. Fed through <see cref="DrawnShell.Reports" />, so that a test sees what
///     Terminal.Gui makes of them — the parsing, the clicks it makes of a press and a release, the view the pointer is
///     over — rather than events made ready for it.
/// </summary>
internal static class Sgr
{
    /// <summary>The left button going down on the cell.</summary>
    public static string Press(int column, int row) => Report(0, column, row, 'M');

    /// <summary>The pointer moving onto the cell with the left button held.</summary>
    public static string Drag(int column, int row) => Report(32, column, row, 'M');

    /// <summary>The left button let go on the cell.</summary>
    public static string Release(int column, int row) => Report(0, column, row, 'm');

    /// <summary>The pointer moving onto the cell with no button held.</summary>
    public static string Move(int column, int row) => Report(35, column, row, 'M');

    /// <summary>
    ///     The pointer moving onto the cell with the right button held — or with it only believed held: a terminal that
    ///     missed a right button's let-go reports every move this way until its window closes, as Ghostty was found to.
    /// </summary>
    public static string RightDrag(int column, int row) => Report(34, column, row, 'M');

    /// <summary>One notch of the wheel over the cell: down, towards the foot of the page, or up.</summary>
    public static string Wheel(int column, int row, bool down = true) => Report(down ? 65 : 64, column, row, 'M');

    /// <summary>One sideways notch over the cell, as a trackpad sends when a finger drifts: right, or left.</summary>
    public static string Sideways(int column, int row, bool right = true) => Report(right ? 67 : 66, column, row, 'M');

    private static string Report(int button, int column, int row, char end) =>
        $"\u001b[<{button};{column + 1};{row + 1}{end}";
}
