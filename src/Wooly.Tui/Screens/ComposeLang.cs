using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     What compose's Lang holds, and what that stands for (ADR-0024, #340): a language as <see cref="Spoken" /> writes
///     it, a code or a name being typed, nothing, or something that is not a language at all — which is refused at send
///     (<see cref="Refusal" />) rather than as it is typed, since half of a name is what a field holds on the way to the
///     whole of one.
/// </summary>
/// <param name="Held">What the field holds, letter for letter.</param>
/// <param name="Opened">
///     The code Lang opened on, which an edit sends back as it is even where this client lists no such language — the
///     instance knew it, and an author who did not touch Lang has not asked for anything else.
/// </param>
public sealed record ComposeLang(string Held, string? Opened)
{
    /// <summary>The columns between a language's code and its own name, in Lang and in its list.</summary>
    public const string CodeGap = "  ";

    /// <summary>
    ///     Lang opened on <paramref name="code" />: the language it is, as <see cref="Spoken" /> writes it, or the bare
    ///     code where this client does not list it — or empty, for none.
    /// </summary>
    public static ComposeLang Opening(string? code) => new(
        code switch
        {
            null => string.Empty,
            _ => PostLanguageName.Of(code) is { } known ? Spoken(known) : code,
        },
        code);

    /// <summary>A language as Lang and its list write it: its code, then its own name — <c>fr  Français</c>.</summary>
    public static string Spoken(PostLanguage language) => $"{language.Code}{CodeGap}{language.OwnName}";

    /// <summary>
    ///     Why the post cannot go out in what Lang holds, or <see langword="null" /> where it can: empty, a language, or
    ///     the code it opened on.
    /// </summary>
    public string? Refusal => Resolves(out _) ? null : PostLanguageName.Rejection(Held.Trim());

    /// <summary>The language Lang holds, if it holds one this client lists — which its list opens picked on.</summary>
    public PostLanguage? Language => Resolves(out var code) && code is not null ? PostLanguageName.Of(code) : null;

    /// <summary>Whether Lang holds a language exactly as <see cref="Spoken" /> writes it, as picking one leaves it.</summary>
    public bool HoldsALanguageWhole => Language is { } language && Held == Spoken(language);

    /// <summary>
    ///     The languages Lang's list offers: those matching what is being typed, or every one where the field is empty or
    ///     already holds a language whole — a click asking for the list is not a search.
    /// </summary>
    public IReadOnlyList<PostLanguage> Offered =>
        Held.Trim().Length == 0 || HoldsALanguageWhole ? PostLanguageName.All : PostLanguageName.Matching(Held);

    /// <summary>
    ///     What Lang stands for: nothing, where it is empty; a language, where it holds one as <see cref="Spoken" />
    ///     writes it or as <see cref="PostLanguageName.Parse" /> reads a code or a name; or the code it opened on.
    ///     Anything else is not a language, and this says so.
    /// </summary>
    /// <param name="code">The code it stands for, or <see langword="null" /> for none.</param>
    public bool Resolves(out string? code)
    {
        var held = Held.Trim();

        code = null;

        if (held.Length == 0)
        {
            return true;
        }

        if (Opened is { } opened && string.Equals(held, opened, StringComparison.OrdinalIgnoreCase))
        {
            code = opened;

            return true;
        }

        var language = PostLanguageName.Parse(held)
                       ?? (PostLanguageName.Of(held.Split(' ')[0]) is { } written
                           && string.Equals(Spoken(written), held, StringComparison.Ordinal)
                               ? written
                               : null);

        code = language?.Code;

        return language is not null;
    }
}
