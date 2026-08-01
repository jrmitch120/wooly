using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The prompt <c>/</c> opens, and what it found. A rail destination like any other, which is why <c>/</c> is a
///     frame key: it means "go to search" everywhere, and this is what it arrives at (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     One search, three kinds of result, listed in the order <see cref="SearchResults" /> holds them — which is what
///     makes a search for a word somebody half-remembers worth typing at all, since they rarely know in advance which
///     of the three it will turn out to be (ADR-0011).
///     <para>
///         What has been typed lives here rather than in a widget, for the reason ADR-0015 gives about the compose
///         editor: what is being searched for is then a fact about the shell — something a test can set and read —
///         rather than something only a terminal knows.
///     </para>
/// </remarks>
public sealed class SearchScreen : Screen
{
    /// <summary>
    ///     The posts a search turned up, held the way a feed holds its own so that marking one, revealing its warning
    ///     and taking it down after a delete mean here exactly what they mean on a timeline. Its own selection is
    ///     unused: what is picked out on this screen is one of three kinds, and <see cref="At" /> counts across them.
    /// </summary>
    private PickedPosts _posts = new([]);

    private IReadOnlyList<Account> _accounts = [];
    private IReadOnlyList<Hashtag> _hashtags = [];
    private bool _typing = true;

    /// <inheritdoc />
    public override string Crumb => Asked is { } asked ? $"search {asked}" : "search";

    /// <inheritdoc />
    /// <remarks>
    ///     While the prompt is taking letters it says only the keys that are not letters, because every other one goes
    ///     into the query rather than acting on anything — including <c>/</c> and <c>?</c>, which a web address and a
    ///     question are both entitled to contain.
    ///     <para>
    ///         No <c>esc</c> on either: search is a rail destination, so it is the bottom of the stack and there is
    ///         nothing under it to walk back to. <c>tab</c> is how you leave, the same as on every other destination.
    ///     </para>
    /// </remarks>
    public override IReadOnlyList<KeyHint> Keys
    {
        get
        {
            if (IsTyping)
            {
                return [new KeyHint("⏎", "search"), new KeyHint("tab", "destination")];
            }

            return Picked is not null
                ? PostKeys.Around(
                    new KeyHint("j/k", "result"),
                    [new KeyHint("/", "search again")],
                    new KeyHint("tab", "destination"))
                :
                [
                    new KeyHint("j/k", "result"),
                    new KeyHint("⏎", "open"),
                    new KeyHint("/", "search again"),
                    new KeyHint("tab", "destination"),
                    new KeyHint("?", "keys"),
                ];
        }
    }

    /// <inheritdoc />
    public override bool IsTyping => _typing;

    /// <summary>What has been typed so far, which is what <c>⏎</c> asks the instance for.</summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>What was last searched for, or <see langword="null" /> while nothing has been asked yet.</summary>
    public string? Asked { get; private set; }

    /// <summary>Which result is picked out, counted across all three kinds as one list.</summary>
    public int At { get; private set; }

    /// <summary>How many results there are, of all three kinds together.</summary>
    public int Count => _accounts.Count + _hashtags.Count + _posts.Count;

    /// <summary>The accounts that were found.</summary>
    public IReadOnlyList<Account> Accounts => _accounts;

    /// <summary>The hashtags that were found.</summary>
    public IReadOnlyList<Hashtag> Hashtags => _hashtags;

    /// <summary>The posts that were found.</summary>
    public IReadOnlyList<Post> Posts => _posts.Posts;

    /// <summary>The account picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Account? PickedAccount => At < FirstHashtag ? _accounts[At] : null;

    /// <summary>The hashtag picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Hashtag? PickedHashtag =>
        At >= FirstHashtag && At < FirstPost ? _hashtags[At - FirstHashtag] : null;

    /// <inheritdoc />
    /// <remarks>
    ///     The post picked out, so that reading, answering and marking one a search found mean what they mean on a
    ///     feed. An account or a hashtag is not a post, and picking one leaves this empty rather than guessing.
    /// </remarks>
    public override Post? Picked => At >= FirstPost && At < Count ? _posts.Posts[At - FirstPost] : null;

    /// <summary>
    ///     Where each kind starts in the one list the selection walks. Asked here rather than counted again at each
    ///     site, so that the order the results are drawn in and the order they are picked out in cannot drift apart.
    /// </summary>
    private int FirstHashtag => _accounts.Count;

    /// <inheritdoc cref="FirstHashtag" />
    private int FirstPost => _accounts.Count + _hashtags.Count;

    /// <summary>Puts a letter into the query.</summary>
    public void Type(char letter) => Query += letter;

    /// <summary>Takes the last letter back out of the query.</summary>
    public void Backspace()
    {
        if (Query.Length > 0)
        {
            Query = Query[..^1];
        }
    }

    /// <summary>
    ///     What the instance answered, which is also what stops the prompt taking letters: from here the keys act on
    ///     the results, and <c>/</c> starts a new search.
    /// </summary>
    public void Found(string asked, SearchResults results)
    {
        Asked = asked;
        _typing = false;
        At = 0;

        _accounts = results.Accounts ?? [];
        _hashtags = results.Hashtags ?? [];
        _posts = new PickedPosts(results.Posts ?? []);
    }

    /// <inheritdoc />
    public override void Move(int by)
    {
        if (Count > 0)
        {
            At = PickedPosts.Clamped(At, by, Count - 1);
        }
    }

    /// <inheritdoc />
    public override bool Reveal() => Picked is { } picked && _posts.Reveal(picked);

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Replace(post);

    /// <inheritdoc />
    public override void Remove(string postId)
    {
        _posts.Remove(postId);

        At = Count == 0 ? 0 : Math.Clamp(At, 0, Count - 1);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var lines = new List<Line> { Prompt(width), Line.Blank };

        if (IsTyping)
        {
            lines.Add(Line.Of(
                TextWrap.Clip("A word, a #hashtag, an @account, or the web address of one of them.", width),
                Role.Muted));

            return lines;
        }

        if (Count == 0)
        {
            lines.Add(Line.Of(TextWrap.Clip($"Nothing found for {Asked}.", width), Role.Muted));

            return lines;
        }

        var room = Math.Max(1, width - 1);

        lines.AddRange(Heading("── accounts ──", _accounts.Count));

        for (var at = 0; at < _accounts.Count; at++)
        {
            lines.Add(AccountLines.Byline(_accounts[at], room).After(PickedPosts.Gutter(at == At)));
            lines.Add(Line.Blank);
        }

        lines.AddRange(Heading("── hashtags ──", _hashtags.Count));

        for (var at = 0; at < _hashtags.Count; at++)
        {
            lines.Add(Tag(_hashtags[at], room).After(PickedPosts.Gutter(FirstHashtag + at == At)));
            lines.Add(Line.Blank);
        }

        lines.AddRange(Heading("── posts ──", _posts.Count));

        for (var at = 0; at < _posts.Count; at++)
        {
            var post = _posts.Posts[at];

            foreach (var line in PostLines.Feed(post, room, _posts.IsRevealed(post), now))
            {
                lines.Add(line.After(PickedPosts.Gutter(FirstPost + at == At)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>A kind nothing was found of gets no heading, rather than a heading over nothing.</summary>
    private static IReadOnlyList<Line> Heading(string said, int count) =>
        count == 0 ? [] : [Line.Of(said, Role.Muted), Line.Blank];

    /// <summary>A tag and how much use it has had lately, which is what makes one result worth reading over another.</summary>
    private static Line Tag(Hashtag hashtag, int width)
    {
        var name = TextWrap.Clip($"#{hashtag.Name}", width);
        var used = $"  {Number.Of(hashtag.RecentPosts)} posts · {Number.Of(hashtag.RecentAccounts)} accounts";

        return Line.Of([
            new Span(name, Role.BylineHandle),
            new Span(TextWrap.Clip(used, Math.Max(0, width - name.Length)), Role.Muted),
        ]);
    }

    /// <summary>
    ///     What is being searched for, with the caret where the next letter lands. The caret is a mark rather than a
    ///     colour, so a terminal with none still says where the typing is going.
    /// </summary>
    private Line Prompt(int width)
    {
        const string label = "Search: ";
        var typed = TextWrap.Clip(IsTyping ? Query : Asked ?? Query, Math.Max(0, width - label.Length - 1));

        // No caret once the prompt has stopped taking letters, and no empty span standing in for one: a row a reader
        // is scanning should say what is there and nothing else.
        return IsTyping
            ? Line.Of([new Span(label, Role.Muted), new Span(typed, Role.Body), new Span("▌", Role.Selection)])
            : Line.Of([new Span(label, Role.Muted), new Span(typed, Role.Body)]);
    }
}
