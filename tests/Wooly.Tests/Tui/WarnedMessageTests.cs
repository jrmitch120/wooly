using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     A warned message on the conversations list: the warning stands, and the <c>x  show it</c> row under it does not
///     (#120). What that screen picks out is a conversation, so there is no post for <c>x</c> to have been asked about
///     — and a key named where it can do nothing reads as a shell that missed the press (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     The other side of it is <see cref="SensitiveMediaTests" />' and <see cref="WarnedPollTests" />': everywhere a
///     screen does pick the post out, the row is still there and <c>x</c> still answers it. Kept here rather than with
///     them because this is a fact about one screen rather than about how a post is drawn.
/// </remarks>
public class WarnedMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    ///     The first two acceptance criteria: the warning is drawn, the message stays behind it, and nothing under it
    ///     offers a key that cannot act.
    /// </summary>
    [Fact]
    public void Messages_DrawAWarnedMessagesWarningWithoutTheRowNamingX()
    {
        var lines = Listed(APost.With(contentWarning: "spoilers", content: "Ewe would not believe it"));

        Assert.Contains(lines, line => line.Text.Contains("⚠ spoilers", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains("x  show it", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains("Ewe would not", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A message the instance marked sensitive with nothing written over it says the same thing the same way: the
    ///     attachments are still hidden and still said to be, and the offer to show them is still not made here.
    /// </summary>
    [Fact]
    public void Messages_DrawASensitiveMessagesWarningWithoutTheRowNamingX()
    {
        var lines = Listed(APost.With(sensitive: true, media: [APost.APicture()]));

        Assert.Contains(lines, line => line.Text.Contains("⚠ Sensitive media", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains("x  show it", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains("A cartoon sheep", StringComparison.Ordinal));
    }

    /// <summary>
    ///     What the answer costs the messages nothing is hidden on, which is most of them: the flag is off for the
    ///     whole screen rather than for the warned rows on it, so a message with nothing behind anything still says
    ///     what it says and still carries its counts underneath.
    /// </summary>
    [Fact]
    public void Messages_DrawAnUnwarnedMessageInFull()
    {
        var lines = Listed(APost.With(content: "Ewe would not believe it"));

        Assert.Contains(lines, line => line.Text.Contains("Ewe would not believe it", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("↺ 3", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The third and fourth acceptance criteria on the screen nearest this one: opening the conversation shows the
    ///     message the ordinary way. Every message in the thread is a post the screen picks out, so the row is back and
    ///     <c>x</c> answers it — as it is everywhere else, which <see cref="SensitiveMediaTests" />' and
    ///     <see cref="WarnedPollTests" />' own row assertions go on saying unchanged.
    /// </summary>
    [Fact]
    public void Conversation_OffersTheRowNamingXOnTheSameWarnedMessage()
    {
        var warned = APost.With(contentWarning: "spoilers", content: "Ewe would not believe it");
        var screen = new ConversationScreen(AConversation.Thread(AConversation.With(), warned));

        var lines = screen.Lines(61, Now);

        Assert.Contains(lines, line => line.Text.Contains("⚠ spoilers", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("x  show it", StringComparison.Ordinal));

        Assert.True(screen.Reveal());

        Assert.Contains(
            screen.Lines(61, Now),
            line => line.Text.Contains("Ewe would not believe it", StringComparison.Ordinal));
    }

    /// <summary>The conversations list, with <paramref name="latest" /> as the last thing said in the one on it.</summary>
    private static IReadOnlyList<Line> Listed(Post latest) =>
        new DirectMessagesScreen([AConversation.With(latest: latest)]).Lines(61, Now, FakePictures.With());
}
