namespace Wooly.Core.Profiles;

/// <summary>
///     What removing a profile turned out to do. Whether it was current is the answer the user did not ask for and
///     most needs: if it was, commands with no <c>--profile</c> of their own now have nothing to act as.
/// </summary>
/// <param name="WasCurrent">The removed profile was the one commands defaulted to, and no profile is current now.</param>
public sealed record ProfileRemoval(bool WasCurrent);
