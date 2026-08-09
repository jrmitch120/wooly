using Microsoft.Extensions.DependencyInjection;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="AccessTokenVerifier" /> — normally proven at the <see cref="System.Net.Http.HttpMessageHandler" />
///     seam in <c>AccessTokenVerifierTests</c> — against the live instance <c>tests/integration/seed.sh</c> seeds, to
///     catch drift between Mastonet's model of <c>verify_credentials</c> and what a real instance returns (ADR-0001,
///     ADR-0005, #33).
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class AccessTokenVerifierIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task VerifyAccount_SignsInAsTheSeededTestAccount()
    {
        var profile = LiveInstance.Profile;
        var verifier = LiveInstance.NewServices().GetRequiredService<IAccessTokenVerifier>();

        var account = await verifier.VerifyAccount(profile.Instance, profile.AccessToken);

        Assert.Equal($"{LiveInstance.Username}@{profile.Instance}", account);
    }

    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task VerifyAccount_RefusesATokenTheInstanceDoesNotRecognise()
    {
        var profile = LiveInstance.Profile;
        var verifier = LiveInstance.NewServices().GetRequiredService<IAccessTokenVerifier>();

        await Assert.ThrowsAsync<AuthenticationException>(
            () => verifier.VerifyAccount(profile.Instance, "not-a-real-token"));
    }
}
