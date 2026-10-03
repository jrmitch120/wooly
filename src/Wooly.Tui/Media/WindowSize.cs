using System.Runtime.InteropServices;

namespace Wooly.Tui.Media;

/// <summary>
///     How big a cell really is, from the terminal window's size as the kernel holds it (<c>TIOCGWINSZ</c>): its pixels
///     over its cells. Terminal.Gui's guess of 10×20 against Ghostty's real 16×34 stretched every photograph (#292).
/// </summary>
/// <remarks>
///     Measured once for each size of screen rather than each time a post asks, which is several times a post a frame.
///     A change of font size changes how many cells the window holds, so it is measured again then too. Asked on the
///     UI thread only, which is where rows are laid out, so the measurement it keeps needs no lock.
/// </remarks>
/// <param name="screen">How many columns and rows the screen is now.</param>
/// <param name="measure">How the cell is measured: <see cref="Measure" />, or a test's answer.</param>
internal sealed class WindowSize(Func<(int Columns, int Rows)> screen, Func<CellSize?> measure)
{
    /// <summary><c>TIOCGWINSZ</c> on macOS, which is the BSDs' number.</summary>
    private const ulong MacRequest = 0x40087468;

    /// <summary><c>TIOCGWINSZ</c> on Linux.</summary>
    private const ulong LinuxRequest = 0x5413;

    /// <summary>Standard output, which is the terminal a full-screen app is drawn on.</summary>
    private const int Output = 1;

    private (int Columns, int Rows)? _measuredAt;
    private CellSize? _cell;

    /// <summary>The cell, measured for the screen as it is now, or <see langword="null" /> where it cannot be.</summary>
    public CellSize? Cell
    {
        get
        {
            var now = screen();

            if (_measuredAt != now)
            {
                _cell = measure();
                _measuredAt = now;
            }

            return _cell;
        }
    }

    /// <summary>
    ///     The cell as the kernel says it is now, or <see langword="null" /> on Windows, where there is no
    ///     <c>TIOCGWINSZ</c>, on a system this does not know the request number for, where the output is not a terminal,
    ///     and where the terminal fills in no pixels — which most do not.
    /// </summary>
    public static CellSize? Measure()
    {
        // On Windows a cell is what Terminal.Gui reported, or 10×20.
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            var size = new Winsize();
            var request = OperatingSystem.IsMacOS() ? MacRequest : LinuxRequest;

            return Ask(request, ref size) == 0
                ? Divided(size.Columns, size.Rows, size.Width, size.Height)
                : null;
        }
        catch (Exception failure) when (failure is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    ///     A window <paramref name="width" /> by <paramref name="height" /> pixels across <paramref name="columns" /> by
    ///     <paramref name="rows" /> cells, as one cell — or <see langword="null" /> where any of it is missing.
    /// </summary>
    public static CellSize? Divided(int columns, int rows, int width, int height) =>
        columns > 0 && rows > 0 && width >= columns && height >= rows
            ? new CellSize(width / columns, height / rows)
            : null;

    /// <summary>
    ///     <c>ioctl</c> is variadic. On Apple Silicon a variadic argument goes on the stack rather than in a register,
    ///     so the plain declaration hands the kernel a garbage address and the process dies of an access violation — as
    ///     it did in the prototype. Six dummies fill the rest of the registers, which puts the pointer on the stack where
    ///     a variadic callee reads it. Everywhere else a variadic argument is passed like any other.
    /// </summary>
    private static int Ask(ulong request, ref Winsize size) =>
        OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? IoctlOnTheStack(Output, request, 0, 0, 0, 0, 0, 0, ref size)
            : Ioctl(Output, request, ref size);

    [DllImport("libc", EntryPoint = "ioctl")]
    private static extern int IoctlOnTheStack(
        int descriptor,
        ulong request,
        long x2,
        long x3,
        long x4,
        long x5,
        long x6,
        long x7,
        ref Winsize size);

    [DllImport("libc", EntryPoint = "ioctl")]
    private static extern int Ioctl(int descriptor, ulong request, ref Winsize size);

    /// <summary>The kernel's <c>struct winsize</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Winsize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort Width;
        public ushort Height;
    }
}
