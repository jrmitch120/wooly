namespace Wooly.Tui.Media;

/// <summary>
///     What a <see cref="PreparedAhead{TKey,TFrom,TValue}" /> keeps of what it has made: told of each value as it is
///     held and as it is handed out again, it says which to let go of. Asked under the cache's lock, so it needs none
///     of its own.
/// </summary>
internal interface IHoldPolicy<TKey>
{
    /// <summary>The value <paramref name="key" /> names has been handed out again.</summary>
    void Used(TKey key);

    /// <summary>
    ///     The value <paramref name="key" /> names has just been held: returns what is to be let go of for it.
    /// </summary>
    IReadOnlyList<TKey> Held(TKey key);

    /// <summary>The value <paramref name="key" /> names has been let go of by the cache's owner.</summary>
    void LetGo(TKey key);
}

/// <summary>
///     Holds no more than so many, letting go of the one used longest ago — with those <c>apart</c> says, counted in
///     room of their own, letting go of none of the rest (ADR-0035).
/// </summary>
/// <param name="most">How many are held, those counted apart left out.</param>
/// <param name="apart">Which are counted apart from the rest, or none.</param>
/// <param name="mostApart">How many of those are held.</param>
internal sealed class LeastRecentlyUsed<TKey>(int most, Func<TKey, bool>? apart = null, int mostApart = 0)
    : IHoldPolicy<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<TKey>> _held = [];

    /// <summary>Every one held but those counted apart, the one used last first.</summary>
    private readonly LinkedList<TKey> _used = new();

    /// <summary>Every one counted apart, the one used last first.</summary>
    private readonly LinkedList<TKey> _usedApart = new();

    public void Used(TKey key)
    {
        if (_held.TryGetValue(key, out var node))
        {
            var used = node.List!;

            used.Remove(node);
            used.AddFirst(node);
        }
    }

    public IReadOnlyList<TKey> Held(TKey key)
    {
        var (used, room) = apart?.Invoke(key) is true ? (_usedApart, mostApart) : (_used, most);
        List<TKey>? gone = null;

        _held[key] = used.AddFirst(key);

        while (used.Count > room)
        {
            var last = used.Last!.Value;

            used.RemoveLast();
            _held.Remove(last);
            (gone ??= []).Add(last);
        }

        return gone ?? [];
    }

    public void LetGo(TKey key)
    {
        if (_held.Remove(key, out var node))
        {
            node.List!.Remove(node);
        }
    }
}

/// <summary>
///     Values made from a key — a picture taken to its box, a sixel's cut — held as a <see cref="IHoldPolicy{TKey}" />
///     says, and made ahead elsewhere for what a frame will soon want, so that the frame wanting it does not make it
///     (#554). The one place the media path waits on work done elsewhere.
/// </summary>
/// <remarks>
///     A frame asking for a value being made elsewhere waits on it rather than making it a second time, which would be
///     the very work making it ahead was to save; where that failed, the frame makes it itself, as it would had nobody
///     made it ahead — except after an out-of-memory, which is thrown on. Whatever happens to it, a value being made
///     ahead is settled, an out-of-memory included, since a frame left waiting on one never settled would leave the UI
///     thread waiting for ever. And it is taken off the list of those being made only where it is still the one there:
///     let go of while it was being made, and asked for again since, it is the second's to settle, and the first holds
///     nothing.
/// </remarks>
/// <param name="work">
///     What makes a value from its key and what it is made from. Outside the lock, on whichever thread asked or was
///     made ahead on: this is the slow part.
/// </param>
/// <param name="elsewhere">
///     Where a value is made ahead: off the UI thread in the client, and wherever a test says.
/// </param>
/// <param name="policy">What is held, and what is let go of as more is.</param>
internal sealed class PreparedAhead<TKey, TFrom, TValue>(
    Func<TKey, TFrom, TValue> work,
    Action<Action> elsewhere,
    IHoldPolicy<TKey> policy) where TKey : notnull
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TKey, TValue> _held = [];
    private readonly Dictionary<TKey, TaskCompletionSource<TValue>> _preparing = [];

    /// <summary>
    ///     The value <paramref name="key" /> names: the one held, or the one being made ahead once it is, or made here
    ///     from <paramref name="from" /> where neither — and then held.
    /// </summary>
    public TValue Of(TKey key, TFrom from)
    {
        TaskCompletionSource<TValue>? preparing;

        lock (_gate)
        {
            if (_held.TryGetValue(key, out var known))
            {
                policy.Used(key);

                return known;
            }

            _preparing.TryGetValue(key, out preparing);
        }

        if (preparing is not null)
        {
            try
            {
                return preparing.Task.GetAwaiter().GetResult();
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                // Made here instead, as it would have been had nobody made it ahead.
            }
        }

        var made = work(key, from);

        lock (_gate)
        {
            return Hold(key, made);
        }
    }

    /// <summary>
    ///     Starts making the value <paramref name="key" /> names elsewhere, from <paramref name="from" />, where it is
    ///     neither held nor being made already.
    /// </summary>
    public void Prepare(TKey key, TFrom from)
    {
        var preparing = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_held.ContainsKey(key) || !_preparing.TryAdd(key, preparing))
            {
                return;
            }
        }

        elsewhere(() =>
        {
            try
            {
                var made = work(key, from);

                lock (_gate)
                {
                    // Let go of while it was being made, in which case it is nobody's to hold — nor to take off the
                    // list, where it has been asked for again since and is being made afresh.
                    if (Settled(key, preparing))
                    {
                        Hold(key, made);
                    }
                }

                preparing.SetResult(made);
            }
            catch (Exception failure)
            {
                lock (_gate)
                {
                    Settled(key, preparing);
                }

                preparing.SetException(failure);

                if (failure is OutOfMemoryException)
                {
                    throw;
                }
            }
        });
    }

    /// <summary>
    ///     Lets go of every value held or being made whose key <paramref name="gone" /> says: one being made is not
    ///     held once it is, and is made again if it is asked for again.
    /// </summary>
    public void LetGoOf(Func<TKey, bool> gone)
    {
        lock (_gate)
        {
            foreach (var key in _held.Keys.Where(gone).ToList())
            {
                _held.Remove(key);
                policy.LetGo(key);
            }

            foreach (var key in _preparing.Keys.Where(gone).ToList())
            {
                _preparing.Remove(key);
            }
        }
    }

    /// <summary>
    ///     Holds <paramref name="made" />, letting go of what the policy says, and returns what is held — the one made
    ///     first, where two raced to it. Under the lock.
    /// </summary>
    private TValue Hold(TKey key, TValue made)
    {
        if (_held.TryGetValue(key, out var known))
        {
            return known;
        }

        _held[key] = made;

        foreach (var gone in policy.Held(key))
        {
            _held.Remove(gone);
        }

        return made;
    }

    /// <summary>
    ///     Takes <paramref name="preparing" /> off the list where it is still the one there for
    ///     <paramref name="key" />, and says whether it was. Under the lock.
    /// </summary>
    private bool Settled(TKey key, TaskCompletionSource<TValue> preparing) =>
        _preparing.TryGetValue(key, out var listed) && listed == preparing && _preparing.Remove(key);
}
