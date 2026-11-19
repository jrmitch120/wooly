using Wooly.Core.Posts;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A translation with everything filled in, so a test only says the part it is about — the counterpart to
///     <see cref="APost" />, whose defaults it translates: German into English, by DeepL.
/// </summary>
internal static class ATranslation
{
    public static Translation With(
        string content = "Hello world, translated",
        string? contentWarning = null,
        string? pollId = null,
        IReadOnlyList<string>? pollOptions = null,
        IReadOnlyDictionary<string, string>? descriptions = null,
        string? from = "de",
        string to = "en",
        string provider = "DeepL.com") => new()
    {
        Content = content,
        ContentWarning = contentWarning,
        PollId = pollId,
        PollOptions = pollOptions ?? [],
        Descriptions = descriptions ?? new Dictionary<string, string>(),
        From = from,
        To = to,
        Provider = provider,
    };
}
