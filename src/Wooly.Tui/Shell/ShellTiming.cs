namespace Wooly.Tui.Shell;

/// <summary>
///     The three lengths of time the shell's behaviour depends on, in one place so that a test can shorten them and
///     a reader can find out what they are without reading for them.
/// </summary>
/// <param name="Settle">
///     How long the tabbing has to stop for before the rail's selection follows its cursor. 250ms: long enough that a
///     deliberate double-tap lands as one move, short enough that a single tab does not read as a pause (ADR-0014).
/// </param>
/// <param name="CacheFor">
///     How long what a destination held stays worth drawing without asking the instance again. A minute: long enough
///     that walking out along the rail and back is free, short enough that a timeline left and returned to a minute
///     later is fetched rather than remembered.
/// </param>
/// <param name="CountdownStep">
///     How often the rate-limit countdown is redrawn while it waits. A second, because that is the unit it counts in.
/// </param>
public sealed record ShellTiming(TimeSpan Settle, TimeSpan CacheFor, TimeSpan CountdownStep)
{
    /// <summary>What the shell runs at in front of a person.</summary>
    public static ShellTiming Default { get; } = new(
        Settle: TimeSpan.FromMilliseconds(250),
        CacheFor: TimeSpan.FromMinutes(1),
        CountdownStep: TimeSpan.FromSeconds(1));
}
