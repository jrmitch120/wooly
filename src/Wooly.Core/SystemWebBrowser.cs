using System.Diagnostics;

namespace Wooly.Core;

/// <summary>
///     Starts whatever <see cref="BrowserLaunch" /> says this platform opens an address with — <c>open</c> on macOS,
///     <c>xdg-open</c> on Linux, the shell's own association on Windows. Everything decided is decided there; all that
///     is left here is the process, which is the part no test can have.
/// </summary>
public sealed class SystemWebBrowser : IWebBrowser
{
    /// <inheritdoc />
    public bool TryOpen(Uri url)
    {
        if (BrowserLaunch.For(url) is not { } launch)
        {
            // A scheme this client does not open. Said as "no browser was launched" because that is what happened,
            // and because nothing above here can be told two things by one bool — the shell that offers a reader an
            // address refuses it before asking, where it can say which of the two it was (#85).
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(launch.FileName) { UseShellExecute = launch.UseShellExecute };

            if (launch.Argument is { } argument)
            {
                // One argument rather than a command line, so that the OS is handed the address whole — an address
                // with a space or an & in it is one argument here and would be two written out.
                start.ArgumentList.Add(argument);
            }

            using var browser = Process.Start(start);

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
