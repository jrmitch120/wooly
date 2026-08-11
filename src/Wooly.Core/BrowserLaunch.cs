using System.Runtime.InteropServices;

namespace Wooly.Core;

/// <summary>
///     What opening an address on this machine comes to: which program is asked, and what it is handed. The decision
///     on its own, with no process in it — which is what makes the one thing this client does that leaves the machine
///     assertable at all, the way role selection is the assertable part of drawing (ADR-0014).
/// </summary>
/// <remarks>
///     Two questions, and the first one is the one worth being careful about. <see cref="Address" /> settles what is
///     opened at all: a scheme is the whole of what makes handing text to a machine's shell dangerous, so
///     <c>http</c> and <c>https</c> are named and everything else is refused. <see cref="For(Uri, OSPlatform)" />
///     settles what each platform is asked, which differs by platform and cannot be tried on the platform it is not
///     for.
/// </remarks>
/// <param name="FileName">The program to start — the address itself on Windows, where the shell is what opens it.</param>
/// <param name="Argument">
///     The address handed to that program, or <see langword="null" /> where the program <em>is</em> the address.
///     One argument rather than a command line, so that an address with an <c>&amp;</c> in it is one argument and not
///     two.
/// </param>
/// <param name="UseShellExecute">
///     Whether the OS is being asked to work out what opens this rather than being told, which is what Windows is
///     asked and what the other two are not.
/// </param>
public readonly record struct BrowserLaunch(string FileName, string? Argument, bool UseShellExecute)
{
    /// <summary>
    ///     The web page <paramref name="written" /> names, or <see langword="null" /> where it names none — which is
    ///     what a refusal is, and is settled here rather than by whoever would have launched it.
    /// </summary>
    /// <remarks>
    ///     Text somebody else wrote, so it may be a whole address or the elided form an instance serves —
    ///     <c>www.example.com/notes</c>, <c>example.com/notes</c> — which names a page without saying how to reach it.
    ///     Those are read as <c>https</c>, which is what a browser handed one would have tried anyway. Anything that
    ///     does say how to reach it, and says anything but the two schemes above, is refused rather than repaired: a
    ///     <c>file:</c> or a <c>javascript:</c> that reached a shell is exactly what this is here to stop.
    /// </remarks>
    public static Uri? Address(string? written)
    {
        if (string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var text = written.Trim();

        if (NamesAScheme(text))
        {
            return Uri.TryCreate(text, UriKind.Absolute, out var named) && Opens(named) ? named : null;
        }

        // Nothing has been said that could be refused — only a page named without a way to reach it.
        return Uri.TryCreate($"{Uri.UriSchemeHttps}://{text}", UriKind.Absolute, out var secured) && Opens(secured)
            ? secured
            : null;
    }

    /// <summary>What this machine is asked to open <paramref name="address" /> with.</summary>
    public static BrowserLaunch? For(Uri address) => For(address, Running);

    /// <summary>
    ///     The same, for a platform named rather than the one underfoot — which is the whole of how the other two are
    ///     ever asked about.
    /// </summary>
    /// <remarks>
    ///     Windows is handed the address itself and lets the shell's association decide what a browser is; macOS and
    ///     Linux each name the program that already knows. Anything else is asked the way Linux is: <c>xdg-open</c> is
    ///     the convention wherever there is a desktop session and this is not Windows or macOS, and a machine without
    ///     one has no browser to open either way.
    /// </remarks>
    /// <returns>
    ///     <see langword="null" /> where <paramref name="address" /> is not one this client opens — the same refusal
    ///     <see cref="Address" /> makes, said again at the edge, so that reaching here with one starts nothing.
    /// </returns>
    public static BrowserLaunch? For(Uri address, OSPlatform on)
    {
        if (!Opens(address))
        {
            return null;
        }

        // Through WebAddress because this is the one place the URL stops being a Uri and becomes a string the OS
        // parses again — the moment an escape lost here turns into one added twice.
        var text = WebAddress.Of(address);

        if (on == OSPlatform.Windows)
        {
            return new BrowserLaunch(text, Argument: null, UseShellExecute: true);
        }

        return new BrowserLaunch(on == OSPlatform.OSX ? "open" : "xdg-open", text, UseShellExecute: false);
    }

    /// <summary>
    ///     Whether <paramref name="text" /> says how to reach the page as well as which one, which is what settles
    ///     whether there is a scheme here to refuse.
    /// </summary>
    /// <remarks>
    ///     Asked here rather than left to <see cref="Uri" />, which reads a colon before the path as a scheme
    ///     whatever follows it: <c>www.example.com:8080/notes</c> is a scheme called <c>www.example.com</c> to it, and
    ///     so is refused as a scheme this client does not open — when it is an ordinary page on a port. A colon
    ///     before the path is a port where digits follow it and a scheme where anything else does, and a colon inside
    ///     the path is punctuation somebody wrote.
    /// </remarks>
    private static bool NamesAScheme(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var slash = text.IndexOf('/', StringComparison.Ordinal);

        if (colon <= 0 || (slash >= 0 && slash < colon))
        {
            return false;
        }

        var between = slash < 0 ? text[(colon + 1)..] : text[(colon + 1)..slash];

        // Nothing between the colon and the path is the "//" of a scheme that named an authority, which the path's
        // own first slash has already been found as.
        return between.Length == 0 || !between.All(char.IsAsciiDigit);
    }

    /// <summary>The platform underfoot, as one of the three this knows how to ask.</summary>
    private static OSPlatform Running => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? OSPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX : OSPlatform.Linux;

    /// <summary>Whether this is an address this client hands to a machine at all.</summary>
    private static bool Opens(Uri address) =>
        address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps;
}
