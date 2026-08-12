using Wooly.Core.Paging;

namespace Wooly.Cli.Commands;

/// <summary>
///     What one paged list is, as the three things that differ between them and nothing else: the port it asks, and
///     the two ways its answer is written down.
/// </summary>
/// <typeparam name="T">What is being listed: a post, a notification, an account, a conversation.</typeparam>
/// <param name="Reads">What the instance is asked for, and how many.</param>
/// <param name="AsJson">How the answer is written for another program, under <c>--json</c>.</param>
/// <param name="AsReport">How it is written for a person.</param>
internal sealed record PagedList<T>(
    Func<CancellationToken, Task<Fetch<T>>> Reads,
    Action<Fetch<T>> AsJson,
    Action<Fetch<T>> AsReport);
