using Wooly.Core.Accounts;

namespace Wooly.Tests.Core;

/// <summary>
///     The rule for how a user names somebody else's account, which every relationship command asks and none of them
///     owns — the same shape <see cref="Wooly.Core.Search.SearchQuery" /> has, and for the same reason.
/// </summary>
public class AccountAddressTests
{
    [Theory]
    [InlineData("alice@hachyderm.io", "alice@hachyderm.io")]
    [InlineData("  alice@hachyderm.io  ", "alice@hachyderm.io")]
    public void Parse_KeepsTheAddressAsTyped(string typed, string expected) =>
        Assert.Equal(expected, AccountAddress.Parse(typed).Text);

    /// <summary>
    ///     Mastodon shows a handle with a leading <c>@</c> everywhere a user might copy one from, so one typed that way
    ///     is the same account rather than a mistake.
    /// </summary>
    [Fact]
    public void Parse_TakesTheLeadingAtMastodonShowsAHandleWith() =>
        Assert.Equal("alice@hachyderm.io", AccountAddress.Parse("@alice@hachyderm.io").Text);

    /// <summary>An instance names its own accounts by bare username, so a user reading one there may type it back.</summary>
    [Fact]
    public void Parse_TakesABareUsernameAsAnAccountOnTheProfilesOwnInstance() =>
        Assert.Equal("alice", AccountAddress.Parse("alice").Text);

    [Theory]
    [InlineData("alice@hachyderm.io", "mastodon.social", "alice@hachyderm.io")]
    [InlineData("alice", "mastodon.social", "alice@mastodon.social")]
    public void On_NamesTheInstanceThatABareUsernameLeftUnsaid(string typed, string instance, string expected) =>
        Assert.Equal(expected, AccountAddress.Parse(typed).On(instance));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@")]
    [InlineData("@@")]
    [InlineData("alice@")]
    [InlineData("alice@hachyderm.io@extra")]
    [InlineData("alice hachyderm.io")]
    public void IsWellFormed_TurnsDownWhatIsNotAnAddress(string typed) =>
        Assert.False(AccountAddress.IsWellFormed(typed));

    [Theory]
    [InlineData("alice")]
    [InlineData("alice@hachyderm.io")]
    [InlineData("@alice@hachyderm.io")]
    public void IsWellFormed_TakesWhatIsOne(string typed) => Assert.True(AccountAddress.IsWellFormed(typed));

    /// <summary>
    ///     Parsing is for values a caller has already checked, so one that gets here unchecked is a defect in this
    ///     client rather than something the user did.
    /// </summary>
    [Fact]
    public void Parse_RefusesWhatACallerShouldHaveTurnedDownAgainstWhatTheUserTyped() =>
        Assert.Throws<ArgumentException>(() => AccountAddress.Parse("alice@"));
}
