using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Key = Terminal.Gui.Input.Key;

namespace Wooly.Tests.Tui;

/// <summary>
///     A post's body painted as it is typed (#572): its hashtags, mentions and addresses drawn in their roles, by the rule
///     a post's text is read by (<c>BodyText.References</c>), and everything else in the body role. Colour only — the
///     cells are the letters typed. Driven through the drawn compose screen (<see cref="ComposedView" />).
/// </summary>
public class ComposeReferencePaintTests
{
    /// <summary>A hashtag, a mention and an address are drawn in their roles, and the words between them are the body.</summary>
    [Fact]
    public async Task AHashtagAMentionAndAnAddressAreDrawnInTheirRoles()
    {
        using var view = await ComposedView.Of();

        view.Type("hi #rustlang @maria@hachyderm.io https://a.io/x ok");

        AssertDrawn(
            view,
            0,
            ("hi ", Role.Body),
            ("#rustlang", Role.Hashtag),
            (" ", Role.Body),
            ("@maria@hachyderm.io", Role.Mention),
            (" ", Role.Body),
            ("https://a.io/x", Role.Link),
            (" ok", Role.Body));
    }

    /// <summary>
    ///     The rule is the one a post is read by, not one of the editor's own: a bare <c>#</c> and a tag this client
    ///     would not treat as well-formed stay the body.
    /// </summary>
    [Fact]
    public async Task ABareHashAndAnIllFormedTagStayTheBody()
    {
        using var view = await ComposedView.Of();

        view.Type("a # b #a‿b c");

        AssertDrawn(view, 0, ("a # b #a‿b c", Role.Body));
    }

    /// <summary>
    ///     A <c>#</c> at the end of an address is part of the address, the second half of an email address is nobody
    ///     named, and a word with a dot in it is prose — as each is in a post.
    /// </summary>
    [Fact]
    public async Task AnAddressAnEmailAndADottedWordAreReadAsAPostReadsThem()
    {
        using var view = await ComposedView.Of();

        view.Type("https://a.io/d#themes me@a.io Node.js");

        AssertDrawn(
            view,
            0,
            ("https://a.io/d#themes", Role.Link),
            (" me@a.io Node.js", Role.Body));
    }

    /// <summary>
    ///     A mention is painted as it is typed, whether or not it would resolve, and while the list of people to
    ///     mention is open over it (#318).
    /// </summary>
    [Fact]
    public async Task AMentionIsPaintedWhileTheListOfPeopleToMentionIsOpen()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "maria@fosstodon.org")),
            Accounts = FakeAccountRelationships.HoldingNobody(),
        };

        using var view = await ComposedView.Of(built);

        view.Type("hi @ma");

        Assert.True(view.Mentions.Visible);
        AssertDrawn(view, 0, ("hi ", Role.Body), ("@ma", Role.Mention));

        view.Type("x@nowhere.example");
        view.Settle();

        AssertDrawn(view, 0, ("hi ", Role.Body), ("@max@nowhere.example", Role.Mention));
    }

    /// <summary>An address too long for a row is wrapped across two, and painted on both.</summary>
    [Fact]
    public async Task AnAddressWrappedAcrossRowsIsPaintedOnEach()
    {
        using var view = await ComposedView.Of();

        var width = view.Editor.Viewport.Width;
        var address = "https://example.com/" + new string('a', width);

        view.Type(address + " ok");

        // Wherever the editor broke it: what the first row holds is the start of the address, and the rest is below.
        var broken = view.Shown(view.Editor).TrimEnd().Length;

        Assert.InRange(broken, "https://example.com/".Length, address.Length - 1);

        AssertDrawn(view, 0, (address[..broken], Role.Link));
        AssertDrawn(view, 1, (address[broken..], Role.Link), (" ok", Role.Body));
    }

    /// <summary>Deleting into a tag, or typing a space inside it, un-paints what is no longer a reference.</summary>
    [Fact]
    public async Task WhatIsNoLongerAReferenceIsNoLongerPainted()
    {
        using var view = await ComposedView.Of();

        view.Type("#a");
        view.Press(Key.Backspace);

        AssertDrawn(view, 0, ("#", Role.Body));

        view.Type("ab");
        view.Press(Key.CursorLeft);
        view.Type(" ");

        AssertDrawn(view, 0, ("#a", Role.Hashtag), (" b", Role.Body));

        view.Press(Key.Home);
        view.Press(Key.CursorRight);
        view.Type(" ");

        AssertDrawn(view, 0, ("# a b", Role.Body));
    }

    /// <summary>Selected text stays the selected-text role, over a reference as over anything else.</summary>
    [Fact]
    public async Task ASelectionOverAReferenceStaysTheSelection()
    {
        using var view = await ComposedView.Of();

        view.Type("hi #rustlang");
        view.Editor.SelectAll();
        view.Redraw();

        AssertDrawn(view, 0, ("hi #rustlang", Role.SelectedText));
    }

    /// <summary>A reply opens on the mentions it is addressed with, and they are painted.</summary>
    [Fact]
    public async Task AReplysOpeningMentionsArePainted()
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "220", account: "ben@hachyderm.io")),
        };

        using var view = await ComposedView.Of(built, ComposeFor.Reply);

        AssertDrawn(view, 0, ("@ben@hachyderm.io", Role.Mention), (" ", Role.Body));
    }

    /// <summary>
    ///     The cells of the editor's <paramref name="row" />, from its first column, are each run of text in its role,
    ///     in turn.
    /// </summary>
    private static void AssertDrawn(ComposedView view, int row, params (string Text, Role Role)[] runs) =>
        DrawnRuns.AssertDrawn(view.Application.Driver!.Contents!, view.Editor, row, Themes.Dark, runs);
}
