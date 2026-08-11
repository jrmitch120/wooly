using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The references inside a post's text — a hashtag, an account somebody named, an address (#46, #83). All three
///     carry themselves without colour — the <c>#</c>, the <c>@</c>, the scheme — so what is asserted here is which
///     run took which role and where it was, never how it looked.
/// </summary>
/// <remarks>
///     Asked of the whole post rather than of a row, which is what changed in #83: a reference has a place among the
///     others and a place in the text, and both are what <c>←</c> and <c>→</c> walk. Drawing a row is the second half
///     of this file, and takes the post's references with the row.
/// </remarks>
public class BodyTextTests
{
    /// <summary>How wide the content region is on an 80-column terminal, which is where the wrap cuts.</summary>
    private const int Width = 61;

    [Fact]
    public void ProseHoldsNoReferencesAtAll()
    {
        Assert.Empty(BodyText.References("Finally shipped the terminal client rewrite."));
    }

    [Fact]
    public void AHashtagIsAReference_LeadingHashAndAll()
    {
        var references = BodyText.References("Shipped it. #dotnet #terminal-gui");

        Assert.Contains(references, reference => reference is { Role: Role.Hashtag, Text: "#dotnet" });

        // A tag is one word by the rule the rest of this client reads a tag by, so the hyphen ends it.
        Assert.Contains(references, reference => reference is { Role: Role.Hashtag, Text: "#terminal" });
        Assert.DoesNotContain(references, reference => reference is { Role: Role.Hashtag, Text: "#terminal-gui" });
    }

    /// <summary>A mention is an account somebody named, whether or not the instance was said.</summary>
    [Theory]
    [InlineData("Thanks @maria@fosstodon.org for the fix", "@maria@fosstodon.org")]
    [InlineData("Thanks @maria for the fix", "@maria")]
    [InlineData("Ask @maria, she wrote it", "@maria")]
    public void AMentionIsAReference(string body, string mentioned) =>
        Assert.Contains(BodyText.References(body), reference => reference == Mention(body, mentioned));

    /// <summary>An address in a mail address is not somebody being named, and the word before it says so.</summary>
    [Fact]
    public void AMailAddressIsNotAMention()
    {
        Assert.DoesNotContain(
            BodyText.References("Write to maria@fosstodon.org about it"),
            reference => reference.Role == Role.Mention);
    }

    [Theory]
    [InlineData("Read https://example.com/posts/1 first", "https://example.com/posts/1")]
    [InlineData("Read www.example.com today", "www.example.com")]
    [InlineData("Read example.com/posts/1 first", "example.com/posts/1")]
    public void AnAddressIsAReference(string body, string address) =>
        Assert.Contains(BodyText.References(body), reference => reference == Link(body, address));

    /// <summary>A sentence that ends in an address ends in a full stop, and the full stop is not part of the address.</summary>
    [Fact]
    public void AnAddressEndsWhereTheSentenceDoes()
    {
        const string Said = "It is at https://example.com/posts/1.";

        Assert.Equal([Link(Said, "https://example.com/posts/1")], BodyText.References(Said));
    }

    /// <summary>
    ///     A dot in a word is not an address. The imprecision this client accepts is a bare domain typed as prose
    ///     being painted as a link; a file name and a library's name are not that, and are the common case.
    /// </summary>
    [Theory]
    [InlineData("Rewrote it in Node.js last week")]
    [InlineData("See config.toml for the rest")]
    [InlineData("Shipped, tested, documented, etc.")]
    public void AWordWithADotInItIsNotAnAddress(string body) =>
        Assert.DoesNotContain(BodyText.References(body), reference => reference.Role == Role.Link);

    /// <summary>A hash inside an address is part of it, not a tag inside a link.</summary>
    [Fact]
    public void AFragmentInsideAnAddressIsPartOfIt()
    {
        const string Said = "See https://example.com/docs#themes for the table";

        Assert.Equal([Link(Said, "https://example.com/docs#themes")], BodyText.References(Said));
    }

    /// <summary>
    ///     The whole point of asking the post rather than the row: the references come back in the order they were
    ///     written, each saying where it is, which is what an index into them means and what <c>←</c> and <c>→</c>
    ///     walk.
    /// </summary>
    [Fact]
    public void TheyComeBackInTheOrderTheyWereWrittenAndSayWhereTheyAre()
    {
        const string Said = "Thanks @maria@fosstodon.org — notes at https://example.com/notes #dotnet";

        var references = BodyText.References(Said);

        Assert.Equal(
            [Role.Mention, Role.Link, Role.Hashtag],
            references.Select(reference => reference.Role));

        Assert.All(references, reference => Assert.Equal(reference.Text, Said.Substring(reference.At, reference.Text.Length)));
    }

    /// <summary>
    ///     The bug the move to post-level matching fixes (#83): an address longer than the content region is cut in
    ///     two by the wrap, and used to be two halves of prose. It is one reference, drawn as one, on both rows.
    /// </summary>
    [Fact]
    public void AnAddressCutAcrossTwoRowsIsOneReferenceDrawnOnBoth()
    {
        const string Address = "https://example.com/a/very/long/path/indeed/that/keeps/going/and/going/and/going";
        var said = $"Notes at {Address} — worth a read";

        var references = BodyText.References(said);

        Assert.Equal([Link(said, Address)], references);

        var rows = TextWrap.Rows(said, Width);
        var spans = rows.SelectMany(row => BodyText.Spans(row, references)).ToList();

        // Two rows carry it, and between them they carry all of it — with nothing of it drawn as prose.
        Assert.Equal(2, rows.Count(row => BodyText.Spans(row, references).Any(span => span.Role == Role.Link)));
        Assert.Equal(Address, string.Concat(spans.Where(span => span.Role == Role.Link).Select(span => span.Text)));
    }

    /// <summary>Every character of the row survives being split up, in the order it was written.</summary>
    [Fact]
    public void TheRowIsAllThereAfterwards()
    {
        const string Said = "@maria@fosstodon.org: #dotnet at https://example.com/posts/1, thanks!";

        Assert.Equal(Said, string.Concat(Drawn(Said).Select(span => span.Text)));
    }

    /// <summary>A row with nothing on it is a row of the screen, and stays one.</summary>
    [Fact]
    public void AnEmptyRowIsStillARow() =>
        Assert.Equal([new Span(string.Empty, Role.Body)], BodyText.Spans(new TextWrap.Row(string.Empty, 0), []));

    /// <summary>
    ///     A picked reference is drawn in brackets, and the brackets are their own role — so a picked hashtag stays
    ///     hashtag-coloured and only the brackets shift (<c>docs/tui-shell.md</c>).
    /// </summary>
    [Fact]
    public void APickedReferenceIsBracketedInItsOwnRole()
    {
        const string Said = "Shipped it. #dotnet today";

        var references = BodyText.References(Said);
        var spans = Drawn(Said, references[0]);

        Assert.Equal("Shipped it. ‹#dotnet› today", string.Concat(spans.Select(span => span.Text)));

        Assert.Contains(spans, span => span is { Role: Role.ReferencePicked, Text: "‹" });
        Assert.Contains(spans, span => span is { Role: Role.ReferencePicked, Text: "›" });
        Assert.Contains(spans, span => span is { Role: Role.Hashtag, Text: "#dotnet" });
    }

    /// <summary>One is picked at a time, so the others are drawn as they always were.</summary>
    [Fact]
    public void OnlyThePickedOneIsBracketed()
    {
        const string Said = "#dotnet and #terminal";

        var references = BodyText.References(Said);

        Assert.Equal("#dotnet and ‹#terminal›", string.Concat(Drawn(Said, references[1]).Select(span => span.Text)));
        Assert.Equal(Said, string.Concat(Drawn(Said).Select(span => span.Text)));
    }

    /// <summary>
    ///     A picked reference cut across two rows is opened on the row it starts on and closed on the row it stops on
    ///     — two columns, once, rather than a pair of brackets on each half.
    /// </summary>
    [Fact]
    public void APickedReferenceCutAcrossTwoRowsIsOpenedOnceAndClosedOnce()
    {
        const string Address = "https://example.com/a/very/long/path/indeed/that/keeps/going/and/going/and/going";
        var said = $"Notes at {Address} — worth a read";

        var references = BodyText.References(said);
        var marks = TextWrap.Rows(said, Width)
                            .Select(row => string.Concat(
                                BodyText.Spans(row, references, references[0])
                                        .Where(span => span.Role == Role.ReferencePicked)
                                        .Select(span => span.Text)))
                            .Where(mark => mark.Length > 0)
                            .ToList();

        Assert.Equal(["‹", "›"], marks);
    }

    /// <summary>The text drawn as one row, which is what a body short enough to fit on one is.</summary>
    private static IReadOnlyList<Span> Drawn(string text, Reference? picked = null) =>
        BodyText.Spans(new TextWrap.Row(text, 0), BodyText.References(text), picked);

    /// <summary>The address <paramref name="written" /> where <paramref name="text" /> puts it.</summary>
    private static Reference Link(string text, string written) =>
        new(written, Role.Link, text.IndexOf(written, StringComparison.Ordinal));

    /// <inheritdoc cref="Link" />
    private static Reference Mention(string text, string written) =>
        new(written, Role.Mention, text.IndexOf(written, StringComparison.Ordinal));
}
