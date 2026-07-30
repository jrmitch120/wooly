using Wooly.Cli.Options;

namespace Wooly.Tests.Cli;

/// <summary>
///     A file and what it shows have to arrive as one word on a command line, and a colon is what separates them. The
///     rule is tested as the pure parse it is, because the interesting cases are all about which colon is the separator —
///     a Windows path starts with one that is not.
/// </summary>
public class MediaOptionTests
{
    [Fact]
    public void Parse_ReadsAPathWithNoAltTextAsJustAPath()
    {
        var attachment = MediaOption.Parse("/home/jeff/cat.png");

        Assert.Equal("/home/jeff/cat.png", attachment.Path);
        Assert.Null(attachment.AltText);
    }

    [Fact]
    public void Parse_ReadsTheAltTextAfterTheColon()
    {
        var attachment = MediaOption.Parse("/home/jeff/cat.png:a ginger cat asleep");

        Assert.Equal("/home/jeff/cat.png", attachment.Path);
        Assert.Equal("a ginger cat asleep", attachment.AltText);
    }

    /// <summary>
    ///     A drive letter's colon is part of the path, not the separator. This client runs on Windows, where every
    ///     absolute path starts with one.
    /// </summary>
    [Theory]
    [InlineData(@"C:\pics\cat.png", @"C:\pics\cat.png", null)]
    [InlineData(@"C:\pics\cat.png:a ginger cat", @"C:\pics\cat.png", "a ginger cat")]
    [InlineData("c:/pics/cat.png:a ginger cat", "c:/pics/cat.png", "a ginger cat")]
    public void Parse_TakesADriveLettersColonAsPartOfThePath(string value, string path, string? altText)
    {
        var attachment = MediaOption.Parse(value);

        Assert.Equal(path, attachment.Path);
        Assert.Equal(altText, attachment.AltText);
    }

    /// <summary>Only the first colon separates, so alt text is free to contain one — and a person's sentence does.</summary>
    [Fact]
    public void Parse_LetsTheAltTextContainAColonOfItsOwn()
    {
        var attachment = MediaOption.Parse("cat.png:the sign reads: no cats");

        Assert.Equal("cat.png", attachment.Path);
        Assert.Equal("the sign reads: no cats", attachment.AltText);
    }

    /// <summary>Alt text nobody wrote is no alt text, rather than an empty description of the picture.</summary>
    [Theory]
    [InlineData("cat.png:")]
    [InlineData("cat.png:   ")]
    public void Parse_ReadsAnEmptyDescriptionAsNoneAtAll(string value) =>
        Assert.Null(MediaOption.Parse(value).AltText);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":a ginger cat")]
    public void IsWellFormed_RefusesAValueThatNamesNoFile(string value) =>
        Assert.False(MediaOption.IsWellFormed(value));

    [Theory]
    [InlineData("cat.png")]
    [InlineData("cat.png:a ginger cat")]
    [InlineData(@"C:\pics\cat.png")]
    public void IsWellFormed_AcceptsAValueThatNamesAFile(string value) =>
        Assert.True(MediaOption.IsWellFormed(value));
}
