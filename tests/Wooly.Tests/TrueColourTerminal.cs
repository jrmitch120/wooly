using System.Runtime.CompilerServices;

namespace Wooly.Tests;

/// <summary>
///     The terminal every test runs on, said once before any test does. Terminal.Gui reads <c>TERM</c> and
///     <c>COLORTERM</c> to decide whether a driver draws in true colour, and offers no way to say so otherwise; left to
///     whatever shell ran the suite, a <c>TERM=linux</c> console draws in sixteen colours and every test asserting on a
///     hex colour's escape fails there and nowhere else.
/// </summary>
internal static class TrueColourTerminal
{
    [ModuleInitializer]
    public static void Pin()
    {
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        Environment.SetEnvironmentVariable("COLORTERM", "truecolor");
    }
}
