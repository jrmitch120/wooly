using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     What each profile's account posts at by default, for the session (ADR-0024, #339): asked when the session starts
///     acting as a profile, so that a compose screen opens on it without asking anything, and then held. By profile
///     rather than by instance, since the defaults are the account's own settings — and a switch back to a profile
///     already asked about asks nothing again. Never written to disk.
/// </summary>
/// <remarks>
///     Not asked through the enquiry, for the reason <see cref="LimitsByInstance" /> gives. Unlike a limit, an account
///     that did not answer is held as unknown rather than asked again: compose without the defaults sends what it
///     always did, which is not worth a request at every compose.
/// </remarks>
/// <param name="port">Where an account's defaults are asked.</param>
/// <param name="host">The drawing thread, which an answer is handed back on.</param>
public sealed class DefaultsByProfile(IAccountDefaults port, IShellHost host)
{
    /// <summary>What each account answered, or unknown where it has been asked and has not — or never will.</summary>
    private readonly Dictionary<string, PostDefaults> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Asks for <paramref name="profile" />'s account's defaults where nobody has yet this session. Until the answer
    ///     lands they are unknown.
    /// </summary>
    public void Ask(ActiveProfile profile)
    {
        if (_known.TryAdd(profile.Name, PostDefaults.Unknown))
        {
            _ = Read(profile);
        }
    }

    /// <summary>
    ///     What <paramref name="profile" />'s account posts at, as far as it is known: <see cref="PostDefaults.Unknown" />
    ///     until it has answered, and for good where it could not.
    /// </summary>
    public PostDefaults For(ActiveProfile profile) => _known.GetValueOrDefault(profile.Name, PostDefaults.Unknown);

    private async Task Read(ActiveProfile profile)
    {
        PostDefaults defaults;

        try
        {
            defaults = await port.Read(profile, CancellationToken.None);
        }
        catch (WoolyException)
        {
            // A refused token, which the reads that fill the screen already say with the key that fixes it.
            return;
        }

        host.OnUiThread(() => _known[profile.Name] = defaults);
    }
}
