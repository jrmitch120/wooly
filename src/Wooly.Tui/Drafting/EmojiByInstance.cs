using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Drafting;

/// <summary>
///     The custom emoji each instance offers its people to write, for the session (#578, #579): asked the first time a
///     <c>:</c>-word would open the list rather than at launch or on composing, so a reader who never writes one is never
///     charged a request for it, and then held, never refreshed. By instance rather than by profile, beside the limits
///     drafting keeps the same way (<see cref="LimitsByInstance" />): they are the instance's, so two profiles on one
///     instance share one read, and a switch away and back reads nothing again. Never written to disk.
/// </summary>
/// <remarks>
///     <para>
///         Not asked through the enquiry, for the reason <see cref="LimitsByInstance" /> gives: nothing on the status
///         row, whatever happens — in flight, failed, or refused, which the reads that fill the screen say. An instance
///         is never asked twice at once, nor again once it has answered, even with none.
///     </para>
///     <para>
///         An answer that lands after the keystroke that asked is kept, but offered only from the next keystroke, so the
///         list never jumps open by itself on a redraw. One that does not answer offers nothing, and may be asked again;
///         when is the asker's to say (<see cref="Views.EmojiOffering" />).
///     </para>
/// </remarks>
/// <param name="port">Where an instance is asked.</param>
/// <param name="host">The drawing thread, which an answer is handed back on.</param>
public sealed class EmojiByInstance(IInstanceEmoji port, IShellHost host)
{
    /// <summary>What each instance answered, as far as a keystroke has taken it up.</summary>
    private readonly Dictionary<string, IReadOnlyList<CustomEmoji>> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each instance answered since the last keystroke, waiting for the next to be offered.</summary>
    private readonly Dictionary<string, IReadOnlyList<CustomEmoji>> _landed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The instances asked and not yet answered, so none is asked twice at once.</summary>
    private readonly HashSet<string> _asking = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The custom emoji <paramref name="profile" />'s instance offers as far as they are known — none until it has
    ///     said. What landed since the last keystroke is taken up only where <paramref name="typed" />.
    /// </summary>
    /// <param name="typed">Whether a keystroke in the editor asks, rather than a redraw.</param>
    public IReadOnlyList<CustomEmoji> For(ActiveProfile profile, bool typed)
    {
        if (typed && _landed.Remove(profile.Instance, out var landed))
        {
            _known[profile.Instance] = landed;
        }

        return _known.GetValueOrDefault(profile.Instance) ?? [];
    }

    /// <summary>
    ///     Asks <paramref name="profile" />'s instance for its emoji, as <paramref name="profile" />, unless it has
    ///     answered already or is being asked now.
    /// </summary>
    public void Ask(ActiveProfile profile)
    {
        if (_known.ContainsKey(profile.Instance)
            || _landed.ContainsKey(profile.Instance)
            || !_asking.Add(profile.Instance))
        {
            return;
        }

        var read = port.Read(profile, CancellationToken.None);

        // An answer in hand already, as on the drawing thread now: kept at once rather than a trip round the loop later.
        if (read.IsCompleted)
        {
            Heard(profile.Instance, Answered(read));

            return;
        }

        _ = Landing(profile.Instance, read);
    }

    private async Task Landing(string instance, Task<IReadOnlyList<CustomEmoji>?> read)
    {
        // Whatever it ended in is read on the drawing thread, where everything else here is.
        await ((Task)read).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        // Kept, and nothing more: the list opens on the next keystroke in the word, rather than jumping open by itself.
        host.OnUiThread(() => Heard(instance, Answered(read)));
    }

    /// <summary>
    ///     What a finished read answered, where it answered: a refused token is nothing here, being said by the reads
    ///     that fill the screen with the key that fixes it.
    /// </summary>
    private static IReadOnlyList<CustomEmoji>? Answered(Task<IReadOnlyList<CustomEmoji>?> read)
    {
        try
        {
            return read.GetAwaiter().GetResult();
        }
        catch (WoolyException)
        {
            return null;
        }
    }

    private void Heard(string instance, IReadOnlyList<CustomEmoji>? emoji)
    {
        _asking.Remove(instance);

        if (emoji is not null)
        {
            _landed[instance] = emoji;
        }
    }
}
