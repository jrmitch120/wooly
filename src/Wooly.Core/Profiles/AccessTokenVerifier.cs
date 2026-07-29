using Mastonet;
using Wooly.Core.Errors;

namespace Wooly.Core.Profiles;

/// <summary>
///     Checks a token by making the smallest authenticated call there is — asking the instance which account the
///     caller is. It answers both questions at once: whether the token works, and what to record the profile as.
/// </summary>
public sealed class AccessTokenVerifier(IMastodonClientFactory clientFactory) : IAccessTokenVerifier
{
    /// <inheritdoc />
    public async Task<string> VerifyAccount(string instance, string accessToken)
    {
        try
        {
            var account = await clientFactory.CreateClient(instance, accessToken).GetCurrentUser();

            // The instance reports its own accounts by bare username, so the domain is added here — a profile has to
            // say which instance its account is on to be worth reading next to another profile's.
            return $"{account.UserName}@{instance}";
        }
        catch (ServerErrorException exception)
        {
            // The only call made was "who am I", so an instance refusing it is refusing the token. Its own wording
            // ("The access token is invalid", "This method requires an authenticated user") says more about which
            // way the token is wrong than this client could work out.
            throw new AuthenticationException($"{instance} would not accept that access token: {exception.Message}");
        }
    }
}
