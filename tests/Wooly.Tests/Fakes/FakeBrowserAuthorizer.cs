using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A sign-in through the browser, without the browser or the instance: it hands out an address to be sent to, and
///     then either the token the user authorized or the refusal they gave instead.
/// </summary>
internal sealed class FakeBrowserAuthorizer(string accessToken, string? refusal) : IBrowserAuthorizer, IBrowserAuthorization
{
    /// <summary>Every instance a sign-in was begun against, in order.</summary>
    public List<string> Instances { get; } = [];

    /// <summary>Whether whoever began the sign-in gave the port back afterwards.</summary>
    public bool Disposed { get; private set; }

    /// <summary>
    ///     Shaped like the real one in the way that matters to a command: escaped. A real authorization request carries
    ///     a scope list and a redirect URI in its query, so anything that writes this address down has an escape to
    ///     lose.
    /// </summary>
    public Uri AuthorizationUrl { get; } = new(
        "https://mastodon.social/oauth/authorize?client_id=client-abc&scope=read%20write&redirect_uri=http%3A%2F%2F127.0.0.1%3A54321%2F");

    /// <summary>A sign-in the user goes through with, authorizing <c>token-from-browser</c>.</summary>
    public static FakeBrowserAuthorizer Authorizing() => new("token-from-browser", refusal: null);

    /// <summary>A sign-in the user turns down at the instance, the way a mis-clicked "Cancel" turns one down.</summary>
    public static FakeBrowserAuthorizer Refusing(string reason = "the request was denied") => new(string.Empty, reason);

    /// <inheritdoc />
    public Task<IBrowserAuthorization> Begin(string instance, CancellationToken cancellationToken)
    {
        Instances.Add(instance);

        return Task.FromResult<IBrowserAuthorization>(this);
    }

    /// <remarks>
    ///     The refusal is worded unlike the real authorizer's for the same reason
    ///     <see cref="FakeAccessTokenVerifier" />'s is: what a command test can fairly assert is that the reason it
    ///     supplied came out the other end, not how <see cref="BrowserAuthorizer" /> phrases one.
    /// </remarks>
    public Task<string> AwaitAccessToken(CancellationToken cancellationToken) =>
        refusal is null
            ? Task.FromResult(accessToken)
            : throw new AuthenticationException($"mastodon.social did not authorize this client: {refusal}");

    /// <inheritdoc />
    public void Dispose() => Disposed = true;
}
