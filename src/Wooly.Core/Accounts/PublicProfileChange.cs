namespace Wooly.Core.Accounts;

/// <summary>
///     What a save asks to change about a public profile, with <see langword="null" /> meaning "leave it as it is" —
///     sent as changed (ADR-0028), so that nothing left alone here can overwrite something changed on the web meanwhile.
/// </summary>
public sealed record PublicProfileChange
{
    /// <summary>The name to be shown as, or <see langword="null" /> to leave it.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The bio as written, or <see langword="null" /> to leave it.</summary>
    public string? Bio { get; init; }

    /// <summary>
    ///     Every custom field the account is to have, or <see langword="null" /> to leave them. All of them rather than
    ///     the ones that changed, because Mastodon replaces them as a set: empty takes them all away.
    /// </summary>
    public IReadOnlyList<PublicProfileField>? Fields { get; init; }

    /// <summary>Whether to approve followers by hand, or <see langword="null" /> to leave it.</summary>
    public bool? IsLocked { get; init; }

    /// <summary>Whether to say the account posts automatically, or <see langword="null" /> to leave it.</summary>
    public bool? IsBot { get; init; }

    /// <summary>Whether to be suggested and listed in the directory, or <see langword="null" /> to leave it.</summary>
    public bool? IsDiscoverable { get; init; }

    /// <summary>
    ///     The file to show as the avatar, or <see langword="null" /> to leave it. Sent in the save itself, since
    ///     Mastodon has nowhere to send a picture ahead of one (ADR-0028).
    /// </summary>
    public string? Avatar { get; init; }

    /// <summary>The file to show as the header, or <see langword="null" /> to leave it. Sent as <see cref="Avatar" /> is.</summary>
    public string? Header { get; init; }

    /// <summary>Whether this asks for nothing at all.</summary>
    public bool IsEmpty => this == new PublicProfileChange();

    /// <summary>
    ///     This change with whatever <paramref name="read" /> already says left out: what asking for a value the
    ///     public profile already has would send is nothing. A picture is kept whatever was read, because a read says
    ///     where the picture is and a change says which file to send, and nothing tells the two apart as the same.
    /// </summary>
    public PublicProfileChange Against(PublicProfile read) => this with
    {
        DisplayName = DisplayName == read.DisplayName ? null : DisplayName,
        Bio = Bio == read.Bio ? null : Bio,
        Fields = Fields is null || Fields.SequenceEqual(read.Fields) ? null : Fields,
        IsLocked = IsLocked == read.IsLocked ? null : IsLocked,
        IsBot = IsBot == read.IsBot ? null : IsBot,
        IsDiscoverable = IsDiscoverable == read.IsDiscoverable ? null : IsDiscoverable,
    };
}
