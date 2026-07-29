using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;

namespace Wooly.Tests.Core;

public class StorageRegistrationTests
{
    [Fact]
    public void AddWoolyCore_PointsAtTheCurrentUsersOwnConfigDirectory()
    {
        var paths = Resolve<WoolyPaths>();

        Assert.Equal(WoolyPaths.ForCurrentUser().ConfigDirectory, paths.ConfigDirectory);
    }

    [Fact]
    public void AddWoolyCore_RegistersConfigAsTheTomlFile()
    {
        Assert.IsType<TomlConfigStore>(Resolve<IConfigStore>());
    }

    /// <summary>
    ///     Resolving the credential store must not go near the machine's keyring — that choice waits until a token is
    ///     actually wanted, so a command that needs none never risks a keyring prompt or a hang (ADR-0003).
    /// </summary>
    [Fact]
    public void AddWoolyCore_RegistersCredentialsWithoutOpeningTheKeyringToDoIt()
    {
        Assert.IsType<FallbackCredentialStore>(Resolve<ICredentialStore>());
    }

    private static T Resolve<T>() where T : notnull
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        return services.BuildServiceProvider().GetRequiredService<T>();
    }
}
