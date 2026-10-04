using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's post limits without the instance (#319). A test says what the instance sets, or that it will not
///     say, and then asks how often it was asked.
/// </summary>
internal sealed class FakeInstanceLimits : IInstanceLimits
{
    private readonly PostLimits? _limits;
    private readonly WoolyException? _refusal;

    private FakeInstanceLimits(PostLimits? limits, WoolyException? refusal = null)
    {
        _limits = limits;
        _refusal = refusal;
    }

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>The instance every read asked, in order.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>An instance setting <paramref name="limits" />, or Mastodon's own where none are said.</summary>
    public static FakeInstanceLimits Setting(PostLimits? limits = null) => new(limits ?? PostLimits.Default);

    /// <summary>An instance that did not answer — refused, rate-limited, unreachable — having recorded the attempt.</summary>
    public static FakeInstanceLimits Unanswering() => new(null);

    /// <summary>
    ///     An instance whose answer is <paramref name="refusal" />, thrown rather than said as nothing: the token
    ///     refused, which the port leaves for whoever can sign the profile in again to hear.
    /// </summary>
    public static FakeInstanceLimits Refusing(WoolyException refusal) => new(PostLimits.Default, refusal);

    public Task<PostLimits?> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile.Instance);
        Tokens.Add(profile.AccessToken);

        return _refusal is null ? Task.FromResult(_limits) : Task.FromException<PostLimits?>(_refusal);
    }
}
