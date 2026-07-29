using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance being asked who an access token belongs to, without the instance. Every token it accepts belongs to
///     <c>jeff</c> on whichever instance was asked, which is all a profile needs to be worth writing down.
/// </summary>
internal sealed class FakeAccessTokenVerifier(string? refusal) : IAccessTokenVerifier
{
    /// <summary>Every token it was asked about, in order — where a test proves what the command handed over.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>An instance that recognizes any token it is shown.</summary>
    public static FakeAccessTokenVerifier Accepting() => new(refusal: null);

    /// <summary>An instance that turns every token down, the way a mistyped one is turned down.</summary>
    public static FakeAccessTokenVerifier Refusing(string reason = "The access token is invalid") => new(reason);

    /// <remarks>
    ///     The refusal is worded unlike the real verifier's on purpose. A test that asserted this wording would be
    ///     checking a fake against a copy of itself, and would go on passing if
    ///     <see cref="AccessTokenVerifier" />'s message changed — so what reaches the user is that class's to phrase
    ///     and <c>AccessTokenVerifierTests</c>' to check. What a command test can fairly assert is that the reason it
    ///     supplied came out the other end.
    /// </remarks>
    public Task<string> VerifyAccount(string instance, string accessToken)
    {
        Tokens.Add(accessToken);

        return refusal is null
            ? Task.FromResult($"jeff@{instance}")
            : throw new AuthenticationException($"{instance} turned down that access token: {refusal}");
    }
}
