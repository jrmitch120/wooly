using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     The custom emoji an instance offers its people to write, without the instance (#578). A test says what the
///     instance offers, or that it will not say, and then asks how often it was asked, and as whom.
/// </summary>
internal sealed class FakeInstanceEmoji : IInstanceEmoji
{
    private readonly IReadOnlyList<CustomEmoji>? _offered;
    private readonly WoolyException? _refusal;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CustomEmoji>>? _each;

    private FakeInstanceEmoji(
        IReadOnlyList<CustomEmoji>? offered,
        WoolyException? refusal = null,
        IReadOnlyDictionary<string, IReadOnlyList<CustomEmoji>>? each = null)
    {
        _offered = offered;
        _refusal = refusal;
        _each = each;
    }

    /// <summary>The profile every read was asked as, in order.</summary>
    public List<ActiveProfile> Reads { get; } = [];

    /// <summary>The access token every read was made with, in order — where a test proves who it was made as.</summary>
    public IEnumerable<string> Tokens => Reads.Select(profile => profile.AccessToken);

    /// <summary>An instance offering <paramref name="shortcodes" />, each at a still of its own.</summary>
    public static FakeInstanceEmoji Offering(params string[] shortcodes) => new([.. shortcodes.Select(Emoji)]);

    /// <summary>
    ///     Instances each offering their own, keyed by domain. An instance not among them does not answer.
    /// </summary>
    public static FakeInstanceEmoji Each(IReadOnlyDictionary<string, string[]> shortcodes) =>
        new(null, each: shortcodes.ToDictionary(
            instance => instance.Key,
            IReadOnlyList<CustomEmoji> (instance) => [.. instance.Value.Select(Emoji)],
            StringComparer.OrdinalIgnoreCase));

    /// <summary>An instance that did not answer — refused, rate-limited, unreachable — having recorded the attempt.</summary>
    public static FakeInstanceEmoji Unanswering() => new(null);

    /// <summary>
    ///     An instance whose answer is <paramref name="refusal" />, thrown rather than said as nothing: the token
    ///     refused, which the port leaves for whoever can sign the profile in again to hear.
    /// </summary>
    public static FakeInstanceEmoji Refusing(WoolyException refusal) => new([], refusal);

    /// <summary>The emoji written <paramref name="shortcode" />, as an instance would list it.</summary>
    public static CustomEmoji Emoji(string shortcode) =>
        new(shortcode, $"https://files.example/emoji/{shortcode}.png");

    public Task<IReadOnlyList<CustomEmoji>?> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile);

        if (_refusal is not null)
        {
            return Task.FromException<IReadOnlyList<CustomEmoji>?>(_refusal);
        }

        return Task.FromResult(_each is null ? _offered : _each.GetValueOrDefault(profile.Instance));
    }
}
