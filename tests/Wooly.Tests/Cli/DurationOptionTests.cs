using Wooly.Cli.Options;

namespace Wooly.Tests.Cli;

/// <summary>
///     How long something lasts, written the way a person says it. Tested as the pure parse it is; what it is for —
///     how long a poll stays open — is tested through the command.
/// </summary>
public class DurationOptionTests
{
    [Theory]
    [InlineData("30m", 30)]
    [InlineData("6h", 360)]
    [InlineData("7d", 10080)]

    // Nobody thinks about case at a shell prompt, and a stray space survives a copied-out command.
    [InlineData("6H", 360)]
    [InlineData(" 6h ", 360)]
    public void Parse_ReadsADurationAsMinutes(string value, int expectedMinutes) =>
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), DurationOption.Parse(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]

    // A bare number is the one thing that looks obvious and is not: 6 could be minutes, hours or days, and guessing
    // wrong closes a poll a week early or a week late.
    [InlineData("6")]
    [InlineData("soon")]
    [InlineData("6 hours")]
    [InlineData("-6h")]
    [InlineData("0h")]
    [InlineData("6w")]
    [InlineData("h")]
    public void Parse_RefusesWhatIsNotADuration(string? value) => Assert.Null(DurationOption.Parse(value));

    /// <summary>The units are named, because a user who wrote one this does not take has no other way to learn them.</summary>
    [Fact]
    public void Rejection_ShowsWhatADurationLooksLike()
    {
        var rejection = DurationOption.Rejection("soon");

        Assert.Contains("soon", rejection);
        Assert.Contains("6h", rejection);
    }
}
