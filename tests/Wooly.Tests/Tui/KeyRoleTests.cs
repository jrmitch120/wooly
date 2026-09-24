using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A key you press is drawn in <see cref="Role.Key" />, in the three places one is drawn — the status row, the help
///     screen's key column, a confirmation's answer — and nowhere else: not the padding beside it, and not prose that
///     names it (#221, <c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     Asserted as role selection, which needs no terminal (ADR-0005, ADR-0014). Whether the two roles look different
///     is a theme's business, and a terminal drawing no colour tells them apart by position.
/// </remarks>
public class KeyRoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    private static IReadOnlyList<Line> Help() =>
        new HelpScreen(new FeedScreen(new Destination(DestinationKind.Home, "Home", Timeline.Home), [APost.With()]))
            .Lines(new Drawing(80, Now));

    /// <summary>The key is the thing you press and its explanation is not, so the two are drawn apart.</summary>
    [Fact]
    public void TheStatusRow_DrawsAKeyInKeyAndItsExplanationInMuted()
    {
        var row = ChromeLines.Status([new KeyHint("⏎", "read")], null, noticeIsError: false, asking: null, 80);

        Assert.Contains(row.Spans, span => span is { Role: Role.Key, Text: "⏎" });
        Assert.Contains(row.Spans, span => span is { Role: Role.Muted, Text: ":read" });
    }

    /// <summary>The separators between hints are furniture, and stay it.</summary>
    [Fact]
    public void TheStatusRow_KeepsItsSeparatorsInChrome()
    {
        var row = ChromeLines.Status(
            [new KeyHint("⏎", "read"), new KeyHint("a", "author")],
            null,
            noticeIsError: false,
            asking: null,
            80);

        Assert.All(
            row.Spans.Where(span => span.Text.Trim() is "·" or ""),
            span => Assert.Equal(Role.Chrome, span.Role));
    }

    /// <summary>The help screen draws a key in the role a key has, rather than borrowing a byline's blue.</summary>
    [Fact]
    public void TheHelpScreen_DrawsItsKeyColumnInKey()
    {
        var rows = Help().Where(line => line.Text.StartsWith("ctrl-q", StringComparison.Ordinal)).ToList();

        var row = Assert.Single(rows);
        Assert.Equal(new Span("ctrl-q", Role.Key), row.Spans[0]);
        Assert.DoesNotContain(Help().SelectMany(line => line.Spans), span => span.Role == Role.BylineHandle);
    }

    /// <summary>
    ///     The key, not its padding: a themer who gives <c>key</c> a background gets a highlighted <c>esc</c>, not a
    ///     sixteen-column band across every row.
    /// </summary>
    [Fact]
    public void TheHelpScreen_PadsAKeyInBodyRatherThanInKey()
    {
        foreach (var span in Help().SelectMany(line => line.Spans).Where(span => span.Role == Role.Key))
        {
            Assert.Equal(span.Text.Trim(), span.Text);
            Assert.NotEqual("", span.Text);
        }

        var row = Help().Single(line => line.Text.StartsWith("ctrl-q", StringComparison.Ordinal));
        Assert.Equal(Role.Body, row.Spans[1].Role);
        Assert.Equal("", row.Spans[1].Text.Trim());
        Assert.Equal(16, row.Spans[0].Width + row.Spans[1].Width);
    }

    /// <summary>A confirmation's two keys take <c>key</c>; the words beside them and the dot between them do not.</summary>
    [Fact]
    public void AConfirmation_DrawsItsTwoKeysInKey()
    {
        var row = ChromeLines.Status([], null, noticeIsError: false, new Confirmation("Delete this post?"), 80);

        var keyed = row.Spans.Where(span => span.Role == Role.Key).Select(span => span.Text).ToList();

        Assert.Equal(["y", "esc"], keyed);
        Assert.Contains(row.Spans, span => span is { Role: Role.Muted, Text: " delete" });
        Assert.Contains(row.Spans, span => span is { Role: Role.Muted, Text: " keep" });
        Assert.Contains(row.Spans, span => span is { Role: Role.Chrome, Text: " · " });
    }

    /// <summary>
    ///     Prose that names a key stays whole: lighting one word inside a sentence would teach that the shell's prose is
    ///     pressable.
    /// </summary>
    [Fact]
    public void ProseNamingAKey_CarriesNoKey()
    {
        var ballot = PostLines.Feed(
            APost.With(poll: APost.APoll()),
            new Drawing(61, Now),
            new Reading(Chosen: new HashSet<int> { 0 }));

        Assert.Contains(ballot, line => line.Text.Contains("v casts this vote", StringComparison.Ordinal));
        Assert.DoesNotContain(ballot.SelectMany(line => line.Spans), span => span.Role == Role.Key);
    }

    /// <summary>A themer writes it by the name the contract gives it.</summary>
    [Fact]
    public void TheRole_IsCalledKey()
    {
        Assert.Equal("key", RoleName.Of(Role.Key));
        Assert.Equal(Role.Key, RoleName.For("key"));
    }

    /// <summary>
    ///     Distinct from its gloss in both built-ins, and shared with no other role — the role exists because
    ///     <c>chrome</c> and <c>muted</c> were one hex.
    /// </summary>
    [Fact]
    public void BothBuiltIns_DrawAKeyApartFromEveryOtherRole()
    {
        foreach (var theme in new[] { Themes.Dark, Themes.Light })
        {
            var key = theme.For(Role.Key).Foreground;

            Assert.All(
                Enum.GetValues<Role>().Where(role => role != Role.Key),
                role => Assert.NotEqual(key, theme.For(role).Foreground));
        }
    }
}
