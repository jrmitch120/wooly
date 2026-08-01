using Terminal.Gui.App;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     <see cref="IShellHost" /> over a running Terminal.Gui application: the two things the shell cannot do itself.
/// </summary>
internal sealed class TerminalHost(IApplication application) : IShellHost
{
    private static readonly IDisposable Nothing = new NotScheduled();

    /// <inheritdoc />
    public void OnUiThread(Action work) => application.Invoke(work);

    /// <inheritdoc />
    public IDisposable After(TimeSpan delay, Action work)
    {
        var token = application.AddTimeout(delay, () =>
        {
            work();

            // Once, not repeatedly: everything the shell schedules is a thing that happens at the end of a wait.
            return false;
        });

        // An application with no main loop yet hands back nothing to cancel. Nothing was scheduled, so there is
        // nothing to call off either, and a handle that does nothing is the honest answer.
        return token is null ? Nothing : new Timeout(application, token);
    }

    /// <summary>Nothing to call off, for a wait that was never scheduled.</summary>
    private sealed class NotScheduled : IDisposable
    {
        public void Dispose()
        {
        }
    }

    /// <summary>
    ///     A scheduled wait that has not happened yet, and can be called off. Every rail keypress calls off the one
    ///     the press before it left waiting, which is the whole of the settle rule.
    /// </summary>
    private sealed class Timeout(IApplication application, object token) : IDisposable
    {
        public void Dispose() => application.RemoveTimeout(token);
    }
}
