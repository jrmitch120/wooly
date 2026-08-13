using Wooly.Core.Paging;

namespace Wooly.Cli.Commands;

/// <summary>
///     What listing one thing means, as the three ways any two of them differ and nothing else: the port asked, and
///     the two ways the answer is written down. Everything else a list command does is
///     <see cref="PagedListCommand{TSettings,TItem}" />'s, which is the point of saying only these three here.
/// </summary>
/// <typeparam name="T">What is being listed: a post, a notification, an account, a conversation.</typeparam>
/// <param name="Reads">What the instance is asked for, and how many.</param>
/// <param name="AsJson">How the answer is written for another program, under <c>--json</c>.</param>
/// <param name="AsReport">How it is written for a person.</param>
internal sealed record Listing<T>(
    Func<CancellationToken, Task<Fetch<T>>> Reads,
    Action<Fetch<T>> AsJson,
    Action<Fetch<T>> AsReport);
