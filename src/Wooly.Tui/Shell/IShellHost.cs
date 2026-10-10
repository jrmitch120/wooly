namespace Wooly.Tui.Shell;

/// <summary>
///     The things the shell needs from a terminal that it cannot do itself: wait, get back onto the thread that
///     draws, and ring the bell. Everything else the shell does — which destination is selected, what a run of rail steps fetches,
///     which role a thing takes — is decided without one, which is what lets it be tested without one (ADR-0005).
/// </summary>
public interface IShellHost
{
    /// <summary>
    ///     Runs <paramref name="work" /> on the thread that draws. A fetch lands on whatever thread the HTTP stack
    ///     finished on, and Terminal.Gui is not a thing to touch from there.
    /// </summary>
    void OnUiThread(Action work);

    /// <summary>
    ///     Runs <paramref name="work" /> once, <paramref name="delay" /> from now, on the thread that draws.
    /// </summary>
    /// <returns>
    ///     A handle that abandons the wait when disposed — which is what every rail keypress does to the one before
    ///     it, and what makes a run of presses one settle rather than six.
    /// </returns>
    IDisposable After(TimeSpan delay, Action work);

    /// <summary>
    ///     Writes the terminal's own bell (<c>BEL</c>) through the shell's output, so the terminal's settings decide
    ///     whether it sounds, flashes or bounces — over SSH too, and with no branch for any one terminal (#399).
    /// </summary>
    void Ring();
}
