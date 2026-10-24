namespace Wooly.Tui.Shell;

/// <summary>
///     Which of its two states the rail is drawn in (<c>GLOSSARY.md</c>): only how it is drawn and how wide, and nothing
///     else about it (#441). Saved in the config file as the boolean <c>collapsed_rail</c> (#442).
/// </summary>
public enum RailState
{
    /// <summary>The <b>expanded rail</b>, each destination its label: what a fresh install opens on.</summary>
    Expanded,

    /// <summary>The <b>collapsed rail</b>, each destination one mark and its count.</summary>
    Collapsed,
}
