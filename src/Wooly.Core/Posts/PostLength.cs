using System.Globalization;
using System.Text.RegularExpressions;

namespace Wooly.Core.Posts;

/// <summary>
///     How long a post is the way a Mastodon instance judges it (#319): by grapheme cluster, so that an emoji built of
///     several code points is one character; with any address counting as <see cref="AddressLength" /> however long it
///     is; and with a mention of somebody on another instance counting only its <c>@username</c> part.
/// </summary>
/// <remarks>
///     The instance counts the warning and the post together, so both are counted here — the warning letter for letter,
///     as the instance does, and only where it amounts to one (<see cref="ContentWarnings.Written" />), since one that
///     is all spaces is never sent.
///     <para>
///         The address and mention rules follow Mastodon's own (<c>StatusLengthValidator</c>), close enough to agree on
///         anything a reader is likely to write: an address is one of the schemes Mastodon links (<c>https://</c>,
///         <c>gemini://</c> and the rest), a host, and any path up to its last letter, digit or one of
///         <c>=_#/+&amp;-</c> — so the punctuation closing a sentence is the sentence's, and a comma after a host ends
///         the address. A mention is not preceded by a word character, so a mail address is counted whole. Where they
///         part, it is on the rarer things Mastodon's link rules know: a host without a known top-level domain, a
///         parenthesis balanced inside a path.
///     </para>
/// </remarks>
public static partial class PostLength
{
    /// <summary>How long a post may be: Mastodon's default. An instance can configure its own, which is not read.</summary>
    public const int Limit = 500;

    /// <summary>What any address counts as, the length of the shortened links Mastodon allows for.</summary>
    public const int AddressLength = 23;

    /// <summary>How long <paramref name="text" /> behind <paramref name="warning" /> is, as the instance counts it.</summary>
    public static int Of(string text, string? warning)
    {
        var counted = Shortened().Replace(
            text,
            match => match.Groups["address"].Success
                ? new string('x', AddressLength)
                : $"@{match.Groups["username"].Value}");

        return new StringInfo($"{ContentWarnings.Written(warning)}{counted}").LengthInTextElements;
    }

    [GeneratedRegex(
        """
        (?<address>(?<![a-z0-9@$\#])(?:https?|dat|dweb|ipfs|ipns|ssb|gopher|gemini)://
            [\w-]+(?:\.[\w-]+)*(?::\d+)?
            (?:[/?\#]\S*[\w=\#/+&-]|/)?)
        |(?<![=/\w])@(?<username>[a-z0-9_]+(?:[a-z0-9_.-]+[a-z0-9_]+)?)@[\w.-]+\w
        """,
        RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    private static partial Regex Shortened();
}
