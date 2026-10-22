namespace Wooly.Core.Accounts;

/// <summary>
///     The public profile (GLOSSARY.md) of the own account, as the account wrote it rather than as anyone reads it: the
///     bio and custom fields are <c>source</c>'s, never the flattened <see cref="Account.Bio" />, so that whatever is
///     edited from here and saved back is what was written (ADR-0028).
/// </summary>
public sealed record PublicProfile
{
    /// <summary>The name the account chose to be shown as, and empty where it chose none.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The bio as written (<c>source.note</c>), and empty where the account wrote none.</summary>
    public required string Bio { get; init; }

    /// <summary>The custom fields as written (<c>source.fields</c>), in the order they were set.</summary>
    public required IReadOnlyList<PublicProfileField> Fields { get; init; }

    /// <summary>Where the avatar is, or <see langword="null" /> where the instance did not say.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Where the header is, or <see langword="null" /> where the instance did not say.</summary>
    public string? HeaderUrl { get; init; }

    /// <summary>Whether the account approves its followers by hand.</summary>
    public required bool IsLocked { get; init; }

    /// <summary>Whether the account says it posts automatically rather than by hand.</summary>
    public required bool IsBot { get; init; }

    /// <summary>Whether the account lets itself be suggested and listed in the directory.</summary>
    public required bool IsDiscoverable { get; init; }

    /// <summary>
    ///     The account the public profile belongs to, as the same read found it — which is what is shown where there
    ///     turns out to be nothing to save.
    /// </summary>
    public required Account Account { get; init; }
}
