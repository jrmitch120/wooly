using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Wooly.Tui.Prototype;

// THROWAWAY PROTOTYPE — four answers to "what shape is the Wooly TUI?" (issue #28). Run it, press F9/F10, pick one.
//
//   dotnet run --project src/Wooly.Tui.Prototype -- --variant c
//   dotnet run --project src/Wooly.Tui.Prototype -- --shot b     (draw one frame as text and exit)

var variantFlag = Argument(args, "--variant") ?? Argument(args, "-v");
var shot = Argument(args, "--shot");
var hold = int.TryParse(Argument(args, "--hold"), out var held) ? held : 600;
var index = Variants.IndexOf(shot ?? variantFlag);

using var application = Application.Create();
application.Init();

if (shot is not null)
{
    // Terminal.Gui v2.4 ships an input injector for tests — which is how a screenshot gets taken with nobody at the
    // keyboard, and worth knowing about given the spec writes off headless TUI testing as impractical.
    var keys = Argument(args, "--keys");

    if (keys is not null)
    {
        var injector = application.GetInputInjector();

        application.AddTimeout(TimeSpan.FromMilliseconds(500), () =>
        {
            var options = new InputInjectionOptions { Mode = InputInjectionMode.Direct, AutoProcess = true };

            foreach (var name in keys.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                injector.InjectKey(new Key(name), options);
            }

            injector.ProcessQueue();

            return false;
        });
    }

    // One frame, then out — enough for a screenshot without a human at the keyboard.
    application.AddTimeout(TimeSpan.FromMilliseconds(hold), () =>
    {
        application.RequestStop();

        return false;
    });
}

while (index >= 0)
{
    using var window = Variants.All[index].Open();
    application.Run(window);
    index = shot is not null ? -1 : window.Next;
}

return;

static string? Argument(string[] arguments, string name)
{
    var at = Array.IndexOf(arguments, name);

    return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
}
