using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An account's own defaults without the instance (#339). A test says what each profile's account posts at, or
///     that the instance will not say, and then asks how often it was asked.
/// </summary>
internal sealed class FakeAccountDefaults : IAccountDefaults
{
    private readonly Func<ActiveProfile, PostDefaults> _defaults;
    private readonly WoolyException? _refusal;

    private FakeAccountDefaults(Func<ActiveProfile, PostDefaults> defaults, WoolyException? refusal = null)
    {
        _defaults = defaults;
        _refusal = refusal;
    }

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>The profile every read asked as, by name, in order.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>Every account posting at <paramref name="defaults" />, or with nothing set where none are said.</summary>
    public static FakeAccountDefaults Setting(PostDefaults? defaults = null) =>
        new(_ => defaults ?? PostDefaults.Unknown);

    /// <summary>Each profile's account posting at what <paramref name="byProfile" /> says for its name.</summary>
    public static FakeAccountDefaults Setting(IReadOnlyDictionary<string, PostDefaults> byProfile) =>
        new(profile => byProfile.GetValueOrDefault(profile.Name, PostDefaults.Unknown));

    /// <summary>
    ///     An instance whose answer is <paramref name="refusal" />, thrown rather than said as unknown: the token
    ///     refused, which the port leaves for whoever can sign the profile in again to hear.
    /// </summary>
    public static FakeAccountDefaults Refusing(WoolyException refusal) => new(_ => PostDefaults.Unknown, refusal);

    public Task<PostDefaults> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Reads.Add(profile.Name);
        Tokens.Add(profile.AccessToken);

        return _refusal is null
            ? Task.FromResult(_defaults(profile))
            : Task.FromException<PostDefaults>(_refusal);
    }
}
