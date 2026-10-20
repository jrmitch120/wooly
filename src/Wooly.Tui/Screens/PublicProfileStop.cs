namespace Wooly.Tui.Screens;

/// <summary>What one place on the edit public profile form is: a field typed into, or a toggle flipped (#406).</summary>
public enum PublicProfilePart
{
    /// <summary>The name the account is shown as.</summary>
    DisplayName,

    /// <summary>The bio, as written: several lines, in the editor a post is written in.</summary>
    Bio,

    /// <summary>A custom field's label, the left half of its row.</summary>
    Label,

    /// <summary>A custom field's value, the right half of its row.</summary>
    Value,

    /// <summary>Whether followers are approved by hand.</summary>
    Locked,

    /// <summary>Whether the account says it posts automatically.</summary>
    Bot,

    /// <summary>Whether the account is suggested and listed in the directory.</summary>
    Discoverable,
}

/// <summary>
///     One place the arrows walk to on the edit public profile form, top to bottom (#406): which part it is, and for a
///     custom field's label or value, which of the rows.
/// </summary>
/// <param name="Part">What it is.</param>
/// <param name="Row">Which custom-field row, counted from nought; nought for every other part.</param>
public readonly record struct PublicProfileStop(PublicProfilePart Part, int Row = 0)
{
    /// <summary>Whether it is one of the three toggles, which are flipped rather than typed into.</summary>
    public bool IsAToggle => Part is PublicProfilePart.Locked or PublicProfilePart.Bot or PublicProfilePart.Discoverable;
}
