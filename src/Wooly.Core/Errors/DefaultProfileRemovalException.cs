namespace Wooly.Core.Errors;

/// <summary>
///     The default profile was named for removal. Refused rather than done, because removing it would leave every
///     command with no <c>--profile</c> — and the next TUI launch — with nothing to act as, and choosing another in its
///     place is the user's to do, not this client's. The way out is named: make another the default first.
/// </summary>
public sealed class DefaultProfileRemovalException(string name)
    : WoolyException(
        $"Profile '{name}' is the default. Make another profile the default with profile switch <NAME> before removing it.")
{
    /// <summary>The profile that was named.</summary>
    public string Name { get; } = name;
}
