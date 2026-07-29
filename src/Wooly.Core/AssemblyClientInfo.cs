using System.Reflection;

namespace Wooly.Core;

/// <summary>
///     Reads <see cref="IClientInfo.Version" /> off an assembly's informational version, so the version users see is
///     the one stamped at build time rather than a constant that drifts out of date.
/// </summary>
public sealed class AssemblyClientInfo : IClientInfo
{
    /// <summary>Reported when an assembly carries no informational version at all (e.g. a dynamic assembly).</summary>
    private const string UnknownVersion = "0.0.0";

    public AssemblyClientInfo(Assembly assembly)
    {
        Version = ReadVersion(assembly);
    }

    /// <inheritdoc />
    public string Name => WoolyClient.Name;

    /// <inheritdoc />
    public string Version { get; }

    private static string ReadVersion(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return UnknownVersion;
        }

        // The SDK appends "+<commit sha>" build metadata; it is noise in a version banner.
        var buildMetadata = informationalVersion.IndexOf('+');

        return buildMetadata < 0 ? informationalVersion : informationalVersion[..buildMetadata];
    }
}
