using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     The count at the foot of compose (#319): how much of the post's limit has been used, counted the way the
///     instance will judge it, and coloured as it nears the limit and passes it.
/// </summary>
public class ComposeCountTests
{
    private const int Width = 60;

    private const int Height = 17;

    private static readonly Post Bens = APost.With(id: "220", account: "ben@hachyderm.io");

    private static readonly Post Mine = APost.With(id: "110", account: "jeff@mastodon.social");

    /// <summary>An empty fresh post has used none of it, right-aligned inside the padding.</summary>
    [Fact]
    public async Task AFreshPostCountsNothing()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        // Right-aligned, two columns in from the panel's edge like everything else on the screen.
        Assert.Equal("0 / 500".PadLeft(Width - 2), Count(compose).Text);
    }

    /// <summary>It follows every edit.</summary>
    [Fact]
    public async Task TheCountFollowsTheText()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = "Hello";

        Assert.Equal("5 / 500", Counted(compose));

        compose.Text = "Hell";

        Assert.Equal("4 / 500", Counted(compose));
    }

    /// <summary>A reply opening on its mention, and an edit opening on the post, count that text from the start.</summary>
    [Theory]
    [InlineData(ComposeFor.Reply, "5 / 500")]
    [InlineData(ComposeFor.Edit, "11 / 500")]
    public async Task WhatAComposeOpensWithIsCountedFromTheStart(ComposeFor opening, string counted)
    {
        var compose = await Opening(opening, opening == ComposeFor.Edit ? Mine : Bens);

        // A reply opens on "@ben@hachyderm.io ", which counts as "@ben "; an edit on "Hello world".
        Assert.Equal(counted, Counted(compose));
    }

    /// <summary>Any address counts as 23, however long it is.</summary>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://a.co")]
    [InlineData("https://example.com/a/very/long/path/that/goes/on/and/on?with=a&query=string#and-a-fragment")]
    [InlineData("https://example.com/")]
    [InlineData("gemini://example.org/a/long/path/on/a/capsule")]
    [InlineData("ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi")]
    public async Task AnAddressCountsAsTwentyThree(string address)
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = $"see {address}";

        Assert.Equal("27 / 500", Counted(compose));
    }

    /// <summary>Punctuation closing the sentence an address ends is the sentence's, not the address's.</summary>
    [Fact]
    public async Task PunctuationAfterAnAddressIsCountedAsWritten()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = "(see https://example.com/page).";

        Assert.Equal("30 / 500", Counted(compose));
    }

    /// <summary>Two addresses run together by a comma are two addresses, as the instance reads them.</summary>
    [Fact]
    public async Task AddressesRunTogetherAreCountedApart()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = "https://a.example.com,https://b.example.com";

        Assert.Equal("47 / 500", Counted(compose));
    }

    /// <summary>A mention of somebody elsewhere counts only its username; one on the profile's instance counts whole.</summary>
    [Theory]
    [InlineData("@ben@hachyderm.io hi", "7 / 500")]
    [InlineData("@ben hi", "7 / 500")]
    [InlineData("hi @some.one@example.social", "12 / 500")]
    public async Task AMentionCountsItsUsernameOnly(string text, string counted)
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = text;

        Assert.Equal(counted, Counted(compose));
    }

    /// <summary>A mail address is not a mention, and is counted letter for letter.</summary>
    [Fact]
    public async Task AMailAddressIsCountedWhole()
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = "name@example.com";

        Assert.Equal("16 / 500", Counted(compose));
    }

    /// <summary>A character made of several code points — an emoji, an accented letter — is one, as the instance reads it.</summary>
    [Theory]
    [InlineData("👩‍👩‍👧‍👦")]
    [InlineData("🇨🇦")]
    [InlineData("👍🏽")]
    [InlineData("e\u0301")]
    public async Task ACharacterOfSeveralCodePointsCountsAsOne(string character)
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = character;

        Assert.Equal("1 / 500", Counted(compose));
    }

    /// <summary>The warning is part of what the instance counts, so it is part of the count.</summary>
    [Fact]
    public async Task AWarningIsCounted()
    {
        var warned = APost.With(id: "220", account: "ben@hachyderm.io", contentWarning: "spoilers");

        var compose = await Opening(ComposeFor.Reply, warned);

        Assert.Equal("13 / 500", Counted(compose));
    }

    /// <summary>Muted to 450, the quota's low colour from 451 to 500, and an error's past it.</summary>
    [Theory]
    [InlineData(0, Role.Muted)]
    [InlineData(450, Role.Muted)]
    [InlineData(451, Role.QuotaLow)]
    [InlineData(500, Role.QuotaLow)]
    [InlineData(501, Role.Error)]
    public async Task TheCountChangesColourNearTheLimitAndPastIt(int length, Role role)
    {
        var compose = await Opening(ComposeFor.Post, Mine);

        compose.Text = new string('a', length);

        var count = Count(compose);

        Assert.Equal($"{length} / 500", count.Text.Trim());
        Assert.Equal(role, Assert.Single(count.Spans, span => span.Text.Trim().Length > 0).Role);
    }

    /// <summary>
    ///     Nearly full and past the limit are told apart in every theme with colour, so a reader can see which side of
    ///     the limit the post is on.
    /// </summary>
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void NearlyFullAndPastTheLimitAreDifferentColours(string name)
    {
        var theme = name == "dark" ? Themes.Dark : Themes.Light;

        Assert.NotEqual(theme.For(Role.Error), theme.For(Role.QuotaLow));
    }

    /// <summary>
    ///     Typed into compose's real fields — the post, and then the warning — the count follows each letter, and each
    ///     is announced as a change, which is what has the window draw the count again.
    /// </summary>
    [Fact]
    public async Task TheCountFollowsTypingInThePostAndTheWarning()
    {
        using var view = await ComposedView.Of();
        var changes = 0;

        view.Shell.Changed += () => changes++;

        view.Type("Hello");

        Assert.Equal("5 / 500", Counted(view.Compose));
        Assert.Equal(5, changes);

        view.Press(Key.W.WithCtrl);
        changes = 0;
        view.Type("cw!");

        Assert.Equal("8 / 500", Counted(view.Compose));
        Assert.Equal(3, changes);
        Assert.Contains(view.Rows(), row => row.Contains(" 8 / 500", StringComparison.Ordinal));
    }

    private static Line Count(ComposeScreen compose) =>
        compose.Lines(new Drawing(Width, AShell.Now, Height: Height))[^1];

    private static string Counted(ComposeScreen compose) => Count(compose).Text.Trim();

    private static async Task<ComposeScreen> Opening(ComposeFor opening, Post post)
    {
        var shell = await new AShell { Timelines = FakeTimelineReader.Holding(post) }.Opened();

        return ComposeRows.Open(shell, opening);
    }
}
