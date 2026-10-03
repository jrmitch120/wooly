namespace Wooly.Core.Errors;

/// <summary>
///     The OS keyring would not give a profile's token up — locked, a permission prompt declined, or its service down.
///     Thrown rather than passed over, because a removal that carried on would leave the token in the keyring with no
///     profile left to reach it through (#296). Nothing has been changed, so trying again once the keyring is unlocked
///     finishes the job.
/// </summary>
public sealed class KeyringUnansweredException(string profileName, Exception refusal)
    : WoolyException(
        $"The OS keyring would not answer for profile \"{profileName}\" ({refusal.Message}), so its access token is " +
        "still there and the profile was not removed. Unlock the keyring and try again.",
        refusal)
{
    /// <summary>The profile whose token the keyring would not give up.</summary>
    public string ProfileName { get; } = profileName;
}
