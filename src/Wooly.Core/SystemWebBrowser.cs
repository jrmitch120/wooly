using System.Diagnostics;

namespace Wooly.Core;

/// <summary>
///     Hands the address to the OS and lets it decide what a browser is — <c>open</c> on macOS, <c>xdg-open</c> on
///     Linux, the shell's default association on Windows — which is what <see cref="ProcessStartInfo.UseShellExecute" />
///     means on each.
/// </summary>
public sealed class SystemWebBrowser : IWebBrowser
{
    /// <inheritdoc />
    public bool TryOpen(Uri url)
    {
        try
        {
            using var browser = Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

            return browser is not null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deliberately every failure: what a machine with no browser throws differs by platform (a missing
            // xdg-open, no association, no desktop session at all), and the caller does the same thing for all of
            // them — shows the user the address so they can open it somewhere else.
            return false;
        }
    }
}
