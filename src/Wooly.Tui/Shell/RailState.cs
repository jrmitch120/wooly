namespace Wooly.Tui.Shell;

/// <summary>
///     Which of its two states the rail is in (<c>GLOSSARY.md</c>): whether it is drawn, and whether it can be walked,
///     and nothing else about it (#441, #445). Saved in the config file as the boolean <c>hidden_rail</c> (#442).
/// </summary>
public enum RailState
{
    /// <summary>The <b>shown rail</b>, each destination its label: what a fresh install opens on.</summary>
    Shown,

    /// <summary>The <b>hidden rail</b>, out of sight, the content taking the whole width of the shell.</summary>
    Hidden,
}
