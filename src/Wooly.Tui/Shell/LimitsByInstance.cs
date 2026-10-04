using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     How long each instance lets a post be, for the session (#319): asked the first time a post is written on an
///     instance rather than at launch, so a reader who never writes one is never charged a request for it, and then
///     held. By instance rather than by profile, since the limit is the instance's setting — two profiles on one share
///     it, and a switch keeps it. Never written to disk.
/// </summary>
/// <remarks>
///     Not asked through the enquiry, on purpose: it would count a rate limit down over the post and say a failure on
///     the status row, and nothing a limit is wanted for is worth either. An instance that does not answer leaves
///     Mastodon's default standing, silently, and is asked again the next time a post is written there.
/// </remarks>
/// <param name="port">Where an instance is asked.</param>
/// <param name="host">The drawing thread, which an answer is handed back on.</param>
public sealed class LimitsByInstance(IInstanceLimits port, IShellHost host)
{
    private readonly Dictionary<string, PostLimits> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The instances asked and not yet answered, so none is asked twice at once.</summary>
    private readonly HashSet<string> _asking = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>An instance answered, on the drawing thread: which one, and what it said.</summary>
    public event Action<string, PostLimits>? Heard;

    /// <summary>
    ///     The limits on <paramref name="profile" />'s instance as far as they are known — Mastodon's own until the
    ///     instance has said — asking it where nobody has yet, whose answer is <see cref="Heard" /> when it lands.
    /// </summary>
    public PostLimits For(ActiveProfile profile)
    {
        if (_known.TryGetValue(profile.Instance, out var known))
        {
            return known;
        }

        if (_asking.Add(profile.Instance))
        {
            _ = Ask(profile);
        }

        return PostLimits.Default;
    }

    private async Task Ask(ActiveProfile profile)
    {
        PostLimits? limits;

        try
        {
            limits = await port.Read(profile, CancellationToken.None);
        }
        catch (WoolyException)
        {
            // A refused token, which the reads that fill the screen already say with the key that fixes it.
            limits = null;
        }

        host.OnUiThread(() =>
        {
            _asking.Remove(profile.Instance);

            if (limits is null)
            {
                return;
            }

            _known[profile.Instance] = limits;

            Heard?.Invoke(profile.Instance, limits);
        });
    }
}
