using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>A save is sent as changed (ADR-0028): asking for what the public profile already says sends nothing.</summary>
public class PublicProfileChangeTests
{
    [Fact]
    public void Against_LeavesOutWhatThePublicProfileAlreadySays()
    {
        var read = APublicProfile.With(displayName: "Jeff", bio: "Writes Wooly.", isLocked: false, isBot: false);

        var change = new PublicProfileChange
        {
            DisplayName = "Jeff",
            Bio = "Writes Wooly, mostly.",
            Fields = read.Fields.ToList(),
            IsLocked = false,
            IsBot = true,
        }.Against(read);

        Assert.Equal(new PublicProfileChange { Bio = "Writes Wooly, mostly.", IsBot = true }, change);
    }

    /// <summary>The fields are one value: any row differing sends them all.</summary>
    [Fact]
    public void Against_KeepsEveryFieldWhereAnyDiffers()
    {
        var read = APublicProfile.With(fields: [new("Site", "https://wooly.example"), new("Pronouns", "they/them")]);
        IReadOnlyList<PublicProfileField> wanted = [new("Site", "https://wooly.example"), new("Pronouns", "he/him")];

        Assert.Equal(wanted, new PublicProfileChange { Fields = wanted }.Against(read).Fields);
    }

    /// <summary>
    ///     A picture read is an address and a picture asked for is a file, and nothing says whether they are the same
    ///     picture — so one asked for is always sent, and a change carrying only that is not empty.
    /// </summary>
    [Fact]
    public void Against_KeepsEveryPictureAskedFor()
    {
        var change = new PublicProfileChange { Avatar = "me.png", Header = "hills.png", IsBot = false }
            .Against(APublicProfile.With(isBot: false));

        Assert.Equal(new PublicProfileChange { Avatar = "me.png", Header = "hills.png" }, change);
        Assert.False(change.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WhereEverythingAskedForIsAlreadySo()
    {
        var read = APublicProfile.With(isDiscoverable: true);

        Assert.True(new PublicProfileChange { IsDiscoverable = true }.Against(read).IsEmpty);
        Assert.False(new PublicProfileChange { IsDiscoverable = false }.Against(read).IsEmpty);
    }
}
