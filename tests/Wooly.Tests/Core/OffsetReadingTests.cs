using Wooly.Core.Errors;
using Wooly.Core.Paging;

namespace Wooly.Tests.Core;

/// <summary>
///     Reading on a list the instance pages by offset rather than by <c>max_id</c> — a narrowed search, a kind of trend
///     (#604, #606): one page asked, and where to read on from carried in the same opaque <see cref="Onward" /> every
///     other list hands back, so a screen reads on from it without knowing which kind of paging is behind it.
/// </summary>
public class OffsetReadingTests
{
    /// <summary>A read from the top asks for the first page, at the size asked for.</summary>
    [Fact]
    public async Task Read_FromTheTop_AsksTheFirstPage()
    {
        var asked = new List<(int Limit, int Offset)>();

        await OffsetReading.Read(40, from: null, Pages(100, asked), CancellationToken.None);

        Assert.Equal([(40, 0)], asked);
    }

    /// <summary>
    ///     A full page is not the end: the list reads on from just past it, and handing that back asks for the page
    ///     after.
    /// </summary>
    [Fact]
    public async Task Read_AFullPage_ReadsOnFromJustPastIt()
    {
        var asked = new List<(int Limit, int Offset)>();

        var first = await OffsetReading.Read(40, from: null, Pages(100, asked), CancellationToken.None);
        var second = await OffsetReading.Read(40, first.Onward, Pages(100, asked), CancellationToken.None);

        Assert.False(first.IsWhole);
        Assert.Equal(Enumerable.Range(0, 40), first.Items);
        Assert.Equal(Enumerable.Range(40, 40), second.Items);
        Assert.Equal([(40, 0), (40, 40)], asked);
    }

    /// <summary>A page that comes back short is the end of the list, which is then whole.</summary>
    [Fact]
    public async Task Read_AShortPage_IsTheEnd()
    {
        var asked = new List<(int Limit, int Offset)>();

        var first = await OffsetReading.Read(40, from: null, Pages(100, asked), CancellationToken.None);
        var second = await OffsetReading.Read(40, first.Onward, Pages(100, asked), CancellationToken.None);
        var last = await OffsetReading.Read(40, second.Onward, Pages(100, asked), CancellationToken.None);

        Assert.Equal(Enumerable.Range(80, 20), last.Items);
        Assert.True(last.IsWhole);
        Assert.True(last.IsComplete);
    }

    /// <summary>
    ///     A page holding more than was asked for is full, not short — an instance puts an exact-match hashtag ahead of
    ///     the forty it ranked (#599) — and the next page still starts where the limit, not the count, says.
    /// </summary>
    [Fact]
    public async Task Read_APageHoldingMoreThanAsked_IsFull_AndReadsOnByTheLimit()
    {
        var asked = new List<(int Limit, int Offset)>();

        var first = await OffsetReading.Read(
            40,
            from: null,
            (limit, offset, _) =>
            {
                asked.Add((limit, offset));

                return Task.FromResult<IReadOnlyList<int>>([.. Enumerable.Range(offset, limit + 1)]);
            },
            CancellationToken.None);

        await OffsetReading.Read(40, first.Onward, Pages(100, asked), CancellationToken.None);

        Assert.False(first.IsWhole);
        Assert.Equal((40, 40), asked[^1]);
    }

    /// <summary>
    ///     A page is one call, so a rate limit refusing it has nothing in hand to keep and is raised rather than
    ///     answered as half a page (ADR-0011) — the reader waits it out or fails as its front end decides.
    /// </summary>
    [Fact]
    public async Task Read_RefusedByARateLimit_RaisesIt() =>
        await Assert.ThrowsAsync<RateLimitedException>(() => OffsetReading.Read<int>(
            40,
            from: null,
            (_, _, _) => throw new RateLimitedException("mastodon.social", resetsAt: null),
            CancellationToken.None));

    /// <summary>Two reads of the same list hand back the same place to read on from, compared by value.</summary>
    [Fact]
    public async Task Read_HandsBackTheSamePlace_ForTheSamePage()
    {
        var first = await OffsetReading.Read(40, from: null, Pages(100, []), CancellationToken.None);
        var again = await OffsetReading.Read(40, from: null, Pages(100, []), CancellationToken.None);

        Assert.Equal(first.Onward, again.Onward);
        Assert.NotEqual(Onward.FromTheTop, first.Onward);
    }

    /// <summary>A list of <paramref name="held" /> numbers from nought, paged by offset, each ask written down.</summary>
    private static Func<int, int, CancellationToken, Task<IReadOnlyList<int>>> Pages(
        int held,
        List<(int Limit, int Offset)> asked) =>
        (limit, offset, _) =>
        {
            asked.Add((limit, offset));

            return Task.FromResult<IReadOnlyList<int>>([.. Enumerable.Range(0, held).Skip(offset).Take(limit)]);
        };
}
