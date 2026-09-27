namespace Wooly.Core.Profiles;

/// <summary>
///     What somebody signing in through the browser is told while it happens, in words. One wording, in one place, so
///     that <c>profile add</c> and the TUI's add screen can never describe the same sign-in two different ways
///     (ADR-0004, ADR-0020).
/// </summary>
/// <remarks>
///     The instance is handed in already written the way the front end wants it, rather than put in here: the CLI draws
///     it bold with markup, and the TUI draws it as it is.
/// </remarks>
public static class BrowserSignIn
{
    /// <summary>
    ///     What is said while the redirect is awaited. The address is drawn under what comes before either way, since
    ///     a browser that opened is no guarantee the right one did.
    /// </summary>
    public const string Waiting = "Waiting for the browser to come back...";

    /// <summary>Said over the address where a browser was opened at it.</summary>
    /// <param name="instance">The instance's domain, written the way the front end draws it.</param>
    public static string Opened(string instance) =>
        $"Opening {instance} in your browser to authorize {WoolyClient.Name}. If it does not open, go to:";

    /// <summary>
    ///     Said over the address where no browser could be opened, which leaves the address the only way on.
    /// </summary>
    /// <param name="instance">The instance's domain, written the way the front end draws it.</param>
    public static string NotOpened(string instance) =>
        $"No browser could be opened here. To authorize {WoolyClient.Name}, go to {instance} at:";
}
