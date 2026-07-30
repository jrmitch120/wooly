using Wooly.Core.Errors;

namespace Wooly.Core.Paging;

/// <summary>
///     What <see cref="PagedReading.Collect{TWire,TItem}" /> came back with. Deliberately anonymous about what it holds:
///     each feature turns this into a fetch that names its own contents — posts on a timeline, notifications in an inbox
///     — because <c>Items</c> is the right word here and the wrong word everywhere a caller reads one.
/// </summary>
/// <param name="StoppedBy">The rate limit that cut the collection short, or <see langword="null" /> if nothing did.</param>
internal sealed record Paged<T>(IReadOnlyList<T> Items, RateLimitedException? StoppedBy);
