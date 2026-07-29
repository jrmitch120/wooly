# Terminal.Gui v2 for the TUI, Spectre.Console + Spectre.Console.Cli for the CLI

The client has two front ends sharing one core: an interactive TUI and a scriptable one-shot CLI. We use two different libraries for them rather than one library for both, because they solve genuinely different problems. `Terminal.Gui` v2 powers the interactive TUI: it has an instance-based `IApplication` (no static/global state, built for testability), a real multi-view/windowing/focus model suited to a scrolling timeline with several screens, and a built-in `ImageView` with working Sixel and Kitty graphics-protocol support. `Spectre.Console` renders the one-shot CLI's human-readable output (tables, color); `Spectre.Console.Cli` handles command parsing so parsing, help text, and rendering share one styling engine and one dependency, rather than pulling in a third framework just for argument parsing.

## Considered Options

- **Spectre.Console alone for both surfaces** — rejected: it excels at rich linear output and a single live-updating region, but has no multi-pane, keyboard-navigable windowing model, and no image-protocol support at all.
- **Terminal.Gui + Spectre.Console.Cli** — chosen, as above.
- **Terminal.Gui + System.CommandLine** — a credible alternative for parsing if the team later wants parsing decoupled from rendering (System.CommandLine reached stable GA alongside .NET 10); not the default because it would add a third framework's conventions purely for argument parsing while Spectre.Console is already in the dependency graph.

## Consequences

An official `Terminal.Gui.Interop.Spectre` bridge lets Spectre.Console's renderable widgets (tables, markup) be reused inside the Terminal.Gui TUI, so the two libraries compose rather than duplicate each other's rendering work.
