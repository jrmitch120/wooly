using Wooly.Core.Configuration;

namespace Wooly.Core.Profiles;

/// <summary>What removing a profile turned out to do.</summary>
/// <param name="Removed">Where the removed profile pointed, so the user can be told which account it was.</param>
public sealed record ProfileRemoval(ProfileConfig Removed);
