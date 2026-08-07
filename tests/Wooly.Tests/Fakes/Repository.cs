namespace Wooly.Tests.Fakes;

/// <summary>
///     Where the checked-in files are, for the two tests that read the repository rather than the code in it — the
///     scan for a hard-coded colour, and the one that holds the role table in <c>docs/tui-shell.md</c> to the roles
///     that exist.
/// </summary>
internal static class Repository
{
    /// <summary>
    ///     The repository, found by walking up from wherever the test assembly ended up until the solution file turns
    ///     up. A path relative to the test binary would break the first time the build layout changed.
    /// </summary>
    public static string Root
    {
        get
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);

            while (here is not null && !File.Exists(Path.Combine(here.FullName, "Wooly.slnx")))
            {
                here = here.Parent;
            }

            return here?.FullName
                   ?? throw new InvalidOperationException("Could not find the repository from the test binary.");
        }
    }
}
