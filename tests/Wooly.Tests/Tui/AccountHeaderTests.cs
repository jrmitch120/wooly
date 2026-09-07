using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What an account screen says about a person before it says what they have posted: their name and handle beside
///     their avatar, the presence line, the day they joined and the flags; then the bio and the custom fields they
///     wrote; then what they are to the reader (#178, ADR-0019).
/// </summary>
/// <remarks>
///     Held at line level with no terminal in the room, which is what lets a test say which role a <c>✓</c> takes and
///     how many columns an avatar gives back — neither of which a screenshot could be asked.
/// </remarks>
public class AccountHeaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>The header block's rows, drawn as a screen with no terminal draws them.</summary>
    private static IReadOnlyList<Line> Header(
        Account account,
        IReadOnlyList<Account>? familiar = null,
        int width = 61,
        IPictures? pictures = null) =>
        AccountLines.Header(account, familiar, new Drawing(width, Now, pictures));

    /// <summary>An account that wrote nothing about itself and stands in no relation the instance was asked about.</summary>
    private static Account Bare() => AnAccount.With(bio: string.Empty);

    /// <summary>Which row the first one <paramref name="which" /> picks out is, so a test can say "the row under it".</summary>
    private static int Row(IReadOnlyList<Line> lines, Func<Line, bool> which)
    {
        for (var at = 0; at < lines.Count; at++)
        {
            if (which(lines[at]))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Who they are, on four rows in that order: the name, the handle, how much of a presence they have, and the
    ///     day they joined. The order is deliberate — who they are, then what they wrote, then what they are to you.
    /// </summary>
    [Fact]
    public void Header_OpensWithTheNameTheHandleThePresenceAndTheDayTheyJoined()
    {
        var lines = Header(AnAccount.With(
            address: "maria@example.social",
            author: "Maria Example",
            posts: 1204,
            following: 340,
            followers: 892,
            joined: new DateOnly(2023, 3, 14)));

        Assert.Equal("Maria Example", lines[0].Text);
        Assert.Equal(Role.BylineName, lines[0].Role);

        Assert.Equal("@maria@example.social", lines[1].Text);
        Assert.Equal(Role.BylineHandle, lines[1].Role);

        Assert.Equal("1,204 posts · 340 following · 892 followers", lines[2].Text);
        Assert.Equal("Joined Mar 2023", lines[3].Text);
        Assert.Equal(Role.Muted, lines[3].Role);
    }

    /// <summary>
    ///     Every section brings its own separator, so an account with no bio, no fields, nobody in common and no note
    ///     collapses to the four rows and its standing with no gaps left behind — and, in particular, no trailing one.
    /// </summary>
    [Fact]
    public void Header_CollapsesToTheFourRowsAndItsStanding_WhereThereIsNothingElseToSay()
    {
        var lines = Header(Bare());

        Assert.Equal(6, lines.Count);
        Assert.Empty(lines[4].Spans);
        Assert.Equal("Standing not asked for.", lines[5].Text);
        Assert.NotEmpty(lines[^1].Spans);
    }

    /// <summary>
    ///     A bio, its own paragraphs kept, wrapped at the width rather than cut: the screen scrolls and one <c>j</c>
    ///     reaches the posts, where a truncated bio would be permanent damage.
    /// </summary>
    [Fact]
    public void Header_WrapsABioWholeRatherThanClippingIt()
    {
        const string bio = "Illustrator, cat photographer, occasional #linocut poster and a writer of "
            + "sentences that run on for rather longer than sixty-one columns allow.\nCommissions open.";

        var lines = Header(AnAccount.With(bio: bio));
        var wrote = lines.Skip(5).TakeWhile(line => line.Spans.Count > 0).ToList();

        Assert.Empty(lines[4].Spans);
        Assert.True(wrote.Count > 2, "a bio this long wraps to more than the two paragraphs it was written in");
        Assert.All(wrote, line => Assert.True(line.Width <= 61));
        Assert.DoesNotContain("…", string.Concat(wrote.Select(line => line.Text)));
        Assert.Equal("Commissions open.", wrote[^1].Text);
    }

    /// <summary>A hashtag inside a bio is a reference like any inside a post, and is drawn as one.</summary>
    [Fact]
    public void Header_DrawsAReferenceInABioInItsOwnRole()
    {
        var lines = Header(AnAccount.With(bio: "Occasional #linocut poster, at https://maria.example — say hello."));

        Assert.Contains(lines, line => line.Spans.Any(span => span is { Role: Role.Hashtag, Text: "#linocut" }));
        Assert.Contains(
            lines,
            line => line.Spans.Any(span => span is { Role: Role.Link, Text: "https://maria.example" }));
    }

    /// <summary>
    ///     A custom field is <c>Label: value</c>, the label muted and the value drawn as what it is — and a verified
    ///     one takes a <c>✓</c> in <see cref="Role.Muted" />, since Mastodon only ever verifies a link and a colour
    ///     there would say what the glyph has already said.
    /// </summary>
    [Fact]
    public void Header_MarksAVerifiedFieldWithATickAndNoRoleOfItsOwn()
    {
        var lines = Header(AnAccount.With(
            bio: string.Empty,
            fields:
            [
                AnAccount.Field("Website", "https://maria.example", new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)),
                AnAccount.Field("Pronouns", "she/her"),
            ]));

        var website = lines.First(line => line.Text.StartsWith("Website"));
        var pronouns = lines.First(line => line.Text.StartsWith("Pronouns"));

        Assert.Equal("Website: https://maria.example ✓", website.Text);
        Assert.Contains(website.Spans, span => span is { Role: Role.Muted, Text: "Website: " });
        Assert.Contains(website.Spans, span => span is { Role: Role.Link, Text: "https://maria.example" });
        Assert.Contains(website.Spans, span => span is { Role: Role.Muted, Text: " ✓" });

        Assert.Equal("Pronouns: she/her", pronouns.Text);
        Assert.Contains(pronouns.Spans, span => span is { Role: Role.Body, Text: "she/her" });
        Assert.DoesNotContain("✓", pronouns.Text);
    }

    /// <summary>
    ///     A field too long for the row wraps, its continuation stepped in two columns while the first row keeps the
    ///     whole width, and an address cut across the break stays one reference rather than two halves of prose.
    /// </summary>
    [Fact]
    public void Header_IndentsAFieldsContinuationAndKeepsAWrappedAddressOneReference()
    {
        var address = "https://maria.example/a-portfolio-page-with-a-long-name/and-another-segment";

        var lines = Header(AnAccount.With(bio: string.Empty, fields: [AnAccount.Field("Website", address)]));

        var at = Row(lines, line => line.Text.StartsWith("Website"));
        var wrote = lines.Skip(at).TakeWhile(line => line.Spans.Count > 0).ToList();

        Assert.True(wrote.Count > 2, "an address this long takes the field past two rows");
        Assert.All(wrote.Skip(1), line => Assert.StartsWith("  ", line.Text));
        Assert.All(wrote.Skip(1), line => Assert.Contains(line.Spans, span => span.Role == Role.Link));

        // One reference across every row it is written on, rather than two halves of prose either side of the break.
        Assert.Equal(
            address,
            string.Concat(wrote.SelectMany(line => line.Spans).Where(span => span.Role == Role.Link).Select(span => span.Text)));
    }

    /// <summary>
    ///     Only the continuation is stepped in, so a field that fits the whole width is one row — the two columns are
    ///     the price of a second row rather than a toll on every field.
    /// </summary>
    [Fact]
    public void Header_KeepsAFieldOnOneRowWhereItFillsTheWholeWidth()
    {
        var value = new string('v', 61 - "Website: ".Length);

        var lines = Header(AnAccount.With(bio: string.Empty, fields: [AnAccount.Field("Website", value)]));
        var at = Row(lines, line => line.Text.StartsWith("Website"));

        Assert.Equal(61, lines[at].Width);
        Assert.Empty(lines[at + 1].Spans);
    }

    /// <summary>The two flags, worded and only where they are true — an unlocked human says nothing extra.</summary>
    [Theory]
    [InlineData(false, false, "Joined Jan 2020")]
    [InlineData(true, false, "Joined Jan 2020 · ⚿ locked")]
    [InlineData(false, true, "Joined Jan 2020 · ⚙ bot")]
    [InlineData(true, true, "Joined Jan 2020 · ⚙ bot · ⚿ locked")]
    public void Header_SaysAFlagOnlyWhereItIsTrue(bool locked, bool bot, string said)
    {
        Assert.Equal(said, Header(AnAccount.With(isLocked: locked, isBot: bot))[3].Text);
    }

    /// <summary>
    ///     No astral glyph anywhere: 🤖 and 🔒 are surrogate pairs, <see cref="TextWrap.Clip" /> cuts by
    ///     <see cref="char" />, and a clip landing mid-pair would draw a broken glyph.
    /// </summary>
    [Fact]
    public void Header_EmitsNoAstralGlyph()
    {
        var lines = Header(
            AnAccount.With(
                isLocked: true,
                isBot: true,
                bio: "Every glyph here is one column wide.",
                fields: [AnAccount.Field("Website", "https://maria.example", Now)],
                standing: AnAccount.Standing(following: true, note: "met at the 2024 meetup")),
            familiar: [AnAccount.With(address: "jon@example.social")]);

        Assert.All(lines, line => Assert.DoesNotContain(line.Text, char.IsSurrogate));
    }

    /// <summary>
    ///     Two handles then a count, on one row: handles rather than display names, because a display name is not
    ///     unique and this row is making an identity claim.
    /// </summary>
    [Theory]
    [InlineData(1, "Followed by @a@example.social")]
    [InlineData(2, "Followed by @a@example.social and @b@example.social")]
    [InlineData(3, "Followed by @a@example.social, @b@example.social and 1 other")]
    [InlineData(5, "Followed by @a@example.social, @b@example.social and 3 others")]
    public void Header_NamesTwoOfTheAccountsInCommonAndCountsTheRest(int howMany, string said)
    {
        var familiar = Enumerable
            .Range(0, howMany)
            .Select(at => AnAccount.With(address: $"{(char)('a' + at)}@example.social"))
            .ToList();

        Assert.Contains(Header(Bare(), familiar), line => line.Text == said);
    }

    /// <summary>
    ///     Nobody in common and nobody asked both draw nothing, and the two are told apart by what the screen was
    ///     handed rather than by what it drew — which is the whole reason the list is nullable.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Header_DrawsNoRowForNobodyInCommonAndNoneForNobodyAsked(bool asked)
    {
        var lines = Header(Bare(), asked ? [] : null);

        Assert.DoesNotContain(lines, line => line.Text.StartsWith("Followed by"));
        Assert.Equal(6, lines.Count);
    }

    /// <summary>
    ///     The reader's own words about somebody, wrapped whole rather than cut: cutting it would be cutting their
    ///     writing, and it rides on the standing at no call of its own.
    /// </summary>
    [Fact]
    public void Header_WrapsTheReadersOwnNoteWholeRatherThanClippingIt()
    {
        const string note = "met at the 2024 meetup, in the queue for coffee, and promised to send them "
            + "the name of that linocut supplier in Lisbon";

        var lines = Header(Bare() with { Standing = AnAccount.Standing(following: true, note: note) });
        var wrote = lines.SkipWhile(line => !line.Text.StartsWith("Your note:")).ToList();

        Assert.True(wrote.Count > 1, "a note this long takes more than one row");
        Assert.All(wrote, line => Assert.Equal(Role.Muted, line.Role));
        Assert.DoesNotContain("…", string.Concat(wrote.Select(line => line.Text)));
        Assert.EndsWith("in Lisbon", wrote[^1].Text);
    }

    /// <summary>
    ///     What they are to the reader sits closest to the posts, in one section: the standing, who they have in
    ///     common, and the reader's own note.
    /// </summary>
    [Fact]
    public void Header_PutsWhatTheyAreToTheReaderLastAndInOneSection()
    {
        var lines = Header(
            Bare() with { Standing = AnAccount.Standing(following: true, followedBy: true, note: "met at a meetup") },
            [AnAccount.With(address: "jon@example.social")]);

        Assert.Equal("you follow them · they follow you", lines[^3].Text);
        Assert.Equal("Followed by @jon@example.social", lines[^2].Text);
        Assert.Equal("Your note: met at a meetup", lines[^1].Text);
        Assert.Empty(lines[^4].Spans);
    }

    /// <summary>
    ///     Where the terminal cannot paint, the avatar's columns are reclaimed rather than held open — what #62
    ///     settled on a byline, at the header's own size.
    /// </summary>
    [Fact]
    public void Header_ReclaimsTheAvatarsColumnsWhereNoPictureIsComing()
    {
        var lines = Header(Bare(), pictures: FakePictures.DrawingNothing());

        Assert.Equal("Alice", lines[0].Text);
        Assert.DoesNotContain(lines.Take(4), line => line.Has(Role.Media));
    }

    /// <summary>
    ///     And where it can, an 8×4 box stands beside the four rows — named once, at its top, so a view reads a box's
    ///     whole shape from one place.
    /// </summary>
    [Fact]
    public void Header_SetsTheFourRowsBesideAnEightByFourAvatar()
    {
        var lines = Header(
            Bare(),
            pictures: FakePictures.With().HoldingAvatarOf("alice@hachyderm.io", 96, 96));

        var inset = Assert.Single(lines[0].Insets);

        Assert.Equal(8, inset.Columns);
        Assert.Equal(4, inset.Rows);
        Assert.Equal(0, inset.Column);
        Assert.Equal("avatar:alice@hachyderm.io", lines[0].Wants?.Id);

        Assert.All(lines.Take(4), line => Assert.StartsWith("          ", line.Text));
        Assert.All(lines.Skip(1).Take(3), line => Assert.Empty(line.Insets));
    }

    /// <summary>
    ///     The columns are taken from the first frame, before the pixels land: a header that gained ten columns on
    ///     arrival would shove the name across the row as the reader was reading it.
    /// </summary>
    [Fact]
    public void Header_HoldsTheAvatarsColumnsWhileThePixelsAreStillOnTheirWay()
    {
        var lines = Header(Bare(), pictures: FakePictures.With());

        Assert.Empty(lines[0].Insets);
        Assert.Equal("avatar:alice@hachyderm.io", lines[0].Wants?.Id);
        Assert.StartsWith("          Alice", lines[0].Text);
    }

    /// <summary>No row runs past the width, however long a name, a field's label or a handle in common is.</summary>
    [Fact]
    public void Header_ReadsAtSixtyOneColumns()
    {
        var lines = Header(
            AnAccount.With(
                address: "somebody@an-extremely-long-instance-domain.example",
                author: "Somebody With A Very Long Display Name Indeed, Truly",
                bio: "A bio with an address in it: https://example.com/a/very/long/path/that/keeps/going/on",
                fields:
                [
                    AnAccount.Field("A label nobody would sensibly type into a field this narrow", "and its value", Now),
                ],
                standing: AnAccount.Standing(following: true, note: new string('n', 200))),
            [
                AnAccount.With(address: "somebody-else@another-extremely-long-instance-domain.example"),
                AnAccount.With(address: "a-third-person@a-third-extremely-long-instance-domain.example"),
                AnAccount.With(address: "a-fourth@yet-another-long-domain.example"),
            ],
            pictures: FakePictures.With().HoldingAvatarOf("somebody@an-extremely-long-instance-domain.example"));

        Assert.All(lines, line => Assert.True(line.Width <= 61, $"'{line.Text}' is {line.Width} columns"));
    }
}
