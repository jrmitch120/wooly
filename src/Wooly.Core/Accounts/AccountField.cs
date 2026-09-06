namespace Wooly.Core.Accounts;

/// <summary>One custom field (CONTEXT.md), of the four Mastodon lets an account set.</summary>
public sealed record AccountField
{
    /// <summary>What the account calls the row, which is anything it chose to type and unique to nothing.</summary>
    public required string Label { get; init; }

    /// <summary>What the row says, flattened out of the HTML the instance serves it as, the way a bio is.</summary>
    public required string Said { get; init; }

    /// <summary>
    ///     When the instance proved the address in <see cref="Said" /> belongs to the account, or
    ///     <see langword="null" /> for a row nobody proved. A moment rather than a yes-or-no because the wire says
    ///     <em>when</em>, and keeping a bool instead would throw away a fact for nothing.
    /// </summary>
    /// <remarks>Only ever a link is verified, so a row saying anything else is always unproved.</remarks>
    public DateTimeOffset? Verified { get; init; }
}
