using Wooly.Core.Posts;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What a post is showing this reader: whether its author's own words are on screen, whether what hangs off them
///     is, and whether there is anything left for <c>x</c> to ask past. The one place either question is answered
///     (#145).
/// </summary>
/// <remarks>
///     One module because it is one rule read in two places. <see cref="Screens.Screen" /> settles what can be walked
///     and what keys are offered; <see cref="Screens.PostLines" /> settles what is drawn — and until #145 each worked
///     the same two facts out for itself, at eight sites between them with no way to check each other. The failure that
///     let in is silent and one-sided: a walk more permissive than the drawing steps <c>←</c>/<c>→</c> onto a reference
///     inside something the reader was never shown and opens it with <c>⏎</c>, which is the one thing a warning exists
///     to prevent. It is also the codebase's highest-churn rule — #113, #116, #119, #120 and #121 each had to edit
///     several of those sites, and each time the question put was which of them this one was rather than what the rule
///     is.
///     <para>
///         Two answers rather than one, because the two halves of <see cref="Post.IsWarned" /> hide different things:
///         the words stand behind <see cref="Post.ContentWarning" /> alone, being what an author writes a warning
///         about, and everything hanging off them behind either half, since the instance's flag is a mark over media
///         and hides them with no text to read it off (#113, #116, #119).
///     </para>
///     <para>
///         The boost unwrap lives in here too. A warning belongs to the post inside a boost, the way every mark does,
///         and a caller asking the wrapper would find no warning on a boost of a warned post.
///     </para>
///     <para>
///         The TUI's, and only the TUI's. The CLI does not participate in any of this by decision: it links
///         everything, hides nothing, and never asks <see cref="Post.IsWarned" /> at all — there is nothing rendered
///         there for a warning to be about and no key to ask past one with (#113, #117, #122).
///     </para>
/// </remarks>
/// <param name="Shown">
///     The post these answers are about: the boosted post where this is a boost, and the post itself where it is not.
///     Handed back rather than unwrapped again by the caller, since whatever puts this question goes straight on to
///     read that post's text, its attachments or its poll — and the wrapper carries none of them.
/// </param>
/// <param name="Words">
///     Whether what the author wrote is on screen rather than behind the content warning: the post's text, and the
///     answers of its poll, which are words the same author typed (#119).
/// </param>
/// <param name="Media">
///     Whether what hangs off the post is on screen: its attachments and its link preview, which stand behind either
///     half of the warning (#113, #116).
/// </param>
public readonly record struct OnShow(Post Shown, bool Words, bool Media)
{
    /// <summary>
    ///     Whether there is anything for <c>x</c> to show — whether either half is being held back. What
    ///     <see cref="Screens.Revealed.Ask" /> answers to, and so what settles whether the key was used at all.
    /// </summary>
    /// <remarks>
    ///     Both halves named though the first already implies the second: a warning its author wrote hides the media
    ///     with the words, so <c>!Words</c> never stands on its own. Said as "anything hidden" anyway, because that is
    ///     the rule the key is offered by rather than a coincidence of which half is wider.
    /// </remarks>
    public bool Asks => !Words || !Media;

    /// <summary>
    ///     What <paramref name="post" /> is showing a reader who has done <paramref name="reading" /> to it — which is
    ///     what the drawing side holds, one per post on the screen (#95).
    /// </summary>
    public static OnShow Of(Post post, Reading reading) => Of(post, reading.Revealed);

    /// <summary>
    ///     The same question asked with nothing but the one fact it turns on — whether the reader has asked past the
    ///     warning. What the walking side puts, where a <see cref="Reading" /> is assembled to draw a post rather than
    ///     to decide what there is of it to draw.
    /// </summary>
    public static OnShow Of(Post post, bool revealed)
    {
        var shown = post.Boosted ?? post;

        return new OnShow(shown, shown.ContentWarning is null || revealed, !shown.IsWarned || revealed);
    }
}
