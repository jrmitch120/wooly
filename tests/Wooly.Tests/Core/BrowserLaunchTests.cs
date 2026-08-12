using System.Runtime.InteropServices;
using Wooly.Core;

namespace Wooly.Tests.Core;

/// <summary>
///     The one thing this client does that leaves the terminal, asked without ever leaving it: which addresses are
///     opened at all, and what each platform is actually asked to do about the ones that are (#85, ADR-0014).
/// </summary>
/// <remarks>
///     Assertable for the reason role selection is (ADR-0014): the decision is separated from the act, so a hostile
///     scheme and a wrong platform call are both caught here rather than on somebody's machine — no process is started
///     by any of this, on any platform, including the one these tests happen to be running on.
/// </remarks>
public class BrowserLaunchTests
{
    /// <summary>What an instance's own elided links, and prose, look like inside a post's text.</summary>
    [Theory]
    [InlineData("https://example.com/notes", "https://example.com/notes")]
    [InlineData("http://example.com/notes", "http://example.com/notes")]
    [InlineData("www.example.com/notes", "https://www.example.com/notes")]
    [InlineData("example.com/notes", "https://example.com/notes")]
    public void Address_ReadsAWebPageOutOfWhatWasWritten(string written, string expected) =>
        Assert.Equal(expected, BrowserLaunch.Address(written)?.AbsoluteUri);

    /// <summary>
    ///     Including a page on a port, which <see cref="Uri" /> alone reads as a scheme called
    ///     <c>www.example.com</c> — and which would then be refused as a scheme this client does not open, for being
    ///     an ordinary address somebody typed a port into.
    /// </summary>
    [Theory]
    [InlineData("www.example.com:8080/notes", "https://www.example.com:8080/notes")]
    [InlineData("example.com:8080/notes", "https://example.com:8080/notes")]
    [InlineData("https://example.com:8080/notes", "https://example.com:8080/notes")]
    public void Address_ReadsAPageOnAPortAsAPageRatherThanAScheme(string written, string expected) =>
        Assert.Equal(expected, BrowserLaunch.Address(written)?.AbsoluteUri);

    /// <summary>And a colon inside the path is punctuation somebody wrote, not a scheme either.</summary>
    [Fact]
    public void Address_ReadsAColonInsideThePathAsPartOfIt() =>
        Assert.Equal("https://example.com/notes/re:this", BrowserLaunch.Address("example.com/notes/re:this")?.AbsoluteUri);

    /// <summary>
    ///     And nothing else is an address to open. A scheme is the whole of what makes handing text to a machine's
    ///     shell dangerous, so the two that are opened are named and everything else is refused — before any platform
    ///     has been asked anything.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<script>")]
    [InlineData("mailto:ben@hachyderm.io")]
    [InlineData("ftp://files.example.com/x")]
    [InlineData("")]
    [InlineData("   ")]
    public void Address_RefusesAnythingThatIsNotAWebPage(string written) =>
        Assert.Null(BrowserLaunch.Address(written));

    /// <summary>
    ///     Windows hands the address to the shell and lets the association decide what a browser is, which is what
    ///     <see cref="System.Diagnostics.ProcessStartInfo.UseShellExecute" /> means there.
    /// </summary>
    [Fact]
    public void For_AsksWindowsToOpenTheAddressItself()
    {
        var launch = BrowserLaunch.For(new Uri("https://example.com/notes"), OSPlatform.Windows);

        Assert.Equal("https://example.com/notes", launch?.FileName);
        Assert.Null(launch?.Argument);
        Assert.True(launch?.UseShellExecute);
    }

    /// <summary>macOS and Linux each name the program that knows what the user's browser is.</summary>
    [Theory]
    [InlineData("OSX", "open")]
    [InlineData("Linux", "xdg-open")]
    public void For_NamesTheProgramMacOsAndLinuxOpenAddressesWith(string platform, string expected)
    {
        var launch = BrowserLaunch.For(new Uri("https://example.com/notes"), OSPlatform.Create(platform));

        Assert.Equal(expected, launch?.FileName);
        Assert.Equal("https://example.com/notes", launch?.Argument);
        Assert.False(launch?.UseShellExecute);
    }

    /// <summary>
    ///     The refusal is the seam's and not just its caller's: a scheme nothing should hand to a shell is refused
    ///     here too, on every platform, so that reaching this with one starts nothing.
    /// </summary>
    [Theory]
    [InlineData("Windows")]
    [InlineData("OSX")]
    [InlineData("Linux")]
    public void For_RefusesASchemeNoBrowserShouldBeHanded(string platform) =>
        Assert.Null(BrowserLaunch.For(new Uri("file:///etc/passwd"), OSPlatform.Create(platform)));

    /// <summary>
    ///     The escapes survive the crossing, which is what <see cref="WebAddress" /> is for: the address is text again
    ///     by the time an OS parses it, and one escape lost here is one added twice by whatever parses it next.
    /// </summary>
    [Fact]
    public void For_WritesTheAddressDownWithItsEscapesIntact()
    {
        var launch = BrowserLaunch.For(new Uri("https://example.com/a%20b?q=one%26two"), OSPlatform.Create("Linux"));

        Assert.Equal("https://example.com/a%20b?q=one%26two", launch?.Argument);
    }

    /// <summary>Whichever machine these run on, the platform it is gets a launch of its own rather than none.</summary>
    [Fact]
    public void For_AnswersForTheMachineItIsRunningOn() =>
        Assert.NotNull(BrowserLaunch.For(new Uri("https://example.com/notes")));
}
