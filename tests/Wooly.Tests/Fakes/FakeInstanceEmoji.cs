using Wooly.Core;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     The custom emoji an instance offers its people to write, without the instance (#578). A test says what the
///     instance offers, or that it will not say, and then asks how often it was asked, and as whom.
/// </summary>
internal sealed class FakeInstanceEmoji(Func<ActiveProfile, Task<IReadOnlyList<CustomEmoji>?>> answer) : IInstanceEmoji
{
    /// <summary>The profile every read was asked as, in order.</summary>
    public List<ActiveProfile> Reads { get; } = [];

    /// <summary>The access token every read was made with, in order — where a test proves who it was made as.</summary>
    public IEnumerable<string> Tokens => Reads.Select(profile => profile.AccessToken);

    /// <summary>
    ///     Whether a read waits to be landed by the test, through <see cref="Held" />, rather than being answered at once
    ///     (#579) — so a test can type on while the instance has not yet said.
    /// </summary>
    public bool Holding { get; set; }

    /// <summary>Every read held while <see cref="Holding" />, in order, each waiting to be landed.</summary>
    public List<Reading> Held { get; } = [];

    /// <summary>An instance offering <paramref name="shortcodes" />, each at a still of its own.</summary>
    public static FakeInstanceEmoji Offering(params string[] shortcodes) =>
        new(_ => Task.FromResult<IReadOnlyList<CustomEmoji>?>([.. shortcodes.Select(Emoji)]));

    /// <summary>Instances each offering their own, keyed by domain. An instance not among them does not answer.</summary>
    public static FakeInstanceEmoji Each(IReadOnlyDictionary<string, string[]> shortcodes) =>
        new(profile => Task.FromResult<IReadOnlyList<CustomEmoji>?>(
            shortcodes.FirstOrDefault(each => each.Key.Equals(profile.Instance, StringComparison.OrdinalIgnoreCase))
                      .Value?.Select(Emoji).ToList()));

    /// <summary>An instance that did not answer — refused, rate-limited, unreachable — having recorded the attempt.</summary>
    public static FakeInstanceEmoji Unanswering() => new(_ => Task.FromResult<IReadOnlyList<CustomEmoji>?>(null));

    /// <summary>
    ///     An instance whose answer is <paramref name="refusal" />, thrown rather than said as nothing: the token
    ///     refused, which the port leaves for whoever can sign the profile in again to hear.
    /// </summary>
    public static FakeInstanceEmoji Refusing(WoolyException refusal) =>
        new(_ => Task.FromException<IReadOnlyList<CustomEmoji>?>(refusal));

    /// <summary>The emoji written <paramref name="shortcode" />, as an instance would list it.</summary>
    public static CustomEmoji Emoji(string shortcode) => new(shortcode, $"https://files.example/emoji/{shortcode}.png");

    public Task<IReadOnlyList<CustomEmoji>?> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile);

        if (!Holding)
        {
            return answer(profile);
        }

        Held.Add(new Reading(() => answer(profile)));

        return Held[^1].Answer;
    }

    /// <summary>A read the instance has not answered yet, which the test lands when it chooses.</summary>
    internal sealed class Reading(Func<Task<IReadOnlyList<CustomEmoji>?>> answer)
    {
        private readonly TaskCompletionSource<IReadOnlyList<CustomEmoji>?> _landing = new();

        public Task<IReadOnlyList<CustomEmoji>?> Answer => _landing.Task;

        /// <summary>Answers what the instance offers, or its refusal.</summary>
        public void Land() => _landing.SetFromTask(answer());

        /// <summary>Answers nothing, as a rate-limited, unreachable or too-slow instance does.</summary>
        public void LandNothing() => _landing.SetResult(null);
    }
}
