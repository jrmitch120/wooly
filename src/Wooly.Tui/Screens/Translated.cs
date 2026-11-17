using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     The posts a reader has asked to see in their instance's <b>Translation</b>, by the id of each, with the
///     translation each is showing in. What <c>t</c> writes to and what drawing a post reads (#615).
/// </summary>
/// <remarks>
///     The counterpart of <see cref="Revealed" />, with its lifetime and for its reason: one per <see cref="Screen" />,
///     so what shows translated belongs to the screen it was asked on and lasts as long as that screen is on the stack
///     — ended by <c>esc</c>, a refresh and an arrival, and asked again on a post drilled into (ADR-0037). What the
///     instance answered is held for the session elsewhere, by the shell, so asking again costs no call; this is only
///     which posts are showing it here. A boost is asked about by the post inside it, whose words they are.
/// </remarks>
public sealed class Translated
{
    private readonly Dictionary<string, Translation> _showing = [];

    /// <summary>
    ///     Whether <paramref name="post" /> is one its instance could translate: public or unlisted, since an instance
    ///     refuses a post only followers or the people it mentions can see, and with words — text, a warning, a poll's
    ///     answer or an attachment's description — since a translation of nothing is nothing (ADR-0037).
    /// </summary>
    /// <remarks>
    ///     Asked of the post inside a boost. The instance's languages map narrows this further, and is not asked here
    ///     yet (#616).
    /// </remarks>
    public static bool Offers(Post post)
    {
        var shown = post.Boosted ?? post;

        return shown.Visibility is PostVisibility.Public or PostVisibility.Unlisted && HasWords(shown);
    }

    /// <summary>The translation <paramref name="post" /> is showing in here, or <see langword="null" /> where none.</summary>
    public Translation? Of(Post post) => _showing.GetValueOrDefault((post.Boosted ?? post).Id);

    /// <summary>Shows <paramref name="post" /> in <paramref name="translation" /> from here on.</summary>
    public void Show(Post post, Translation translation) => _showing[(post.Boosted ?? post).Id] = translation;

    /// <summary>Shows the post <paramref name="id" /> names as written again, whether or not it was translated.</summary>
    public void Drop(string id) => _showing.Remove(id);

    /// <summary>
    ///     <paramref name="post" /> as it is drawn here: in the translation it is showing in, or as written where there
    ///     is none — what is walked inside it as well as what is drawn, so the two cannot differ.
    /// </summary>
    public Post Over(Post post) => Of(post) is { } translation ? translation.Over(post) : post;

    private static bool HasWords(Post post) =>
        !string.IsNullOrWhiteSpace(post.Content)
        || !string.IsNullOrWhiteSpace(post.ContentWarning)
        || post.Poll?.Options.Any(option => !string.IsNullOrWhiteSpace(option.Text)) == true
        || post.Media.Any(attached => !string.IsNullOrWhiteSpace(attached.Description));
}
