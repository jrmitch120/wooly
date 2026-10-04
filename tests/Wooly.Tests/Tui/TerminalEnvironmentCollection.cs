namespace Wooly.Tests.Tui;

/// <summary>
///     Groups the tests that write <c>NO_COLOR</c> or <c>TERM</c>, and runs them with nothing else running. Terminal.Gui
///     reads both as a headless terminal starts, so a test drawing the shell while one of these had colour switched off
///     drew with none and failed on a colour it was looking for — intermittently, and more often the more drawing tests
///     there were to overlap.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TerminalEnvironmentCollection
{
    public const string Name = nameof(TerminalEnvironmentCollection);
}
