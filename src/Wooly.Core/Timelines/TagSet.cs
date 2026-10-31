namespace Wooly.Core.Timelines;

/// <summary>
///     A <b>Tag set</b> (GLOSSARY.md, ADR-0033): a name the reader gives to a few hashtags, read as one timeline of the
///     public posts carrying any of them. Built through <see cref="Of" /> alone, so that a set with no name, no tags, a
///     tag that is not one word or more tags than <see cref="MostTags" /> is not a thing to be represented, let alone
///     sent to an instance.
/// </summary>
/// <remarks>
///     The same set by value: two sets of the same name holding the same tags in the same order are one set, which is
///     what lets a rail destination carrying one be compared by what it is (#510).
/// </remarks>
public sealed record TagSet
{
    /// <summary>
    ///     The most tags a set holds: three asks' worth of <see cref="TagsPerAsk" />, so the dearest set's cost is
    ///     known before it is written (ADR-0033).
    /// </summary>
    public const int MostTags = 12;

    /// <summary>
    ///     The most tags Mastodon's tag timeline reads in one ask (<c>TagFeed::LIMIT_PER_MODE</c>), the path tag and
    ///     <c>any[]</c> together. The instance drops any past it without saying so, so a set of more is this client's
    ///     to cut into runs of this many, each its own ask.
    /// </summary>
    public const int TagsPerAsk = 4;

    private TagSet(string name, IReadOnlyList<string> tags)
    {
        Name = name;
        Tags = tags;
    }

    /// <summary>What the reader called the set, which is what the rail and the breadcrumb say.</summary>
    public string Name { get; }

    /// <summary>The set's tags, bare and each once, in the order the reader wrote them.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>The set the reader wrote as <paramref name="name" /> and <paramref name="tags" />.</summary>
    /// <param name="name">What the set is called.</param>
    /// <param name="tags">Its tags, each with or without its leading <c>#</c>, a repeat counting once.</param>
    /// <exception cref="ArgumentException">
    ///     The set is not one this client can read (<see cref="Rejection" />). A caller is expected to have refused
    ///     that against what the reader wrote; reaching here with one is a defect, not user error.
    /// </exception>
    public static TagSet Of(string name, IEnumerable<string> tags)
    {
        var written = tags.ToList();

        if (Rejection(name, written) is { } problem)
        {
            throw new ArgumentException(problem, nameof(tags));
        }

        return new TagSet(name, Bare(written));
    }

    /// <summary>
    ///     What is wrong with a set written as <paramref name="name" /> and <paramref name="tags" />, or <see
    ///     langword="null" /> where it is a set — said once here so that the config file refusing one and the domain
    ///     refusing one that got past it cannot say different things about it. Begun lower-case, as the config store's
    ///     own messages are, since the store says it after "Could not read {path}: ".
    /// </summary>
    public static string? Rejection(string? name, IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "a tag set needs a name.";
        }

        if (tags.Count == 0)
        {
            return $"the tag set '{name}' has no tags.";
        }

        if (tags.FirstOrDefault(tag => !Hashtag.IsWellFormed(tag)) is { } malformed)
        {
            return $"in the tag set '{name}': {Hashtag.Rejection(malformed)}";
        }

        var count = Bare(tags).Count;

        return count > MostTags
            ? $"the tag set '{name}' has {count} tags, and a set holds at most {MostTags}."
            : null;
    }

    /// <inheritdoc />
    public bool Equals(TagSet? other) => other is not null && other.Name == Name && other.Tags.SequenceEqual(Tags);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Tags.Aggregate(Name.GetHashCode(StringComparison.Ordinal), (hash, tag) => HashCode.Combine(hash, tag));

    /// <summary>
    ///     The tags as an instance wants them, each said once, in the order written. Once whatever their case, since an
    ///     instance reads <c>#DotNet</c> and <c>#dotnet</c> as the one tag; the first spelling written is the one kept.
    /// </summary>
    private static List<string> Bare(IEnumerable<string> tags) =>
        [.. tags.Select(Hashtag.Bare).Distinct(StringComparer.OrdinalIgnoreCase)];
}
