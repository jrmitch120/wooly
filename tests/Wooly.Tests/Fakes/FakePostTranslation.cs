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
    private readonly Task<Translation>? _held;

    private FakePostTranslation(
        Func<string, Translation> translation,
        WoolyException? refusal = null,
        Task<Translation>? held = null)
    {
        _translation = translation;
        _refusal = refusal;
        _held = held;
    }

    /// <summary>
    ///     What the languages map is answered with (#616): by default an instance whose translator takes German and
    ///     English to each other, and a post with no language to either — LibreTranslate loading the two, as the
    ///     integration suite's does.
    /// </summary>
    private Task<TranslationLanguages> _languages = Task.FromResult(Map(
        ("de", ["en"]),
        ("en", ["de"]),
        (TranslationLanguages.Undetermined, ["de", "en"])));

    /// <summary>Every translation it was asked for, in order — where a test proves which post, target and profile.</summary>
    public List<Asked> Requests { get; } = [];

    /// <summary>
    ///     Every time the languages map was read, as which profile and token — where a test proves it was read once per
    ///     instance rather than per post or per screen.
    /// </summary>
    public List<(string Profile, string Token)> LanguageReads { get; } = [];

    /// <summary>A languages map out of each language a post can be in and the languages it can be taken to.</summary>
    public static TranslationLanguages Map(params (string From, string[] To)[] pairs) =>
        new(pairs.ToDictionary(pair => pair.From, IReadOnlyList<string> (pair) => pair.To));

    /// <summary>This instance, answering its languages map with <paramref name="languages" /> — none, for no translator.</summary>
    public FakePostTranslation Taking(TranslationLanguages languages) => Taking(Task.FromResult(languages));

    /// <summary>
    ///     This instance, answering its languages map with <paramref name="languages" /> when the test says — or failing
    ///     to, where it is a faulted task.
    /// </summary>
    public FakePostTranslation Taking(Task<TranslationLanguages> languages)
    {
        _languages = languages;

        return this;
    }

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

    /// <summary>
    ///     An instance whose answer is <paramref name="held" />, landing when the test says — where a test proves what
    ///     shows while the question is in flight.
    /// </summary>
    public static FakePostTranslation Awaiting(Task<Translation> held) => new(_ => ATranslation.With(), held: held);

    /// <summary>An instance refusing every translation with <paramref name="refusal" />, a rate limit among them.</summary>
    public static FakePostTranslation Refusing(WoolyException refusal) => new(_ => ATranslation.With(), refusal);

    public Task<Translation> Translate(
        ActiveProfile profile,
        string postId,
        string? target,
        CancellationToken cancellationToken)
    {
        Requests.Add(new Asked(profile.Name, profile.AccessToken, postId, target));

        if (_held is { } held)
        {
            return held;
        }

        return _refusal is null
            ? Task.FromResult(_translation(postId))
            : Task.FromException<Translation>(_refusal);
    }

    public Task<TranslationLanguages> Languages(ActiveProfile profile, CancellationToken cancellationToken)
    {
        LanguageReads.Add((profile.Name, profile.AccessToken));

        return _languages;
    }

    /// <summary>One translation asked for: as which profile and token, of which post, and into what — or nothing.</summary>
    internal sealed record Asked(string Profile, string Token, string PostId, string? Target);
}
