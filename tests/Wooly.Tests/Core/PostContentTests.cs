using Wooly.Core.Posts;

namespace Wooly.Tests.Core;

/// <summary>
///     A post's text arrives as HTML and has to come out as something a terminal can print. This is a pure function
///     over a string, and it is tested as one rather than through <c>TimelineReader</c>: the cases that matter are the
///     shapes of markup an instance sends, and a table of them says that far more plainly than a table of payloads.
/// </summary>
public class PostContentTests
{
    [Theory]
    // The ordinary case: one paragraph, nothing else.
    [InlineData("<p>Hello world</p>", "Hello world")]

    // Paragraphs are what the author pressed enter twice for, so they stay a blank line apart.
    [InlineData("<p>First</p><p>Second</p>", "First\n\nSecond")]

    // A break is one line ending, not two.
    [InlineData("<p>Line one<br />Line two</p>", "Line one\nLine two")]

    // Links, mentions and hashtags are all markup around text that is already readable.
    [InlineData("""<p>Look at <a href="https://x.test/">x.test</a></p>""", "Look at x.test")]
    [InlineData("""<p><span class="h-card"><a href="https://h.io/@alice">@<span>alice</span></a></span> hi</p>""", "@alice hi")]

    // Entities are the author's own characters, and are decoded after the markup is gone so that an escaped angle
    // bracket in their text cannot be read as a tag.
    [InlineData("<p>Tom &amp; Jerry</p>", "Tom & Jerry")]
    [InlineData("<p>&lt;script&gt;</p>", "<script>")]
    [InlineData("<p>caf&#233;</p>", "café")]

    // Empty in every way an instance says it: a post that is only media has no text at all.
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("<p></p>", "")]

    // However many blank lines the markup implied, a post is one block of text — three in a row is the HTML showing
    // through.
    [InlineData("<p>Above</p><p></p><p></p><p>Below</p>", "Above\n\nBelow")]
    public void ToPlainText_FlattensTheMarkupAnInstanceSendsAPostsTextAs(string? html, string expected) =>
        Assert.Equal(expected, PostContent.ToPlainText(html));
}
