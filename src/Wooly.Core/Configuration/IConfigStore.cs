namespace Wooly.Core.Configuration;

/// <summary>The non-secret half of what this client persists. Access tokens go through the credential store instead.</summary>
public interface IConfigStore
{
    /// <summary>
    ///     Reads the stored configuration, or <see cref="WoolyConfig.Empty" /> if nothing has been written yet.
    /// </summary>
    /// <exception cref="Errors.ConfigurationException">The file exists but cannot be made sense of.</exception>
    WoolyConfig Load();

    /// <summary>Writes <paramref name="config" /> out in full, replacing whatever was stored before.</summary>
    void Save(WoolyConfig config);
}
