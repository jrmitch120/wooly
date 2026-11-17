using System.Net.Http;
using System.Text.Json;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     What each instance's translator can do, for the session (#616): its languages map, asked when the session starts
///     acting as a profile on it and then held, so that whether <c>t</c> is offered on a post costs no request — the
///     map is read once per instance, never per post or per screen (ADR-0037). By instance rather than by profile, as
///     the post limits beside it are (<see cref="Drafting.LimitsByInstance" />): the translator is the instance's, so
///     two profiles on one share it. Never written to disk.
/// </summary>
/// <remarks>
///     Not asked through the enquiry, for the reason <see cref="Drafting.LimitsByInstance" /> gives: nothing on the
///     status row is worth a spinner or a notice for it. Until the map lands an instance takes nothing, so <c>t</c> is
///     never offered on a guess; an empty map — no translator, or an instance without the endpoint — is held as the
///     answer it is. An instance that failed to answer is held as taking nothing too, and asked again only when the
///     session next starts acting as a profile on it, rather than at every frame that wonders.
/// </remarks>
/// <param name="port">Where an instance is asked.</param>
/// <param name="host">The drawing thread, which an answer is handed back on.</param>
public sealed class LanguagesByInstance(IPostTranslation port, IShellHost host)
{
    private readonly Dictionary<string, TranslationLanguages> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The instances asked and not yet answered, so none is asked twice at once.</summary>
    private readonly HashSet<string> _asking = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>An instance answered, on the drawing thread — which is when what is offered may have changed.</summary>
    public event Action? Heard;

    /// <summary>Asks for <paramref name="profile" />'s instance's map where nobody has this session, or it failed to answer.</summary>
    public void Ask(ActiveProfile profile)
    {
        if (!_known.ContainsKey(profile.Instance) && _asking.Add(profile.Instance))
        {
            _ = Read(profile);
        }
    }

    /// <summary>
    ///     <paramref name="profile" />'s instance's map as far as it is known: <see cref="TranslationLanguages.None" />
    ///     until it has answered.
    /// </summary>
    public TranslationLanguages For(ActiveProfile profile) =>
        _known.GetValueOrDefault(profile.Instance, TranslationLanguages.None);

    private async Task Read(ActiveProfile profile)
    {
        TranslationLanguages? languages;

        try
        {
            languages = await port.Languages(profile, CancellationToken.None);
        }
        catch (Exception unanswered) when (unanswered is WoolyException or HttpRequestException or JsonException)
        {
            // A refused token, which the reads that fill the screen already say with the key that fixes it; or an
            // instance not answering, which offering nothing until it is asked again says well enough.
            languages = null;
        }

        host.OnUiThread(() =>
        {
            _asking.Remove(profile.Instance);

            if (languages is null)
            {
                return;
            }

            _known[profile.Instance] = languages;

            Heard?.Invoke();
        });
    }
}
