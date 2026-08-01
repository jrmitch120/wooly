using System.Text.RegularExpressions;

namespace Wooly.Tests.Tui;

/// <summary>
///     ADR-0014's rule, enforced rather than remembered: nothing in the TUI constructs a colour. A view names a role
///     and the theme answers it, so the only file allowed to hold an <c>Attribute</c>, a <c>Color</c> or a
///     <c>StandardColor</c> is the theme itself.
/// </summary>
/// <remarks>
///     A source scan rather than an assertion about behaviour, because that is what the rule is about: a hard-coded
///     pair cannot be themed, cannot degrade to a terminal with sixteen colours or none, and cannot be tested — and
///     the prototype's static palette of them is the exact anti-pattern this ticket was told to leave behind. The one
///     way to be sure a new screen has not quietly reintroduced one is to look.
/// </remarks>
public partial class NoHardCodedColourTests
{
    /// <summary>Where a colour may be built. Everything under here is the theme; everything else names roles.</summary>
    private const string TheThemesOwnFolder = "Theme";

    [Fact]
    public void NoViewOutsideTheThemeConstructsAColour()
    {
        var offenders = new List<string>();

        foreach (var file in Sources())
        {
            var relative = Path.GetRelativePath(TuiSources(), file);

            if (relative.StartsWith(TheThemesOwnFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            offenders.AddRange(
                Colours()
                    .Matches(text)
                    .Select(match => $"{relative}: {match.Value}"));
        }

        Assert.Empty(offenders);
    }

    /// <summary>The scan is worth nothing if it is looking at no files, so it says so rather than passing quietly.</summary>
    [Fact]
    public void TheScanIsLookingAtTheTuiSource()
    {
        var sources = Sources().ToList();

        Assert.True(sources.Count > 10, $"Only found {sources.Count} source files under {TuiSources()}.");
        Assert.Contains(sources, file => file.EndsWith("PostLines.cs", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Building an attribute, or naming one of Terminal.Gui's own colours. Deliberately crude: this is a rule about
    ///     what appears in the source, and something that has to be written in a roundabout way to get past a regular
    ///     expression is something a reviewer will see.
    /// </summary>
    [GeneratedRegex(@"\bnew\s+Attribute\s*\(|\bStandardColor\b|\bColorName16\b|\bnew\s+Color\s*\(")]
    private static partial Regex Colours();

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(TuiSources(), "*.cs", SearchOption.AllDirectories)
                 .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                 .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string TuiSources() => Path.Combine(RepositoryRoot(), "src", "Wooly.Tui");

    /// <summary>
    ///     The repository, found by walking up from wherever the test assembly ended up until the solution file turns
    ///     up. A path relative to the test binary would break the first time the build layout changed.
    /// </summary>
    private static string RepositoryRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Wooly.slnx")))
        {
            here = here.Parent;
        }

        return here?.FullName ?? throw new InvalidOperationException("Could not find the repository from the test binary.");
    }
}
