using Wooly.Core.Paging;

namespace Wooly.Tests.Fakes;

/// <summary>
///     How a fake list that reads on hands itself out (#460): as many as were asked for from just past where the last
///     read said to read on from, and where to read on from after them — the last one handed out — until the list runs
///     out, where it is whole.
/// </summary>
internal static class OnwardRun
{
    /// <summary>
    ///     <paramref name="limit" /> of <paramref name="all" /> from just past the one <paramref name="from" /> names,
    ///     and where to read on from after them unless that was the last of them.
    /// </summary>
    /// <param name="all">Everything the list holds, in the instance's order.</param>
    /// <param name="idOf">What names one of them, which is what reading on from it hands back.</param>
    /// <param name="from">Where to read on from, or <see langword="null" /> for the top.</param>
    /// <param name="limit">How many are wanted.</param>
    public static Fetch<T> Of<T>(IReadOnlyList<T> all, Func<T, string> idOf, Onward? from, int limit)
    {
        var start = from?.MaxId is { } after ? all.ToList().FindIndex(item => idOf(item) == after) + 1 : 0;
        var run = all.Skip(start).Take(limit).ToList();

        return start + run.Count < all.Count
            ? Fetch<T>.Complete(run, new Onward(idOf(run[^1])))
            : Fetch<T>.Complete(run);
    }
}
