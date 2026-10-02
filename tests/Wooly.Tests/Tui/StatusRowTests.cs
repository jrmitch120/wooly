using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The status row's one rule (#218, <c>docs/tui-shell.md</c>): as many whole hints as the terminal has room for,
///     in rank order, then <c>…+N</c> for the ones it could not fit, then <c>Keys: ?</c> — which is never cut. Each
///     hint is drawn <c>Does: key</c> and the pairs are divided by <c> | </c> (#268).
/// </summary>
/// <remarks>
///     All of it asserted against the spans, since the row is a list of spans computed from a width and nothing
///     else: no terminal is needed to know what it says.
/// </remarks>
public class StatusRowTests
{
    /// <summary>What a post screen hands the row: the thread walk, <c>g</c>, the way back out, the post keys.</summary>
    private static IReadOnlyList<KeyHint> Post { get; } =
        PostKeys.Around(new KeyHint("j/k", "thread"), [Screen.Refreshing], new KeyHint("esc", "back"));

    /// <summary>
    ///     What a feed hands it — the bottom of the stack, so <c>tab</c> and <c>`</c> rather than <c>esc</c>.
    /// </summary>
    private static IReadOnlyList<KeyHint> Feed { get; } =
        PostKeys.Around(new KeyHint("j/k", "post"), [Screen.Refreshing], PostKeys.AlongTheRail);

    /// <summary>The six states #169 measured, each as the keys <see cref="Screen.Keys" /> would hand the row.</summary>
    public static TheoryData<string> States => new(
        "post-poll",
        "post-reference",
        "post",
        "account",
        "notifications",
        "feed");

    private static IReadOnlyList<KeyHint> Of(string state) => state switch
    {
        "post-poll" => PostKeys.OnAPoll(Post),
        "post-reference" => PostKeys.OnAReference(Post),
        "post" => Post,
        "account" => PostKeys.Around(
            new KeyHint("j/k", "post"),
            [
                new KeyHint("[/]", "section"),
                new KeyHint("F", "follow"),
                new KeyHint("M", "mute"),
                new KeyHint("B", "block"),
                new KeyHint("w", "follows"),
                new KeyHint("s", "posts and replies"),
                Screen.Refreshing,
            ],
            new KeyHint("esc", "back")),
        "notifications" => PostKeys.Around(
            new KeyHint("j/k", "notification"),
            [new KeyHint("d", "dismiss", NeedsAPick: true), new KeyHint("D", "clear all", NeedsAPick: true), Screen.Refreshing],
            new KeyHint("tab", "destination")),
        "feed" => Feed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static Line Row(IReadOnlyList<KeyHint> keys, int width) =>
        ChromeLines.Status(keys, notice: null, noticeIsError: false, asking: null, width);

    /// <summary>What the row reads as, one entry per thing between its bars.</summary>
    private static IReadOnlyList<string> Said(Line row) => row.Text.Trim().Split(" | ");

    /// <summary>A hint as the row draws it, <c>Does: key</c>.</summary>
    private static string Drawn(KeyHint key) => string.Concat(key.Spans.Select(span => span.Text));

    /// <summary>The hints drawn, less the mark and <c>?</c>.</summary>
    private static IReadOnlyList<string> Hints(Line row) =>
        [.. Said(row).Where(said => !said.StartsWith('…') && said != Drawn(PostKeys.Asking))];

    /// <summary>Everything drawn, every hint whole and no mark: there was room, so there is nothing to count.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AtAWidthThatFitsEverything_EveryHintIsDrawnAndThereIsNoMark(string state)
    {
        var keys = Of(state);
        var row = Row(keys, 400);

        Assert.Equal(keys.Select(Drawn), Said(row));
        Assert.DoesNotContain("…", row.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     At 80 columns on each state: the first however many in rank order, the mark counting exactly the rest, and
    ///     <c>?</c> last — and the row within its width, ending in a whole hint rather than a clipped one.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AtEighty_TheHintsDrawnAreTheFirstInRankAndTheMarkCountsTheRest(string state)
    {
        var keys = Of(state);
        var ranked = keys.Where(key => key != PostKeys.Asking).Select(Drawn).ToList();
        var row = Row(keys, 80);
        var drawn = Hints(row);

        Assert.True(row.Width <= 80, row.Text);
        Assert.Equal(ranked.Take(drawn.Count), drawn);
        Assert.Equal([$"…+{ranked.Count - drawn.Count}", "Keys: ?"], Said(row).TakeLast(2));
        Assert.NotEmpty(drawn);
    }

    /// <summary>The rows the ticket drew, which are what a reader on those screens sees.</summary>
    [Theory]
    [InlineData(40, " Option: 1-0 | Vote: v | …+14 | Keys: ?")]
    [InlineData(61, " Option: 1-0 | Vote: v | Thread: j/k | …+13 | Keys: ?")]
    [InlineData(80, " Option: 1-0 | Vote: v | Thread: j/k | Refresh: g | Back: esc | …+11 | Keys: ?")]
    [InlineData(
        120,
        " Option: 1-0 | Vote: v | Thread: j/k | Refresh: g | Back: esc | Read: ⏎ | Reply: r | Boost: b | …+8 | Keys: ?")]
    public void AtEachWidth_APollOfYourOwnDegradesRatherThanTruncating(int width, string row) =>
        Assert.Equal(row, Row(Of("post-poll"), width).Text);

    /// <summary>
    ///     A hint is drawn <c>Does: key</c>, the words capitalised as a label, and the pairs divided by <c> | </c> —
    ///     lazygit's strip (#268). At 80 columns a feed reads its walk, <c>g</c>, its way out and <c>⏎</c>.
    /// </summary>
    [Fact]
    public void AtEighty_AFeedReadsDoesThenKeyDividedByBars() =>
        Assert.Equal(
            " Post: j/k | Refresh: g | Destination: tab | Group: ` | Read: ⏎ | …+10 | Keys: ?",
            Row(Feed, 80).Text);

    /// <summary>The words are capitalised as a label, and only their first letter: the key keeps its case.</summary>
    [Fact]
    public void AHint_CapitalisesItsWordsAndNotItsKey() =>
        Assert.Equal(
            " Posts and replies: s | Clear all: D",
            Row([new KeyHint("s", "posts and replies"), new KeyHint("D", "clear all")], 80).Text);

    /// <summary>The words are muted, the key is a key, and the bar between pairs is the frame's furniture.</summary>
    [Fact]
    public void AHint_DrawsItsWordsMutedItsKeyAsAKeyAndItsBarsAsChrome() =>
        Assert.Equal(
            [
                new Span(" ", Role.Chrome),
                new Span("Read: ", Role.Muted),
                new Span("⏎", Role.Key),
                new Span(" | ", Role.Chrome),
                new Span("Author: ", Role.Muted),
                new Span("a", Role.Key),
            ],
            Row([new KeyHint("⏎", "read"), new KeyHint("a", "author")], 80).Spans);

    /// <summary><c>?</c> is the one absolute, down to the floor where nothing but the mark stands beside it.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AtForty_AskingIsStillOnTheRow(string state) =>
        Assert.EndsWith(" | Keys: ?", Row(Of(state), 40).Text, StringComparison.Ordinal);

    /// <summary>The floor: sixteen hints cut, and <c>?</c> still standing in fifteen columns.</summary>
    [Fact]
    public void AtTheFloor_AskingIsStillOnTheRow()
    {
        KeyHint[] keys = [.. Enumerable.Range(0, 16).Select(at => new KeyHint($"{at}", "something")), PostKeys.Asking];

        Assert.Equal(" …+16 | Keys: ?", Row(keys, 15).Text);
    }

    /// <summary>
    ///     The fill stops at the first hint that does not fit: a narrow one behind it is not promoted, since the row
    ///     would stop reading as a ranking and the keys shown would reshuffle as the terminal was resized.
    /// </summary>
    [Fact]
    public void TheFill_StopsAtTheFirstHintThatDoesNotFit()
    {
        KeyHint[] keys =
        [
            new("a", "first"),
            new("b", "a very much wider second"),
            new("c", "c"),
            PostKeys.Asking,
        ];

        // Room for First: a and the narrow C: c, but not for b's words.
        var row = Row(keys, 32);

        Assert.Equal(" First: a | …+2 | Keys: ?", row.Text);
    }

    /// <summary>The mark is <c>muted</c>, and the row carries no role it did not already have.</summary>
    [Fact]
    public void TheMark_IsMutedAndAddsNoRole()
    {
        var row = Row(Of("account"), 80);

        Assert.Contains(row.Spans, span => span is { Role: Role.Muted } && span.Text.StartsWith('…'));
        Assert.All(row.Spans, span => Assert.Contains(span.Role, new[] { Role.Chrome, Role.Key, Role.Muted }));
    }

    /// <summary>Resizing narrower only ever takes hints off the tail of what is drawn: nothing reshuffles.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void Narrowing_OnlyTakesHintsOffTheTail(string state)
    {
        var keys = Of(state);

        var wide = Hints(Row(keys, 120));
        var middling = Hints(Row(keys, 80));
        var narrow = Hints(Row(keys, 61));

        Assert.Equal(wide.Take(middling.Count), middling);
        Assert.Equal(middling.Take(narrow.Count), narrow);
    }

    /// <summary>
    ///     A row whose screen does not answer to <c>?</c> right now — a prompt taking letters — is not handed one: the
    ///     pin keeps the key where the screen says it, and adds it nowhere.
    /// </summary>
    [Fact]
    public void AScreenNotAnsweringToAsking_IsNotGivenIt()
    {
        KeyHint[] keys = [new("⏎", "search"), new("tab", "destination")];

        Assert.Equal(" Search: ⏎ | Destination: tab", Row(keys, 80).Text);
    }

    /// <summary>
    ///     A notice and a confirmation are what they were: the row holds one of those <b>or</b> the keys, so neither
    ///     gains the mark nor the pinned <c>?</c>.
    /// </summary>
    [Fact]
    public void TheNoticeAndTheConfirmationRows_AreUnchanged()
    {
        var keys = Of("account");

        var notice = ChromeLines.Status(keys, "Only your own posts can be deleted.", noticeIsError: true, null, 80);
        var asking = ChromeLines.Status(
            keys,
            notice: null,
            noticeIsError: false,
            new Confirmation("Delete this post?", Pressing.Nothing),
            80);

        Assert.Equal(" Only your own posts can be deleted.", notice.Text);
        Assert.Equal(" Delete this post? This cannot be undone.  y delete · esc keep", asking.Text);
    }

    /// <summary>
    ///     The rank <see cref="PostKeys.Around(KeyHint, IReadOnlyList{KeyHint}, KeyHint[])" /> assembles: the walk,
    ///     the screen's own keys, the way out, the post keys worth a reminder, the tail, and <c>?</c>.
    /// </summary>
    [Fact]
    public void TheRank_IsWalkOwnWayOutRemindersTailThenAsking() =>
        Assert.Equal(
            "j/k:post g:refresh tab:destination `:group ⏎:read r:reply b:boost f:favorite a:author c:compose "
            + "↓/↑:row x:show warning p:pin e:edit d:delete ?:keys",
            string.Join(' ', Feed));
}
