namespace Wooly.Core.Posts;

/// <summary>
///     A language a post can say it is in.
/// </summary>
/// <param name="Code">
///     What the instance is sent: an ISO 639-1 code, or an ISO 639-3 one for a language the shorter list has no code for.
/// </param>
/// <param name="Name">What the language is called in English.</param>
/// <param name="OwnName">What the language calls itself, which is how most of the people writing in it know it.</param>
public sealed record PostLanguage(string Code, string Name, string OwnName);
