using Wooly.Tui.Screens;

namespace Wooly.Tests.Tui;

/// <summary>
///     What a compose screen's rows read as drawn, and the one way a test opens one — so that the wording of a header
///     is written down once rather than in every test that reads it (#317).
/// </summary>
internal static class ComposeRows
{
    /// <summary>The warning header with nothing written in it and nobody writing.</summary>
    public const string NoWarning = "   Warn  none · ctrl-w to add";

    /// <summary>The Media header with nothing attached (#375).</summary>
    public const string NoMedia = "  Media  none · ctrl-o to add";

    /// <summary>To on a post going out public, as every post <c>APost</c> builds does (#338).</summary>
    public const string ToPublic = "     To  ● public  ○ unlisted  ○ followers  ○ direct";

    /// <summary>To on a fresh post with no <c>default_visibility</c>, which sends nothing (#338).</summary>
    public const string ToAccountDefault = "     To  ◂ ● account default ▸";

    /// <summary>Lang with no language in it, which sends none (#340).</summary>
    public const string NoLanguage = "   Lang  none · the instance decides";

    /// <summary>The warning header holding <paramref name="written" />.</summary>
    public static string Warning(string written) => $"   Warn  {written}";

    /// <summary>A hairline across a content region <paramref name="width" /> wide, inside its two columns of padding.</summary>
    public static string Hairline(int width) => $"  {new string('─', width - 4)}";

    /// <summary>Opens a compose for <paramref name="purpose" /> on the post <paramref name="shell" /> is showing.</summary>
    public static ComposeScreen Open(Wooly.Tui.Shell.Shell shell, ComposeFor purpose)
    {
        switch (purpose)
        {
            case ComposeFor.Post:
                shell.Compose();

                break;
            case ComposeFor.Reply:
                shell.Reply();

                break;
            default:
                shell.Edit();

                break;
        }

        return Assert.IsType<ComposeScreen>(shell.Screen);
    }
}
