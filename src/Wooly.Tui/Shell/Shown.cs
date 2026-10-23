namespace Wooly.Tui.Shell;

/// <summary>
///     What the <see cref="StatusRow" /> is showing, as one value: a <see cref="Confirmation" /> where one is open,
///     else a <see cref="Shown.Notice" />, else nothing of its own — which is the keys of the screen in front
///     (<c>GLOSSARY.md</c>). What draws the row takes this and nothing looser, so the order the three come in is
///     settled once, where the row is kept, and drawing it is only drawing.
/// </summary>
public abstract record Shown
{
    private Shown()
    {
    }

    /// <summary>Nothing said and nothing asked: the row is the keys of the screen in front.</summary>
    public sealed record Keys : Shown;

    /// <summary>A confirmation open on the row, which is the only thing answered while it is.</summary>
    public sealed record Asking(Confirmation Confirmation) : Shown;

    /// <summary>
    ///     Something said that is not a screen: a remark, or a failure where <paramref name="IsError" />, which settles
    ///     the role it is drawn in.
    /// </summary>
    public sealed record Notice(string Text, bool IsError) : Shown;
}
