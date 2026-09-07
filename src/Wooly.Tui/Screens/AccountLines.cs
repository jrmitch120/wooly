using System.Globalization;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     An account as rows of spans: who they are, how much of a presence they have, and where this profile stands with
///     them. What <see cref="PostLines" /> is for a post, and here for the same reason — three screens draw an account
///     (its own, a follow request, a search result) and a name that read one way on one of them and another way on the
///     next would be three different ideas of the same thing.
/// </summary>
public static class AccountLines
{
    /// <summary>
    ///     What marks a custom field the instance has proved. It takes <see cref="Role.Muted" /> and no role of its
    ///     own: Mastodon only ever verifies a link, so the mark always lands on something already drawn in
    ///     <see cref="Role.Link" /> and a colour would say what the glyph has said (ADR-0019).
    /// </summary>
    private const string VerifiedMark = " ✓";

    /// <summary>How far a custom field's continuation is stepped in, so a wrapped value reads as one row.</summary>
    private const int FieldIndent = 2;

    /// <summary>
    ///     The header block: who they are, what they wrote about themselves, and what they are to the reader — the
    ///     four rows beside the avatar, then a section apiece for the bio, the custom fields, and the standing.
    /// </summary>
    /// <remarks>
    ///     It draws whole and cuts nothing. The worst case is around 29 rows, and that is fine: the screen scrolls and
    ///     one <c>j</c> puts the posts on screen, where a truncated bio would be permanent damage done to solve a
    ///     problem one keypress already solves (ADR-0019).
    ///     <para>
    ///         Every section brings its own separator, so an account with no bio, no fields, nobody in common and no
    ///         note collapses to the four rows and its standing with no gaps left behind — rather than each section
    ///         leaving a blank after it and an empty profile drawing as a column of them.
    ///     </para>
    /// </remarks>
    /// <param name="account">The account being drawn, as the instance last answered about them.</param>
    /// <param name="familiar">
    ///     The accounts the reader follows that also follow this one, or <see langword="null" /> where the instance
    ///     was never asked — which draws nothing, exactly as an empty list does. The distinction is in what the screen
    ///     was handed rather than in what it drew, and it is what lets the row be honest when a rate limit stops the
    ///     one call it costs (CONTEXT.md).
    /// </param>
    /// <param name="drawing">The room, the moment, and what this terminal can paint.</param>
    public static IReadOnlyList<Line> Header(
        Account account,
        IReadOnlyList<Account>? familiar,
        Drawing drawing)
    {
        var width = drawing.Width;

        var avatar = Avatar.Header(account.Address, account.AvatarUrl, drawing.Pictures);
        var room = Math.Max(0, width - avatar.Width);

        var lines = new List<Line>(avatar.Across(
            Line.Of(TextWrap.Clip(account.Author, room), Role.BylineName),
            Line.Of(TextWrap.Clip($"@{account.Address}", room), Role.BylineHandle),
            Presence(account, room),
            Line.Of(TextWrap.Clip(string.Join(" · ", [Joined(account), .. Flags(account)]), room), Role.Muted)));

        Section(lines, Bio(account.Bio, width));
        Section(lines, account.Fields.SelectMany(field => Field(field, width)));

        Section(lines, [
            Standing(account, width),
            .. InCommon(familiar, width),
            .. Note(account.Standing?.Note, width),
        ]);

        return lines;
    }

    /// <summary>The name and handle on one row, for a list where each account gets as few rows as it can.</summary>
    public static Line Byline(Account account, int width)
    {
        var name = TextWrap.Clip(account.Author, width);
        var handle = TextWrap.Clip($"@{account.Address}", Math.Max(0, width - name.Length - 1));

        return Line.Of([
            new Span(name, Role.BylineName),
            new Span(handle.Length > 0 ? $" {handle}" : string.Empty, Role.BylineHandle),
        ]);
    }

    /// <summary>How much of a presence they have: posts, and the two follow counts.</summary>
    public static Line Presence(Account account, int width) => Line.Of(
        TextWrap.Clip(
            $"{Number.Of(account.Posts)} posts · {Number.Of(account.Following)} following · {Number.Of(account.Followers)} followers",
            width),
        Role.Muted);

    /// <summary>
    ///     Where the profile stands with them, or the fact that the instance was not asked. Absent is not the same as
    ///     nothing (CONTEXT.md), and five silences would say the profile follows nobody.
    /// </summary>
    public static Line Standing(Account account, int width)
    {
        if (account.Standing is not { } standing)
        {
            return Line.Of("Standing not asked for.", Role.Muted);
        }

        var said = new List<string>();

        if (standing.Following)
        {
            said.Add("you follow them");
        }
        else if (standing.FollowRequested)
        {
            said.Add("you have asked to follow them");
        }

        if (standing.FollowedBy)
        {
            said.Add("they follow you");
        }

        if (standing.Blocking)
        {
            said.Add("blocked");
        }

        if (standing.Muting)
        {
            said.Add("muted");
        }

        return said.Count == 0
            ? Line.Of("No ties either way.", Role.Muted)
            : Line.Of(TextWrap.Clip(string.Join(" · ", said), width), Role.Muted);
    }

    /// <summary>
    ///     <paramref name="rows" /> under the blank that separates them from whatever is above, or nothing at all
    ///     where there are none — which is what stops a section that says nothing leaving a gap behind it.
    /// </summary>
    private static void Section(List<Line> lines, IEnumerable<Line> rows)
    {
        var said = rows.ToList();

        if (said.Count == 0)
        {
            return;
        }

        lines.Add(Line.Blank);
        lines.AddRange(said);
    }

    /// <summary>
    ///     When they arrived, as a month and a year — because nothing on a profile measures an age in days, and a date
    ///     here would only invite a time of day to be printed beside it.
    /// </summary>
    private static string Joined(Account account) =>
        $"Joined {account.Joined.ToString("MMM yyyy", CultureInfo.CurrentCulture)}";

    /// <summary>
    ///     Whichever of the two flags is true, the word carrying and the glyph decorating — so a terminal drawing
    ///     either as a box loses nothing. Both are omitted where false: a flag nobody set says nothing rather than
    ///     saying no.
    /// </summary>
    /// <remarks>
    ///     No emoji here or anywhere else on this screen: 🤖 and 🔒 are astral, <see cref="TextWrap.Clip" /> cuts by
    ///     <see cref="char" /> so a clip landing mid-pair would draw a broken glyph, and their column width is
    ///     terminal-dependent. Every glyph the TUI has today is BMP and one column, and it stays that way until
    ///     something makes <see cref="TextWrap" /> rune-aware (ADR-0019).
    /// </remarks>
    private static IEnumerable<string> Flags(Account account)
    {
        if (account.IsBot)
        {
            yield return "⚙ bot";
        }

        if (account.IsLocked)
        {
            yield return "⚿ locked";
        }
    }

    /// <summary>
    ///     What they wrote about themselves, wrapped whole, with the hashtags, handles and addresses in it drawn as
    ///     the references they are — the same rows a post's own text takes, off the same flattened plain text.
    /// </summary>
    private static IEnumerable<Line> Bio(string bio, int width)
    {
        if (string.IsNullOrWhiteSpace(bio))
        {
            return [];
        }

        var references = BodyText.References(bio);

        return TextWrap.Rows(bio, width).Select(row => new Line(BodyText.Spans(row, references)));
    }

    /// <summary>
    ///     One custom field: <c>Label: value ✓</c> on one row, wrapping, its continuation stepped in two columns so
    ///     that a long value reads as one row rather than as two fields.
    /// </summary>
    /// <remarks>
    ///     Wrapped as one text rather than as a label and a value laid out separately, which is what keeps an address
    ///     the break cuts in two one reference across both rows — <see cref="TextWrap.Row" />'s offsets already do
    ///     this for a post's text, and the references are found on the value alone and moved along by the label, so
    ///     nothing in a label a reader chose to call <c>https</c> is ever painted as a link.
    /// </remarks>
    private static IEnumerable<Line> Field(AccountField field, int width)
    {
        var label = $"{field.Label}: ";
        var said = label + field.Said + (field.Verified is null ? string.Empty : VerifiedMark);
        var references = BodyText
            .References(field.Said)
            .Select(reference => reference with { At = reference.At + label.Length })
            .ToList();

        return Hanging(said, width).Select((row, at) =>
        {
            var line = new Line(MutedOutside(BodyText.Spans(row, references), row.At, label.Length, label.Length + field.Said.Length));

            return at == 0 ? line : line.After(new Span(new string(' ', FieldIndent), Role.Body));
        });
    }

    /// <summary>
    ///     The rows <paramref name="text" /> takes with the first at the full width and every one under it stepped in
    ///     <see cref="FieldIndent" /> columns, which is what a continuation is and what the first row is not.
    /// </summary>
    /// <remarks>
    ///     Wrapped in two passes because <see cref="TextWrap" /> wraps at one width throughout — a post's text has
    ///     never wanted a hanging indent — and wrapping the whole field narrow instead would spend two columns of the
    ///     first row on nothing. The offsets of the second pass are moved back onto the whole text, so a reference cut
    ///     across the break is still one reference written in two places.
    /// </remarks>
    /// <param name="text">The whole field, label and value together.</param>
    /// <param name="width">How much room the first row gets.</param>
    private static IReadOnlyList<TextWrap.Row> Hanging(string text, int width)
    {
        var rows = TextWrap.Rows(text, width);

        if (rows.Count < 2)
        {
            return rows;
        }

        var rest = rows[0].End;

        // Whatever the wrap broke on belongs to neither row: the spaces it stepped over, and the author's own line
        // break where that is what ended the row — which a second pass would otherwise open with an empty row for.
        while (rest < text.Length && text[rest] == ' ')
        {
            rest++;
        }

        if (rest < text.Length && text[rest] == '\n')
        {
            rest++;
        }

        return
        [
            rows[0],
            .. TextWrap
                .Rows(text[rest..], Math.Max(1, width - FieldIndent))
                .Select(row => row with { At = row.At + rest }),
        ];
    }

    /// <summary>
    ///     <paramref name="spans" /> with everything outside <c>[from, to)</c> — the label, and the <c>✓</c> after the
    ///     value — put back into <see cref="Role.Muted" />, whatever role the text they were read out of took.
    /// </summary>
    /// <param name="spans">The runs across one row, as the text's own references made them.</param>
    /// <param name="at">Where that row starts in the text it was wrapped out of.</param>
    /// <param name="from">Where the value begins in that text.</param>
    /// <param name="to">Where it ends.</param>
    private static IReadOnlyList<Span> MutedOutside(IReadOnlyList<Span> spans, int at, int from, int to)
    {
        var roled = new List<Span>();

        foreach (var span in spans)
        {
            var end = at + span.Text.Length;

            (int From, int To, Role Role)[] pieces =
            [
                (at, Math.Min(end, from), Role.Muted),
                (Math.Max(at, from), Math.Min(end, to), span.Role),
                (Math.Max(at, to), end, Role.Muted),
            ];

            roled.AddRange(pieces
                .Where(piece => piece.To > piece.From)
                .Select(piece => new Span(span.Text[(piece.From - at)..(piece.To - at)], piece.Role)));

            at = end;
        }

        return roled;
    }

    /// <summary>
    ///     Who the reader already knows here: two of them by handle and a count of the rest, on one row. Handles
    ///     rather than display names, because a display name is not unique and this row is making an identity claim.
    /// </summary>
    /// <remarks>
    ///     Nothing where nobody is in common, and nothing where the question was never put — the one row on this
    ///     screen whose absence says two different things, and deliberately: it is asked last of the calls an arrival
    ///     makes, so a rate limit costs this row and nothing else (ADR-0012's amendment).
    /// </remarks>
    private static IEnumerable<Line> InCommon(IReadOnlyList<Account>? familiar, int width)
    {
        if (familiar is not { Count: > 0 })
        {
            return [];
        }

        var named = familiar.Take(2).Select(account => $"@{account.Address}").ToList();
        var rest = familiar.Count - named.Count;

        var said = rest switch
        {
            0 => string.Join(" and ", named),
            1 => $"{string.Join(", ", named)} and 1 other",
            _ => $"{string.Join(", ", named)} and {Number.Of(rest)} others",
        };

        return [Line.Of(TextWrap.Clip($"Followed by {said}", width), Role.Muted)];
    }

    /// <summary>
    ///     What the reader wrote to themselves about this account, wrapped whole rather than clipped: it is their own
    ///     writing, and cutting it would be cutting their words.
    /// </summary>
    private static IEnumerable<Line> Note(string? note, int width) =>
        string.IsNullOrWhiteSpace(note)
            ? []
            : TextWrap.Wrap($"Your note: {note}", width).Select(row => Line.Of(row, Role.Muted));
}
