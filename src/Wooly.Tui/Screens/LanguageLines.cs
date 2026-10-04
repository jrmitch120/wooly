using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a language says in the list under compose's Lang (#340): its code, then its own name, as Lang itself writes
///     it once picked (<see cref="ComposeScreen.Spoken" />). The list round them is <see cref="PickLines" />'s.
/// </summary>
public static class LanguageLines
{
    /// <summary>One language, given whether it is the picked one: its code and, dimmer, its own name.</summary>
    public static IEnumerable<Span> Row(PostLanguage language, bool picked)
    {
        var spoken = ComposeScreen.Spoken(language);
        var code = language.Code.Length;

        return
        [
            new Span(spoken[..code], picked ? Role.ReferencePicked : Role.Body),
            new Span(spoken[code..], picked ? Role.Body : Role.Muted),
        ];
    }

    /// <summary>What a language's row takes.</summary>
    public static int Columns(PostLanguage language) => Glyphs.Columns(ComposeScreen.Spoken(language));
}
