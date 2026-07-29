namespace Wooly.Tests.Core;

/// <summary>
///     Groups the tests that write <c>GCM_CREDENTIAL_STORE</c>. The process environment is one shared thing, and
///     xUnit runs test classes in parallel by default, so without this they would overwrite each other's setup.
/// </summary>
[CollectionDefinition(nameof(GcmEnvironmentCollection))]
public sealed class GcmEnvironmentCollection;
