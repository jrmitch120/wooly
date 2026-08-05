namespace Wooly.Core;

/// <summary>
///     Where this client keeps its files on disk. One place resolves the OS-conventional location, so nothing else has
///     to reason about <see cref="Environment.SpecialFolder" /> — and tests point every file at a scratch directory by
///     constructing this with one.
/// </summary>
public sealed class WoolyPaths(string configDirectory)
{
    /// <summary>
    ///     The paths a real user's files live at: the OS's roaming application-data folder, under this client's own
    ///     directory. .NET resolves that per platform, and the three do not agree: <c>~/Library/Application Support</c>
    ///     on macOS, <c>~/.config</c> on Linux, <c>%APPDATA%</c> on Windows.
    /// </summary>
    public static WoolyPaths ForCurrentUser()
    {
        // DoNotVerify because the folder legitimately does not exist yet on a first run — creating it is Save's job,
        // and without this the call would hand back an empty string on Unix instead of the path.
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        return new WoolyPaths(Path.Combine(applicationData, "wooly"));
    }

    /// <summary>The directory holding every file this client writes. Not guaranteed to exist yet.</summary>
    public string ConfigDirectory { get; } = configDirectory;

    /// <summary>The human-readable, hand-editable TOML config: profiles, current profile, preferences.</summary>
    public string ConfigFile { get; } = Path.Combine(configDirectory, "config.toml");

    /// <summary>
    ///     Access tokens, in the clear — written only where no OS keyring is available to hold them instead
    ///     (ADR-0003). Kept beside the config rather than inside it so that a user reading the config file is never
    ///     looking at their secrets by accident.
    /// </summary>
    public string CredentialFile { get; } = Path.Combine(configDirectory, "credentials.toml");
}
