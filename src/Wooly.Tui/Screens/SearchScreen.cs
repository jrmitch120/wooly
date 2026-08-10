using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    ///     What the search found, all three kinds as the one list the reader walks. Held this way so that the order
    ///     the results are drawn in and the order they are picked out in are the same order by construction, rather
    ///     than two counts kept in step by hand.
    /// </summary>
    private Picked<Result> _results = new([]);

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
            // Nothing to walk while the prompt is taking letters: what is on screen is the prompt and the sentence
            // under it, so the arrows are left unsaid rather than offered against three rows.
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
                    PostKeys.Scrolling,
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
    public int At => _results.At;

    /// <summary>How many results there are, of all three kinds together.</summary>
    public int Count => _results.Count;

    /// <summary>The accounts that were found.</summary>
    public IReadOnlyList<Account> Accounts => [.. _results.All.OfType<Result.OfAccount>().Select(one => one.Account)];

    /// <summary>The hashtags that were found.</summary>
    public IReadOnlyList<Hashtag> Hashtags => [.. _results.All.OfType<Result.OfHashtag>().Select(one => one.Hashtag)];

    /// <summary>The posts that were found.</summary>
    public IReadOnlyList<Post> Posts => [.. _results.All.OfType<Result.OfPost>().Select(one => one.Post)];

    /// <summary>The account picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Account? PickedAccount => _results.Out is Result.OfAccount(var account) ? account : null;

    /// <summary>The hashtag picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Hashtag? PickedHashtag => _results.Out is Result.OfHashtag(var hashtag) ? hashtag : null;

    /// <inheritdoc />
    /// <remarks>
    ///     The post picked out, so that reading, answering and marking one a search found mean what they mean on a
    ///     feed. An account or a hashtag is not a post, and picking one leaves this empty rather than guessing.
    /// </remarks>
    public override Post? Picked => _results.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    protected override IPicked Walking => _results;

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
    /// <remarks>
    ///     Accounts, then hashtags, then posts — the order <see cref="SearchResults" /> holds them in, and from here
    ///     the only order there is.
    /// </remarks>
    public void Found(string asked, SearchResults results)
    {
        Asked = asked;
        _typing = false;

        _results = new Picked<Result>([
            .. (results.Accounts ?? []).Select(Result (account) => new Result.OfAccount(account)),
            .. (results.Hashtags ?? []).Select(Result (hashtag) => new Result.OfHashtag(hashtag)),
            .. (results.Posts ?? []).Select(Result (post) => new Result.OfPost(post)),
        ]);
    }

    /// <inheritdoc />
    public override void Replace(Post post) => _results.Rewrite(found =>
        found is Result.OfPost(var held) ? new Result.OfPost(PostChange.Replaced(held, post)) : found);

    /// <inheritdoc />
    public override void Remove(string postId) =>
        _results.Remove(found => found is Result.OfPost(var held) && PostChange.Names(held, postId));

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
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

        for (var at = 0; at < _results.Count; at++)
        {
            lines.AddRange(Heading(at));
            lines.AddRange(_results.RowsOf(at, width, Draw));
            lines.Add(Line.Rule(width));
        }

        return lines;

        IReadOnlyList<Line> Draw(Result result, int at, int room) => result switch
        {
            Result.OfAccount(var account) => [AccountLines.Byline(account, room)],
            Result.OfHashtag(var hashtag) => [Tag(hashtag, room)],
            Result.OfPost(var post) => PostLines.Feed(post, room, Revealed.Has(post), now, pictures),
            _ => [],
        };
    }

    /// <summary>What a kind of result is called, which is what the heading over the run of them says.</summary>
    private static string Called(Result result) => result switch
    {
        Result.OfAccount => "── accounts ──",
        Result.OfHashtag => "── hashtags ──",
        _ => "── posts ──",
    };

    /// <summary>
    ///     The heading over the result at <paramref name="at" />, where it is the first of its kind — so a kind
    ///     nothing was found of gets no heading, rather than a heading over nothing.
    /// </summary>
    private IReadOnlyList<Line> Heading(int at)
    {
        var result = _results.All[at];

        // Asked of the kind rather than of what its heading says, so that two kinds could never come to share one
        // heading by being called the same thing.
        return at > 0 && _results.All[at - 1].GetType() == result.GetType()
            ? []
            : [Line.Of(Called(result), Role.Muted), Line.Blank];
    }

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
