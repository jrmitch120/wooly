using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Core;

namespace Wooly.Tui;

/// <summary>
///     The TUI's top-level window. A placeholder until the timeline screen exists — it only proves the shell launches
///     and can read from the shared core.
/// </summary>
internal sealed class MainWindow : Window
{
    public MainWindow(IClientInfo clientInfo)
    {
        Title = $"{clientInfo.Name} {clientInfo.Version}";

        Add(new Label
        {
            Text = $"Timelines land here. Press {Application.GetDefaultKey(Command.Quit)} to quit.",
            X = Pos.Center(),
            Y = Pos.Center(),
        });
    }
}
