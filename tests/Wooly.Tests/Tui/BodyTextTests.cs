using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The three things inside a post's text that are something in particular (#46). All three carry themselves
///     without colour — the <c>#</c>, the <c>@</c>, the scheme — so what is asserted here is which run took which
///     role, never how it looked.
/// </summary>
public class BodyTextTests
{
    [Fact]
    public void ProseIsOneRunOfBodyAndNothingElse()
    {
        var spans = BodyText.Spans("Finally shipped the terminal client rewrite.");

        Assert.Equal([new Span("Finally shipped the terminal client rewrite.", Role.Body)], spans);
    }

    [Fact]
    public void AHashtagIsItsOwnRole_LeadingHashAndAll()
    {
        var spans = BodyText.Spans("Shipped it. #dotnet #terminal-gui");

        Assert.Contains(new Span("#dotnet", Role.Hashtag), spans);

        // A tag is one word by the rule the rest of this client reads a tag by, so the hyphen ends it.
        Assert.Contains(new Span("#terminal", Role.Hashtag), spans);
        Assert.DoesNotContain(spans, span => span is { Role: Role.Hashtag, Text: "#terminal-gui" });
    }

    /// <summary>A mention is an account somebody named, whether or not the instance was said.</summary>
    [Theory]
    [InlineData("Thanks @maria@fosstodon.org for the fix", "@maria@fosstodon.org")]
    [InlineData("Thanks @maria for the fix", "@maria")]
    [InlineData("Ask @maria, she wrote it", "@maria")]
    public void AMentionIsItsOwnRole(string body, string mentioned) =>
        Assert.Contains(new Span(mentioned, Role.Mention), BodyText.Spans(body));

    /// <summary>An address in a mail address is not somebody being named, and the word before it says so.</summary>
    [Fact]
    public void AMailAddressIsNotAMention()
    {
        var spans = BodyText.Spans("Write to maria@fosstodon.org about it");

        Assert.DoesNotContain(spans, span => span.Role == Role.Mention);
    }

    [Theory]
    [InlineData("Read https://example.com/posts/1 first", "https://example.com/posts/1")]
    [InlineData("Read www.example.com today", "www.example.com")]
    [InlineData("Read example.com/posts/1 first", "example.com/posts/1")]
    public void AnAddressIsItsOwnRole(string body, string address) =>
        Assert.Contains(new Span(address, Role.Link), BodyText.Spans(body));

    /// <summary>A sentence that ends in an address ends in a full stop, and the full stop is not part of the address.</summary>
    [Fact]
    public void AnAddressEndsWhereTheSentenceDoes()
    {
        var spans = BodyText.Spans("It is at https://example.com/posts/1.");

        Assert.Contains(new Span("https://example.com/posts/1", Role.Link), spans);
        Assert.Contains(new Span(".", Role.Body), spans);
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
        Assert.DoesNotContain(BodyText.Spans(body), span => span.Role == Role.Link);

    /// <summary>A hash inside an address is part of it, not a tag inside a link.</summary>
    [Fact]
    public void AFragmentInsideAnAddressIsPartOfIt()
    {
        var spans = BodyText.Spans("See https://example.com/docs#themes for the table");

        Assert.Contains(new Span("https://example.com/docs#themes", Role.Link), spans);
        Assert.DoesNotContain(spans, span => span.Role == Role.Hashtag);
    }

    /// <summary>Every character of the row survives being split up, in the order it was written.</summary>
    [Fact]
    public void TheRowIsAllThereAfterwards()
    {
        const string Row = "@maria@fosstodon.org: #dotnet at https://example.com/posts/1, thanks!";

        Assert.Equal(Row, string.Concat(BodyText.Spans(Row).Select(span => span.Text)));
    }

    /// <summary>A row with nothing on it is a row of the screen, and stays one.</summary>
    [Fact]
    public void AnEmptyRowIsStillARow() => Assert.Equal([new Span(string.Empty, Role.Body)], BodyText.Spans(string.Empty));
}
