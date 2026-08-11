using System.Text.RegularExpressions;
using Wooly.Core.Timelines;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The references inside a post's text — a hashtag, an account somebody named, an address — and the plain text
///     between them (#46, #83).
/// </summary>
/// <remarks>
///     Found on the flattened plain text rather than on the HTML it arrived as: <c>PostContent</c> discards the
///     instance's own <c>class="mention"</c> on the way through, and keeping that structure would change
///     <c>Post.Content</c>, which the CLI prints too. <c>#tag</c> and <c>@user@instance</c> are unambiguous in plain
///     text; an address is matched by pattern, so a bare domain somebody typed as prose is painted as a link. That is
///     the imprecision this takes on purpose, and all three roles carry themselves without colour anyway — the
///     <c>#</c>, the <c>@</c>, the scheme — so nothing is lost where the theme is not.
///     <para>
///         Matched once on the whole post, before the wrap rather than after it (#83). A row at a time was cheaper and
///         had no state running from one row to the next, but it could say nothing about a reference's place among the
///         others — which is what <c>←</c> and <c>→</c> walk — and an address longer than the row was cut into two
///         halves, each of which read as prose. <see cref="Spans" /> is still handed one row at a time; what changed
///         is that it is handed the post's references with it, and takes the ones written on that row.
///     </para>
/// </remarks>
public static partial class BodyText
{
    /// <summary>What a sentence puts after an address, which is not part of it.</summary>
    private const string SentenceTail = ".,;:!?)]}>\"'…";

    /// <summary>What a sentence puts after a handle. A dot is in a handle as readily as after one.</summary>
    private const string HandleTail = ".-";

    /// <summary>
    ///     The brackets a picked reference is drawn in — always drawn, in colour and no-colour terminals alike, which
    ///     is the whole of why they are brackets rather than a colour (<c>docs/tui-shell.md</c>).
    /// </summary>
    private const string Opening = "‹";

    /// <inheritdoc cref="Opening" />
    private const string Closing = "›";

    /// <summary>
    ///     The references inside <paramref name="text" />, left to right — the order they are walked in, and the order
    ///     an index into them means anything in.
    /// </summary>
    public static IReadOnlyList<Reference> References(string text)
    {
        var references = new List<Reference>();

        foreach (Match match in Referenced().Matches(text))
        {
            if (Found(match) is { Text.Length: > 0 } reference)
            {
                references.Add(reference);
            }
        }

        return references;
    }

    /// <summary>
    ///     The runs across <paramref name="row" />, left to right, with what each one is — the plain text, and
    ///     whichever of <paramref name="references" /> are written on this row.
    /// </summary>
    /// <remarks>
    ///     A reference cut across two rows takes its role on both, since the two halves are one thing that happens to
    ///     be written in two places. The brackets are the exception: <see cref="Opening" /> is drawn where the picked
    ///     reference starts and <see cref="Closing" /> where it stops, so a reference cut in two is opened on the row
    ///     it starts on and closed on the row it ends on rather than bracketed twice.
    /// </remarks>
    /// <param name="row">One wrapped row, and where in the text it came out of it starts.</param>
    /// <param name="references">The references in that whole text, as <see cref="References" /> found them.</param>
    /// <param name="picked">
    ///     The one the reader has walked to, or <see langword="null" /> where none is picked — which is every post but
    ///     the one being read, and every screen where <c>←</c> and <c>→</c> have not been pressed.
    /// </param>
    public static IReadOnlyList<Span> Spans(
        TextWrap.Row row,
        IReadOnlyList<Reference> references,
        Reference? picked = null)
    {
        var spans = new List<Span>();
        var text = row.Text;
        var at = 0;

        foreach (var reference in references)
        {
            // Where the reference falls on this row, in the row's own columns — which is its offset into the text and
            // the row's, differing by nothing else (TextWrap.Row).
            var from = reference.At - row.At;
            var to = reference.End - row.At;

            if (to <= 0 || from >= text.Length)
            {
                continue;
            }

            var starts = Math.Max(from, 0);
            var stops = Math.Min(to, text.Length);

            // Anything before this reference, and anything a reference before it left behind — a full stop after an
            // address is written by whoever ended the sentence, not by whoever wrote the address.
            if (starts > at)
            {
                spans.Add(new Span(text[at..starts], Role.Body));
            }

            var bracketed = picked == reference;

            if (bracketed && from >= 0)
            {
                spans.Add(new Span(Opening, Role.ReferencePicked));
            }

            spans.Add(new Span(text[starts..stops], reference.Role));

            if (bracketed && to <= text.Length)
            {
                spans.Add(new Span(Closing, Role.ReferencePicked));
            }

            at = stops;
        }

        if (at < text.Length || spans.Count == 0)
        {
            // A row with nothing on it is still a row of the screen, so it goes back as one empty run rather than as
            // no runs at all.
            spans.Add(new Span(text[at..], Role.Body));
        }

        return spans;
    }

    /// <summary>
    ///     What <paramref name="match" /> is, how much of it, and where — or nothing, where it is not what it looked
    ///     like.
    /// </summary>
    private static Reference? Found(Match match)
    {
        if (match.Groups["link"].Success)
        {
            return new Reference(match.Value.TrimEnd(SentenceTail.ToCharArray()), Role.Link, match.Index);
        }

        if (match.Groups["mention"].Success)
        {
            return new Reference(match.Value.TrimEnd(HandleTail.ToCharArray()), Role.Mention, match.Index);
        }

        // Held to the same one-word rule the rail's tag setting and the tag command are held to, so that a word this
        // client would not fetch a timeline for is not painted as one either.
        return Hashtag.IsWellFormed(match.Value) ? new Reference(match.Value, Role.Hashtag, match.Index) : null;
    }

    /// <summary>
    ///     An address, an account named, or a tag — in that order, so that a <c>#themes</c> at the end of an address is
    ///     part of the address rather than a tag inside a link.
    /// </summary>
    /// <remarks>
    ///     An address is a scheme, a <c>www.</c>, or a domain with a path on it. The last of those is the elided form
    ///     an instance serves and the reason for matching at all; a domain with nothing after it is left as prose,
    ///     which is what keeps <c>Node.js</c> and <c>config.toml</c> from being painted as addresses.
    ///     <para>
    ///         A mention is not preceded by a word character, so the second half of a mail address is somebody's mail
    ///         rather than somebody being named.
    ///     </para>
    /// </remarks>
    [GeneratedRegex(
        """
        (?<link>(?:https?://|www\.)\S+|[\w-]+(?:\.[\w-]+)+/\S*)
        |(?<mention>(?<![\w@])@[\w.-]+(?:@[\w.-]+)?)
        |(?<hashtag>(?<![\w#])\#\w+)
        """,
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Referenced();
}
