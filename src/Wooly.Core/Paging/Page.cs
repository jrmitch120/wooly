namespace Wooly.Core.Paging;

/// <summary>
///     One page of a list as an instance answered it: what was on it, and where the instance said the next page
///     starts — the <c>max_id</c> of its <c>next</c> link — or <see langword="null" /> where it named none.
/// </summary>
/// <param name="Items">What was on the page, in the order the instance listed it.</param>
/// <param name="NextPageMaxId">Where the next page starts, by the instance's own link to it.</param>
internal sealed record Page<T>(IReadOnlyList<T> Items, string? NextPageMaxId);
