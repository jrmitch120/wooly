namespace Wooly.Core.Posts;

/// <summary>
///     How long an instance lets a post be, and what it counts an address in one as (#319) — and what it lets a post
///     carry (#375). Each instance sets its own; <see cref="Default" /> is what Mastodon ships with, and what is assumed
///     until an instance says otherwise.
/// </summary>
/// <param name="Characters">The most a post may be, warning and all, as <see cref="PostLength" /> counts it.</param>
/// <param name="PerAddress">What any address counts as, however long it is.</param>
public sealed record PostLimits(int Characters, int PerAddress)
{
    /// <summary>Mastodon's own: 500 characters, an address counting as 23, and Mastodon's attachments.</summary>
    public static readonly PostLimits Default = new(500, 23);

    /// <summary>The most attachments a post may carry: four, on an instance that does not say.</summary>
    public int Attachments { get; init; } = 4;

    /// <summary>
    ///     The types of file the instance accepts as attachments, by MIME type: Mastodon's own
    ///     (<see cref="AttachmentTypes.Mastodon" />) on an instance that does not say.
    /// </summary>
    public IReadOnlyList<string> MediaTypes { get; init; } = AttachmentTypes.Mastodon;

    /// <summary>The longest an attachment's description may be: 1500 characters, on an instance that does not say.</summary>
    public int Descriptions { get; init; } = 1500;

    /// <summary>
    ///     Whether the instance accepts the file at <paramref name="path" /> by its type — as its name says it, which is
    ///     all a terminal knows of a file before it is sent; the instance has the last word once it has it.
    /// </summary>
    public bool Accepts(string path) =>
        AttachmentTypes.Of(path) is { } type && MediaTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>The types compared one by one, so that two answers saying the same thing are the same limits.</remarks>
    public bool Equals(PostLimits? other) =>
        other is not null
        && Characters == other.Characters
        && PerAddress == other.PerAddress
        && Attachments == other.Attachments
        && Descriptions == other.Descriptions
        && MediaTypes.SequenceEqual(other.MediaTypes);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Characters, PerAddress, Attachments, Descriptions);
}
