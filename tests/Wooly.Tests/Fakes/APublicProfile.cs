using Wooly.Core.Accounts;

namespace Wooly.Tests.Fakes;

/// <summary>A public profile as written, with everything filled in, so a test only says the part it is about.</summary>
internal static class APublicProfile
{
    public static PublicProfile With(
        string displayName = "Jeff",
        string bio = "Writes Wooly.",
        IReadOnlyList<PublicProfileField>? fields = null,
        bool isLocked = false,
        bool isBot = false,
        bool isDiscoverable = true,
        Account? account = null) => new()
    {
        DisplayName = displayName,
        Bio = bio,
        Fields = fields ?? [new PublicProfileField("Site", "https://wooly.example")],
        AvatarUrl = "https://files.example/avatar.png",
        HeaderUrl = "https://files.example/header.png",
        IsLocked = isLocked,
        IsBot = isBot,
        IsDiscoverable = isDiscoverable,
        Account = account ?? AnAccount.With(address: "jeff@mastodon.social", author: displayName, bio: bio),
    };
}
