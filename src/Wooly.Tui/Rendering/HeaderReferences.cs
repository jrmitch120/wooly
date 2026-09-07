using Wooly.Core.Accounts;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What an account's header block carries that points somewhere else: the references written in their bio, then
///     those written in the value of each custom field, in the order the block draws them (#179, ADR-0019). The first
///     answer to <see cref="IReferring" /> that is not a post.
/// </summary>
/// <remarks>
///     Said once for the walk and for the drawing, which is the rule #145 settled about a post and holds here for the
///     same reason: a reference the arrows can reach and the brackets cannot mark is a pick nobody can see.
///     <see cref="Screens.AccountLines" /> draws each text with the references this places in it, so the two cannot
///     come to disagree about where one is.
///     <para>
///         Every text in the block is numbered as if the whole block were one, past the bio and past every field
///         above — the convention <see cref="Reference.At" /> already keeps for an attachment's own address, and for
///         the same reason: two references are told apart by what they say and where, so two fields both saying
///         <c>https://example.com</c> at the same offset in their own value would be one reference bracketed twice.
///     </para>
/// </remarks>
/// <param name="Account">Whose header block it is.</param>
public readonly record struct HeaderReferences(Account Account) : IReferring
{
    /// <summary>
    ///     What a custom field's value is drawn behind, and so what its references are moved along by — said here
    ///     rather than where the row is laid out, because where a reference <em>is</em> has to be one answer.
    /// </summary>
    public static string Label(AccountField field) => $"{field.Label}: ";

    /// <inheritdoc />
    /// <remarks>
    ///     A mention is drawn and never walked. <c>⏎</c> on one resolves off <c>Post.Mentions</c>, which the wire sends
    ///     with every post, and a bio carries no such list — so a bare <c>@maria</c> here would be resolved against the
    ///     reader's own instance and open whoever <em>their</em> server has by that name. A hashtag and an address need
    ///     no resolution at all, which is what makes a verified link on somebody's profile reachable: the most useful
    ///     thing on the screen, and one <c>→</c> away on arrival.
    /// </remarks>
    public IReadOnlyList<Reference> References =>
        [.. Whole(Account).Where(reference => reference.Role != Role.Mention)];

    /// <summary>The references written in <paramref name="account" />'s bio, which the block draws first.</summary>
    /// <remarks>
    ///     Every kind of them, mentions included: what is drawn is not what is walked here, and a mention is still a
    ///     mention to look at.
    /// </remarks>
    public static IReadOnlyList<Reference> InBio(Account account) => BodyText.References(account.Bio);

    /// <summary>
    ///     The references written in the value of <paramref name="account" />'s <paramref name="which" />th custom
    ///     field, placed where <see cref="At" /> says that field's text stands and moved along by its label.
    /// </summary>
    /// <remarks>
    ///     Found on the value alone and moved, rather than on the whole row: nothing in a label somebody chose to call
    ///     <c>https</c> is painted as a link.
    /// </remarks>
    public static IReadOnlyList<Reference> InField(Account account, int which)
    {
        var at = Value(account, which);

        return [.. BodyText
            .References(account.Fields[which].Said)
            .Select(reference => reference with { At = reference.At + at })];
    }

    /// <summary>
    ///     Where the <paramref name="which" />th field's <em>value</em> begins in that numbering: its own text, past
    ///     the label drawn in front of it.
    /// </summary>
    /// <remarks>
    ///     Beside <see cref="At" /> because the row is laid out from one and the references are placed by the other,
    ///     and the two being worked out separately is how a bracket comes to be drawn a label's width from the thing
    ///     it is bracketing.
    /// </remarks>
    public static int Value(Account account, int which) =>
        At(account, which) + Label(account.Fields[which]).Length;

    /// <summary>
    ///     Where the <paramref name="which" />th custom field's own text begins in the block's numbering: past the bio
    ///     and past every field drawn above it.
    /// </summary>
    /// <remarks>
    ///     A column of its own between each text and the next, so that a text ending where the following one begins
    ///     cannot put two references in the same place.
    /// </remarks>
    public static int At(Account account, int which)
    {
        var at = account.Bio.Length + 1;

        for (var above = 0; above < which; above++)
        {
            at += Label(account.Fields[above]).Length + account.Fields[above].Said.Length + 1;
        }

        return at;
    }

    /// <summary>Everything the block draws as a reference, in draw order — what is walked is a subset of it.</summary>
    private static IEnumerable<Reference> Whole(Account account)
    {
        foreach (var reference in InBio(account))
        {
            yield return reference;
        }

        for (var which = 0; which < account.Fields.Count; which++)
        {
            foreach (var reference in InField(account, which))
            {
                yield return reference;
            }
        }
    }
}
