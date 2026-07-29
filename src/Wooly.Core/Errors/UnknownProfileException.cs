namespace Wooly.Core.Errors;

/// <summary>
///     A profile was named that has never been set up — a typo in <c>--profile</c>, most often. The names that do exist
///     are listed, because with a handful of short profile names that is usually the whole answer.
/// </summary>
public sealed class UnknownProfileException(string name, IEnumerable<string> knownProfiles)
    : WoolyException(BuildMessage(name, knownProfiles))
{
    private static string BuildMessage(string name, IEnumerable<string> knownProfiles)
    {
        var known = string.Join(", ", knownProfiles);

        return known.Length == 0
            ? $"There is no profile named '{name}'. No profiles have been set up yet."
            : $"There is no profile named '{name}'. Profiles that exist: {known}.";
    }
}
