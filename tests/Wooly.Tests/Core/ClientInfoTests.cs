using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;

namespace Wooly.Tests.Core;

public class ClientInfoTests
{
    [Fact]
    public void AddWoolyCore_RegistersClientInfoWithANameAndVersion()
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        var clientInfo = services.BuildServiceProvider().GetRequiredService<IClientInfo>();

        Assert.Equal("mastodon-cli", clientInfo.Name);
        Assert.NotEmpty(clientInfo.Version);
    }

    /// <summary>
    ///     This test assembly pins its <c>InformationalVersion</c> to <c>1.2.3-test+0123456789abcdef</c> so the version
    ///     read (and the stripping of the <c>+commit-sha</c> suffix the SDK appends) is observable.
    /// </summary>
    [Fact]
    public void Version_ReportsTheInformationalVersionWithoutItsBuildMetadata()
    {
        var clientInfo = new AssemblyClientInfo(typeof(ClientInfoTests).Assembly);

        Assert.Equal("1.2.3-test", clientInfo.Version);
    }
}
