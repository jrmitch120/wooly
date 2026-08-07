using System.Text.RegularExpressions;
using Wooly.Core.Timelines;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The three things inside a post's text that are something in particular — a hashtag, an account somebody named,
///     an address — and the plain text between them (#46).
/// </summary>
/// <remarks>
///     Found on the flattened plain text rather than on the HTML it arrived as: <c>PostContent</c> discards the
///     instance's own <c>class="mention"</c> on the way through, and keeping that structure would change
///     <c>Post.Content</c>, which the CLI prints too. <c>#tag</c> and <c>@user@instance</c> are unambiguous in plain
///     text; an address is matched by pattern, so a bare domain somebody typed as prose is painted as a link. That is
///     the imprecision this takes on purpose, and all three roles carry themselves without colour anyway — the
///     <c>#</c>, the <c>@</c>, the scheme — so nothing is lost where the theme is not.
///     <para>
///         A row at a time, after the wrap rather than before it, so there is no state running from one row to the
///         next: a row is spanned on what is written on it. An address long enough to be broken across two rows is
///         painted on whichever halves still read as one.
///     </para>
/// </remarks>
public static partial class BodyText
{
    /// <summary>What a sentence puts after an address, which is not part of it.</summary>
    private const string SentenceTail = ".,;:!?)]}>\"'…";

    /// <summary>What a sentence puts after a handle. A dot is in a handle as readily as after one.</summary>
    private const string HandleTail = ".-";

    /// <summary>The runs across <paramref name="row" />, left to right, with what each one is.</summary>
    public static IReadOnlyList<Span> Spans(string row)
    {
        var spans = new List<Span>();
        var at = 0;

        foreach (Match match in Marks().Matches(row))
        {
            if (Mark(match) is not { Text.Length: > 0 } mark)
            {
                continue;
            }

            // Anything before this mark, and anything a mark before it left behind — a full stop after an address is
            // written by whoever ended the sentence, not by whoever wrote the address.
            if (match.Index > at)
            {
                spans.Add(new Span(row[at..match.Index], Role.Body));
            }

            spans.Add(mark);

            at = match.Index + mark.Text.Length;
        }

        if (at < row.Length || spans.Count == 0)
        {
            // A row with nothing on it is still a row of the screen, so it goes back as one empty run rather than as
            // no runs at all.
            spans.Add(new Span(row[at..], Role.Body));
        }

        return spans;
    }

    /// <summary>What <paramref name="match" /> is, and how much of it — or nothing, where it is not what it looked like.</summary>
    private static Span? Mark(Match match)
    {
        if (match.Groups["link"].Success)
        {
            return new Span(match.Value.TrimEnd(SentenceTail.ToCharArray()), Role.Link);
        }

        if (match.Groups["mention"].Success)
        {
            return new Span(match.Value.TrimEnd(HandleTail.ToCharArray()), Role.Mention);
        }

        // Held to the same one-word rule the rail's tag setting and the tag command are held to, so that a word this
        // client would not fetch a timeline for is not painted as one either.
        return Hashtag.IsWellFormed(match.Value) ? new Span(match.Value, Role.Hashtag) : null;
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
    private static partial Regex Marks();
}
