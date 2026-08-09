namespace Wooly.Tests.Integration;

/// <summary>
///     Serializes every test against the live instance behind one xunit collection. They share one seeded account
///     (<c>tests/integration/seed.sh</c>), so a timeline read racing a post publish from another test would be
///     reading its own setup out from under it — parallelism here would trade a flake for a fast run of a suite ADR-0005
///     already keeps out of the fast run.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveInstanceCollection
{
    public const string Name = "Live Mastodon instance";
}
