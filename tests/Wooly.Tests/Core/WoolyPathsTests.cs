using Wooly.Core;

namespace Wooly.Tests.Core;

public class WoolyPathsTests
{
    [Fact]
    public void ForCurrentUser_PutsWoolysFilesUnderTheOsConventionalConfigFolder()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        var paths = WoolyPaths.ForCurrentUser();

        Assert.Equal(Path.Combine(applicationData, "wooly"), paths.ConfigDirectory);
    }

    /// <summary>The config file is meant to be found and hand-edited, so its name and extension are part of the contract.</summary>
    [Fact]
    public void ConfigFile_IsANamedTomlFileInsideTheConfigDirectory()
    {
        var paths = new WoolyPaths("/somewhere");

        Assert.Equal(Path.Combine("/somewhere", "config.toml"), paths.ConfigFile);
    }

    [Fact]
    public void CredentialFile_SitsBesideTheConfigFileRatherThanInsideIt()
    {
        var paths = new WoolyPaths("/somewhere");

        Assert.Equal(Path.Combine("/somewhere", "credentials.toml"), paths.CredentialFile);
    }
}
