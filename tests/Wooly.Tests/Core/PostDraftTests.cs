using Wooly.Core.Posts;

namespace Wooly.Tests.Core;

/// <summary>
///     What an instance will and will not take as a post, tested where the rule lives — the argument parser asks it so a
///     user reads the answer where they typed the mistake, and the adapter asks it again before a request goes out.
/// </summary>
public class PostDraftTests
{
    [Fact]
    public void Problem_AcceptsAPostWithSomethingToSay() =>
        Assert.Null(new PostDraft { Text = "Hello world" }.Problem);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Problem_RefusesAPostWithNothingToSay(string text) =>
        Assert.NotNull(new PostDraft { Text = text }.Problem);

    /// <summary>A picture can be the whole of what somebody wanted to say.</summary>
    [Fact]
    public void Problem_AcceptsAPostWithNoTextWhenItCarriesAFile() =>
        Assert.Null(new PostDraft
        {
            Text = string.Empty,
            Media = [new MediaAttachment { Path = "cat.png" }],
        }.Problem);

    /// <summary>
    ///     Answered here rather than left to the instance's refusal, because by the time an instance refuses, the files
    ///     have already been uploaded.
    /// </summary>
    [Fact]
    public void Problem_RefusesAPostCarryingBothFilesAndAPoll() =>
        Assert.NotNull(new PostDraft
        {
            Text = "Cats or dogs?",
            Media = [new MediaAttachment { Path = "cat.png" }],
            Poll = PostPoll.Of(["Cats", "Dogs"], TimeSpan.FromDays(1)),
        }.Problem);

    /// <summary>
    ///     Not a synonym for public. An account whose own default is followers-only would otherwise have every post from
    ///     this client published wider than the account asked for.
    /// </summary>
    [Fact]
    public void Visibility_IsUnsaidUntilSomebodySaysIt() =>
        Assert.Null(new PostDraft { Text = "Hello world" }.Visibility);
}
