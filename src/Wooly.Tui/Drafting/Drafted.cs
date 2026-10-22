namespace Wooly.Tui.Drafting;

/// <summary>What <see cref="Drafting.Do" /> made of a verb, which is all the shell's verb route needs back (ADR-0029).</summary>
public enum Drafted
{
    /// <summary>Not a draft verb: the shell carries on with it as with any other.</summary>
    NotMine,

    /// <summary>A draft verb, carried out — the press was used.</summary>
    Used,

    /// <summary>
    ///     A draft verb that found nothing to do — a paste from a clipboard holding nothing to attach — so the key falls
    ///     back through the window to whatever else wants it, the field's own paste above all.
    /// </summary>
    Declined,
}
