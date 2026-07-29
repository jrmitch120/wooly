using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Wooly.Core;
using Wooly.Tui;

var services = new ServiceCollection();
services.AddWoolyCore();
services.AddTransient<MainWindow>();

await using var provider = services.BuildServiceProvider();

// Terminal.Gui v2's instance-based application: no static/global shell state, so the TUI owns its own lifetime
// (ADR-0002).
using var application = Application.Create();
application.Init();

using var window = provider.GetRequiredService<MainWindow>();
application.Run(window);
