namespace Wooly.Core.Profiles;

/// <summary>
///     What adding a profile turned out to do. Both answers are things the user did not say and would want to know:
///     that a name they gave was already taken, and that this profile is now the one commands default to.
/// </summary>
/// <param name="ReplacedExisting">A profile of the same name was already set up, and this replaced it.</param>
/// <param name="IsCurrent">Commands with no <c>--profile</c> of their own now act as this profile.</param>
public sealed record ProfileAddition(bool ReplacedExisting, bool IsCurrent);
