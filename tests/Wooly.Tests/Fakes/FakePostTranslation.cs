using System.Net;
using Wooly.Core.Errors;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's translator without the instance — ADR-0005's primary seam for translation, beside
///     <see cref="FakeTrends" />: a test scripts what the instance translates a post into, or how it refuses, and then
///     asks what was asked for. Never fakes HTTP to do it.
/// </summary>
internal sealed class FakePostTranslation : IPostTranslation
{
    private readonly Func<string, Translation> _translation;
    private readonly WoolyException? _refusal;

    private FakePostTranslation(Func<string, Translation> translation, WoolyException? refusal = null)
    {
        _translation = translation;
        _refusal = refusal;
    }

    /// <summary>Every translation it was asked for, in order — where a test proves which post, target and profile.</summary>
    public List<Asked> Requests { get; } = [];

    /// <summary>An instance that translates every post it is asked about into <paramref name="translation" />.</summary>
    public static FakePostTranslation Translating(Translation? translation = null) =>
        new(_ => translation ?? ATranslation.With());

    /// <summary>An instance translating each post by its id, through <paramref name="translationOf" />.</summary>
    public static FakePostTranslation Translating(Func<string, Translation> translationOf) => new(translationOf);

    /// <summary>
    ///     An instance that will not translate, saying <paramref name="reason" /> — a followers-only or direct post, a
    ///     pair its translator does not take, or no translator at all.
    /// </summary>
    public static FakePostTranslation Refusing(string reason = "This action is not allowed") =>
        new(_ => ATranslation.With(), TranslationRefusedException.For(reason));

    /// <summary>An instance whose translator failed to answer — out of quota, rate-limited or down — saying <paramref name="reason" />.</summary>
    public static FakePostTranslation Failing(
        string reason = "There have been too many requests to the translation service recently.") =>
        new(
            _ => ATranslation.With(),
            new InstanceFailedException("mastodon.social", HttpStatusCode.ServiceUnavailable, reason));

    /// <summary>An instance refusing every translation with <paramref name="refusal" />, a rate limit among them.</summary>
    public static FakePostTranslation Refusing(WoolyException refusal) => new(_ => ATranslation.With(), refusal);

    public Task<Translation> Translate(
        ActiveProfile profile,
        string postId,
        string? target,
        CancellationToken cancellationToken)
    {
        Requests.Add(new Asked(profile.Name, profile.AccessToken, postId, target));

        return _refusal is null
            ? Task.FromResult(_translation(postId))
            : Task.FromException<Translation>(_refusal);
    }

    /// <summary>One translation asked for: as which profile and token, of which post, and into what — or nothing.</summary>
    internal sealed record Asked(string Profile, string Token, string PostId, string? Target);
}
