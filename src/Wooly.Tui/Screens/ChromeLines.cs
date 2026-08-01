using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The two rows that are not a screen: the breadcrumb above the content, and the status row along the bottom. Both
///     are the frame rather than the thing being read, which is why they never move and never scroll.
/// </summary>
public static class ChromeLines
{
    /// <summary>What the breadcrumb says while a fetch is in flight, said here and nowhere else.</summary>
    private const string Fetching = "fetching…";

    /// <summary>
    ///     Where you are in the stack, with the fetch marker at its right. This is the one place a fetch in flight is
    ///     announced — the rail holds still (ADR-0014) — and it is beside the content it is about to replace.
    /// </summary>
    public static Line Breadcrumb(string trail, bool fetching, int width)
    {
        var mark = fetching ? Fetching : string.Empty;
        var room = Math.Max(0, width - mark.Length - 1);
        var shown = TextWrap.Clip(trail, room);

        return Line.Of([
            new Span(shown, Role.Chrome),
            new Span(new string(' ', Math.Max(1, width - shown.Length - mark.Length)), Role.Chrome),
            new Span(mark, Role.Loading),
        ]);
    }

    /// <summary>
    ///     The status row: what this screen's keys are, or — when there is one — the thing the shell has to say
    ///     instead. A confirmation displaces the keys because it is the only thing on screen worth answering.
    /// </summary>
    public static Line Status(
        IReadOnlyList<KeyHint> keys,
        string? notice,
        bool noticeIsError,
        Shell.Confirmation? asking,
        int width)
    {
        if (asking is { } question)
        {
            return Line.Of([
                new Span(TextWrap.Clip($" {question.Question}", width), Role.Destructive),
                new Span(
                    TextWrap.Clip(
                        $"  {question.Confirm} {question.Going} · esc keep",
                        Math.Max(0, width - question.Question.Length - 1)),
                    Role.Muted),
            ]);
        }

        if (notice is { } said)
        {
            return Line.Of(TextWrap.Clip($" {said}", width), noticeIsError ? Role.Error : Role.Muted);
        }

        return Line.Of(TextWrap.Clip($" {string.Join(" · ", keys.Select(key => key.ToString()))}", width), Role.Chrome);
    }
}
