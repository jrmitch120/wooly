using System.Globalization;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     An account as rows of spans: who they are, how much of a presence they have, and where this profile stands with
///     them. What <see cref="PostLines" /> is for a post, and here for the same reason — five screens draw an account
///     (its own, a search result, a follow request, a follow list, Discover) and a name that read one way on one of
///     them and another way on the next would be five different ideas of the same thing.
/// </summary>
/// <remarks>
///     There is one shape for a person and it is <see cref="Block" />: the four rows beside the 8×4 avatar that
///     <see cref="Header" /> opens with, drawn by every screen that merely <em>lists</em> somebody. The header block
///     is then the Account block <em>plus</em> the bio, the custom fields and the standing, rather than a near-copy of
///     its top — because two shapes that drift by even a flag are a locked account reading as locked on its own screen
///     and not on the list it was found from (#198, CONTEXT.md).
/// </remarks>
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

    /// <summary>What the small facts on a row are strung together on, wherever this module strings any together.</summary>
    private const string Joins = " · ";

    /// <summary>What a follow in place reads as on a row.</summary>
    private const string FollowingSaid = "following";

    /// <summary>And what one still waiting for a locked account to accept it reads as.</summary>
    private const string AskedSaid = "asked";

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
    ///     <para>
    ///         The four rows are <see cref="Block" />'s and are not built again here: this is the Account block plus
    ///         the bio, the custom fields and the standing (#198).
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
    /// <param name="picked">
    ///     The reference the reader has walked to inside the block, or <see langword="null" /> where none is — which
    ///     is every screen drawing an account that is not this one, and this one until <c>←</c> or <c>→</c> is pressed
    ///     (#179).
    /// </param>
    public static IReadOnlyList<Line> Header(
        Account account,
        IReadOnlyList<Account>? familiar,
        Drawing drawing,
        Reference? picked = null)
    {
        var width = drawing.Width;

        // No standing on row 4 here: this screen says it as a sentence of its own further down, where it has a row to
        // itself and room for all five facts.
        var lines = new List<Line>(Block(account, drawing));

        Section(lines, Bio(account, width, picked));
        Section(lines, account.Fields.SelectMany((_, which) => Field(account, which, width, picked)));

        Section(lines, [
            Standing(account, width),
            .. InCommon(familiar, width),
            .. Note(account.Standing?.Note, width),
        ]);

        return lines;
    }

    /// <summary>
    ///     The Account block: who somebody is, on four rows beside their avatar. What every screen that merely
    ///     <em>lists</em> an account draws for everyone on it — a search result, a follow request, a follow list and
    ///     Discover — and what <see cref="Header" /> opens with (#198).
    /// </summary>
    /// <remarks>
    ///     Row 1 is the display name, row 2 the handle, row 3 how much of a presence they have, and row 4 the month
    ///     they joined, the flags they carry and whatever the calling screen has to say about them, <c>·</c>-joined
    ///     and muted. The standing sits there rather than after the name because row 4 is already a list of small
    ///     facts about the person, which is what <c>following · follows you</c> is, and it leaves row 1 as the name
    ///     alone.
    ///     <para>
    ///         There is no per-screen variant and no width argument beyond the room in <paramref name="drawing" />:
    ///         the whole of what a listing screen may say about somebody is <paramref name="said" />, so a screen
    ///         cannot grow a fifth shape by drawing its own rows around this one.
    ///     </para>
    ///     <para>
    ///         Nothing inside it is walkable. Unlike the header block it carries no bio and no custom fields, so it
    ///         holds no <see cref="Reference" />s and <c>←</c>/<c>→</c> stay unconsumed on the screens that draw it.
    ///     </para>
    /// </remarks>
    /// <param name="account">
    ///     Them, as the instance last answered about them — every field this draws arriving on the full account
    ///     entity that every listing endpoint answers with, so the block costs no call of its own.
    /// </param>
    /// <param name="drawing">The room the block gets, the moment, and what this terminal can paint.</param>
    /// <param name="said">
    ///     What the calling screen has to add to row 4, <c>·</c>-joined already where it is more than one word, or
    ///     empty where the screen has nothing to say — which draws the joined month and the flags alone rather than
    ///     leaving a separator hanging off the end.
    /// </param>
    public static IReadOnlyList<Line> Block(Account account, Drawing drawing, string said = "")
    {
        var avatar = Avatar.Header(account.Address, account.AvatarUrl, drawing.Pictures);
        var room = Math.Max(0, drawing.Width - avatar.Width);

        // What the screen says goes on the end of the row rather than in it, so a flag stays a fact about the person
        // and whatever a screen adds stays a fact about the pair.
        var facts = string.Join(Joins, [Joined(account), .. Flags(account)]);

        return
        [
            .. avatar.Across(
                Line.Of(TextWrap.Clip(account.Author, room), Role.BylineName),
                Line.Of(TextWrap.Clip($"@{account.Address}", room), Role.BylineHandle),
                Presence(account, room),
                Line.Of(TextWrap.Clip(Facts(facts, said, room), room), Role.Muted)),
        ];
    }

    /// <summary>
    ///     Row 4 of an Account block: the small facts about the person, then what the screen drawing them has to add.
    /// </summary>
    /// <remarks>
    ///     What the screen says is measured out of the row first and the facts take what is left, because the facts
    ///     are the half a reader can still recognise clipped: a joined month cut short is still a joined month, and
    ///     <c>follo</c> is not a standing (#180). Where that leaves the facts no room at all the row is what the
    ///     screen said and nothing else, rather than a separator with nothing in front of it.
    /// </remarks>
    /// <param name="facts">The joined month and the flags, strung together already.</param>
    /// <param name="said">What the screen adds, or empty where it has nothing to say.</param>
    /// <param name="room">How much room the row gets.</param>
    private static string Facts(string facts, string said, int room)
    {
        if (said.Length == 0)
        {
            return facts;
        }

        var kept = TextWrap.Clip(facts, Math.Max(0, room - Glyphs.Columns(said) - Glyphs.Columns(Joins)));

        return kept.Length == 0 ? said : kept + Joins + said;
    }

    /// <summary>
    ///     What a follow reads as on a row: in place, asked for and still waiting, or nothing at all — including
    ///     where the instance was never asked, which is no tie rather than none in place.
    /// </summary>
    /// <remarks>
    ///     The two words are written here rather than at each screen that says one, for the reason the rest of this
    ///     module is here: a follow that reads one way on a follow list and another way on Discover would be two
    ///     ideas of the same thing (#181).
    /// </remarks>
    public static string Follow(AccountStanding? standing) => standing switch
    {
        { Following: true } => FollowingSaid,
        { FollowRequested: true } => AskedSaid,
        _ => string.Empty,
    };

    /// <summary>
    ///     Where the reader stands with them, in the fewest words that still tell the five apart — and nothing at all
    ///     where the instance was never asked, or where what it answered is what the list already said.
    /// </summary>
    /// <remarks>
    ///     The same five facts <see cref="Standing" /> says on an account's own screen, said shorter: that screen has
    ///     a row to itself and a sentence fits, and here it shares row 4 of an Account block with the joined month and
    ///     the flags.
    /// </remarks>
    /// <param name="standing">
    ///     Where the profile stands with them, or <see langword="null" /> where the instance was never asked — which
    ///     says nothing rather than saying there is no tie (CONTEXT.md).
    /// </param>
    /// <param name="said">
    ///     What the list this row is on has already said about everyone on it, which is what the row must not repeat.
    /// </param>
    public static string Compact(AccountStanding? standing, Implied said = Implied.Nothing)
    {
        if (standing is null)
        {
            return string.Empty;
        }

        var words = new List<string>();

        // A follow the list has already claimed of everyone on it is left unsaid; a follow still waiting to be let in
        // is not the same claim, so it survives.
        if (Follow(standing) is { Length: > 0 } follow
            && (follow != FollowingSaid || said != Implied.YouFollowThem))
        {
            words.Add(follow);
        }

        if (standing.FollowedBy && said != Implied.TheyFollowYou)
        {
            words.Add("follows you");
        }

        if (standing.Blocking)
        {
            words.Add("blocked");
        }

        if (standing.Muting)
        {
            words.Add("muted");
        }

        return string.Join(Joins, words);
    }

    /// <summary>How much of a presence they have: posts, and the two follow counts — row 3 of an Account block.</summary>
    private static Line Presence(Account account, int width) => Line.Of(
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
            : Line.Of(TextWrap.Clip(string.Join(Joins, said), width), Role.Muted);
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
    private static IEnumerable<Line> Bio(Account account, int width, Reference? picked)
    {
        var bio = account.Bio;

        if (string.IsNullOrWhiteSpace(bio))
        {
            return [];
        }

        var references = HeaderReferences.InBio(account);

        return TextWrap.Rows(bio, width).Select(row => new Line(BodyText.Spans(row, references, picked)));
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
    private static IEnumerable<Line> Field(Account account, int which, int width, Reference? picked)
    {
        var field = account.Fields[which];
        var label = HeaderReferences.Label(field);
        var said = label + field.Said + (field.Verified is null ? string.Empty : VerifiedMark);
        var references = HeaderReferences.InField(account, which);

        // Where this field's own text stands in the block's numbering, which is what its references are placed by — so
        // each row says where it falls in the block rather than in the field alone, and a pick is bracketed where it is.
        var place = HeaderReferences.At(account, which);

        var value = HeaderReferences.Value(account, which);

        return Hanging(said, width).Select((row, at) =>
        {
            var placed = row with { At = row.At + place };

            var line = new Line(MutedOutside(
                BodyText.Spans(placed, references, picked),
                placed.At,
                value,
                value + field.Said.Length));

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
            // The brackets a picked reference is drawn in are not written in the text at all, so they take no place in
            // it and keep the role of their own that says what they are (BodyText.Spans).
            if (span.Role == Role.ReferencePicked)
            {
                roled.Add(span);

                continue;
            }

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
