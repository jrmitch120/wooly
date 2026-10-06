namespace Wooly.Tui.Media;

/// <summary>
///     The blurs a <b>Stand-in</b> is drawn from, decoded once from a blurhash and remembered, apart from the picture
///     cache (#349, ADR-0025).
/// </summary>
/// <remarks>
///     Apart because a blur is not a picture the cache should weigh against the pictures it stands in for: it is
///     decoded from the post rather than fetched, it costs a few kilobytes rather than megabytes, and counting it in the
///     cache's budget would let a screen of blurs push out the very pictures they are waiting on. Remembered at all
///     because a post's rows are worked out on every frame, and decoding the same hash on each of them would be most of
///     what a frame cost.
///     <para>
///         Decoded at <see cref="Side" /> pixels square whatever the box: a blur has no detail to lose by being
///         stretched to its box, which both raster paths do anyway, and one size a hash means one entry a hash. At four
///         bytes a pixel that is 4 KB a blur, so <see cref="MostHeld" /> of them is a megabyte at most.
///     </para>
/// </remarks>
public static class Blurs
{
    /// <summary>How many pixels across and down a blur is decoded at.</summary>
    public const int Side = 32;

    /// <summary>How many blurs are remembered before the one decoded longest ago is let go of.</summary>
    public const int MostHeld = 256;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Picture?> Held = [];
    private static readonly Queue<string> Order = new();

    /// <summary>
    ///     The blur <paramref name="blurhash" /> decodes to, or <see langword="null" /> where it does not decode — which
    ///     is remembered too, so a malformed hash is not decoded again on every frame either.
    /// </summary>
    public static Picture? Of(string blurhash)
    {
        lock (Gate)
        {
            if (Held.TryGetValue(blurhash, out var known))
            {
                return known;
            }
        }

        var blur = Blurhash.Decode(blurhash, Side, Side);

        lock (Gate)
        {
            if (Held.TryAdd(blurhash, blur))
            {
                Order.Enqueue(blurhash);

                while (Order.Count > MostHeld)
                {
                    Held.Remove(Order.Dequeue());
                }
            }

            return Held.GetValueOrDefault(blurhash, blur);
        }
    }
}
