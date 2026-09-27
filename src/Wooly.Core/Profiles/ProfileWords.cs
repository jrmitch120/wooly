namespace Wooly.Core.Profiles;

/// <summary>
///     What somebody adding or making the default a profile is told about it, in words. Shared by the
///     <c>profile</c> commands and the TUI for the reason <see cref="BrowserSignIn" /> is: the same state described two
///     ways reads as two states (#245).
/// </summary>
public static class ProfileWords
{
    /// <summary>A profile given no name, turned down before anything is written.</summary>
    public const string NameMissing = "Give the profile a name to be known by, e.g. work.";

    /// <summary>
    ///     That <paramref name="name" /> is now the current profile, which commands act as when not told otherwise.
    /// </summary>
    /// <param name="name">The profile's name, written the way the front end draws it.</param>
    public static string ActsAs(string name) => $"Commands act as {name} unless told otherwise.";
}
