using Wooly.Core.Profiles;

namespace Wooly.Tests.Core;

/// <summary>What a profile says about who it signs in as.</summary>
public class ActiveProfileTests
{
    /// <summary>
    ///     The handle is the account's username behind an <c>@</c>, the way the rail and compose's From header both
    ///     draw it — and nothing for a profile that has not said who it signs in as.
    /// </summary>
    [Theory]
    [InlineData("jeff@mastodon.social", "@jeff")]
    [InlineData("jeff", "@jeff")]
    [InlineData(null, null)]
    public void Handle_IsTheUsernameBehindAnAt(string? account, string? handle)
    {
        var profile = new ActiveProfile
        {
            Name = "personal",
            Instance = "mastodon.social",
            Account = account,
            AccessToken = "token",
        };

        Assert.Equal(handle, profile.Handle);
    }
}
