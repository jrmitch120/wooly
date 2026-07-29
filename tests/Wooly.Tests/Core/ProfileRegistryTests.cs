using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     The registry is the only place the two stores of ADR-0003 meet, so it is exercised over both of them for real —
///     a TOML config file and a token store — in a scratch directory. Faking either one here would leave the thing
///     under test, that a profile is half config and half secret, untested.
/// </summary>
public class ProfileRegistryTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Add_MakesTheFirstProfileTheOneCommandsDefaultTo()
    {
        var registry = NewRegistry();

        var addition = registry.Add("personal", Pointing("mastodon.social"), "token-personal");

        Assert.True(addition.IsCurrent);
        Assert.Equal("personal", registry.Resolve(null).Name);
    }

    /// <summary>
    ///     Adding a second account is not a request to start using it — that is what <c>profile switch</c> is for.
    /// </summary>
    [Fact]
    public void Add_LeavesTheCurrentProfileAloneOnceOneIsChosen()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");

        var addition = registry.Add("work", Pointing("hachyderm.io"), "token-work");

        Assert.False(addition.IsCurrent);
        Assert.Equal("personal", registry.Resolve(null).Name);
    }

    /// <summary>The config file is the half a user is invited to read and hand-edit, so no secret may reach it.</summary>
    [Fact]
    public void Add_KeepsTheAccessTokenOutOfTheConfigFile()
    {
        NewRegistry().Add("personal", Pointing("mastodon.social"), "token-personal");

        Assert.DoesNotContain("token-personal", File.ReadAllText(Path.Combine(_directory.Path, "config.toml")));
    }

    /// <summary>
    ///     Re-adding a name is how a profile whose token was revoked gets a working one again, so it replaces rather
    ///     than refuses — but it says that it did, because the same keystrokes are also how someone overwrites the
    ///     wrong profile by accident.
    /// </summary>
    [Fact]
    public void Add_ReplacesAProfileOfTheSameNameAndSaysThatItDid()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-old");

        var addition = registry.Add("personal", Pointing("mastodon.social"), "token-new");

        Assert.True(addition.ReplacedExisting);
        Assert.Equal("token-new", registry.Resolve(null).AccessToken);
    }

    [Fact]
    public void Resolve_HandsBackWhereTheProfilePointsAndTheTokenToCallWith()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social", "jeff@mastodon.social"), "token-personal");

        var profile = registry.Resolve(null);

        Assert.Equal("personal", profile.Name);
        Assert.Equal("mastodon.social", profile.Instance);
        Assert.Equal("jeff@mastodon.social", profile.Account);
        Assert.Equal("token-personal", profile.AccessToken);
    }

    /// <summary>The <c>--profile</c> override: this invocation only, with nothing about the default disturbed.</summary>
    [Fact]
    public void Resolve_ActsAsTheNamedProfileWithoutMakingItTheDefault()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");
        registry.Add("work", Pointing("hachyderm.io"), "token-work");

        var profile = registry.Resolve("work");

        Assert.Equal("hachyderm.io", profile.Instance);
        Assert.Equal("token-work", profile.AccessToken);
        Assert.Equal("personal", NewRegistry().Resolve(null).Name);
    }

    /// <summary>Two accounts on two instances is the whole reason profiles are named rather than assumed.</summary>
    [Fact]
    public void Resolve_KeepsEachProfilePointingAtItsOwnAccount()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social", "jeff@mastodon.social"), "token-personal");
        registry.Add("work", Pointing("hachyderm.io", "jeff@hachyderm.io"), "token-work");

        Assert.Equal("jeff@mastodon.social", registry.Resolve("personal").Account);
        Assert.Equal("jeff@hachyderm.io", registry.Resolve("work").Account);
    }

    [Fact]
    public void Resolve_ReportsTheProfilesItDoesKnowWhenAskedForOneItDoesNot()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");

        var exception = Assert.Throws<UnknownProfileException>(() => registry.Resolve("wrok"));

        Assert.Contains("wrok", exception.Message);
        Assert.Contains("personal", exception.Message);
    }

    [Fact]
    public void Resolve_ReportsThatNothingIsSetUpYetOnAMachineWithNoProfiles()
    {
        var exception = Assert.Throws<AuthenticationException>(() => NewRegistry().Resolve(null));

        Assert.Contains("profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     A profile in the config with no token behind it is what a hand-emptied keyring leaves, and it is an
    ///     authentication problem rather than a missing profile: the profile is right there.
    /// </summary>
    [Fact]
    public void Resolve_ReportsAProfileWhoseTokenIsGone()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");
        NewCredentialStore().DeleteAccessToken("personal");

        var exception = Assert.Throws<AuthenticationException>(() => registry.Resolve(null));

        Assert.Contains("personal", exception.Message);
    }

    /// <summary>
    ///     The config file is hand-editable, so it can name a current profile that was never set up. That is a problem
    ///     with the file, and the report says which file and what is wrong with it.
    /// </summary>
    [Fact]
    public void Resolve_ReportsAConfigFileNamingACurrentProfileThatDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_directory.Path, "config.toml"), "current_profile = \"work\"");

        var exception = Assert.Throws<ConfigurationException>(() => NewRegistry().Resolve(null));

        Assert.Contains("config.toml", exception.Message);
        Assert.Contains("work", exception.Message);
    }

    [Fact]
    public void Switch_ChangesTheProfileLaterCommandsDefaultTo()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");
        registry.Add("work", Pointing("hachyderm.io"), "token-work");

        registry.Switch("work");

        Assert.Equal("work", NewRegistry().Resolve(null).Name);
    }

    [Fact]
    public void Switch_RefusesAProfileThatWasNeverSetUpAndLeavesTheCurrentOneAlone()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social"), "token-personal");

        Assert.Throws<UnknownProfileException>(() => registry.Switch("work"));
        Assert.Equal("personal", NewRegistry().Resolve(null).Name);
    }

    [Fact]
    public void List_ReportsEveryProfileAndWhichOneIsCurrent()
    {
        var registry = NewRegistry();
        registry.Add("personal", Pointing("mastodon.social", "jeff@mastodon.social"), "token-personal");
        registry.Add("work", Pointing("hachyderm.io", "jeff@hachyderm.io"), "token-work");
        registry.Switch("work");

        var profiles = NewRegistry().List();

        Assert.Equal(["personal", "work"], profiles.Select(profile => profile.Name));
        Assert.Equal(["mastodon.social", "hachyderm.io"], profiles.Select(profile => profile.Instance));
        Assert.Equal([false, true], profiles.Select(profile => profile.IsCurrent));
    }

    [Fact]
    public void List_ReadsAsEmptyOnAMachineThatHasNeverSetUpAProfile()
    {
        Assert.Empty(NewRegistry().List());
    }

    /// <summary>Which store the tokens landed in is a tradeoff a front end has to be able to show (ADR-0003).</summary>
    [Fact]
    public void TokenStorage_ReportsWhereTheTokensAreBeingKept()
    {
        Assert.Equal(CredentialStorage.PlaintextFile, NewRegistry().TokenStorage);
    }

    /// <summary>
    ///     The rule lives here rather than only in the command that types it, so that the next way of making a profile
    ///     — OAuth, or the TUI — cannot store an address this client could never reach.
    /// </summary>
    [Theory]
    [InlineData("https://mastodon.social")]
    [InlineData("mastodon.social/api")]
    [InlineData("")]
    public void Add_RefusesAnInstanceThatIsNotABareDomain(string instance)
    {
        var registry = NewRegistry();

        Assert.Throws<ArgumentException>(() => registry.Add("personal", Pointing(instance), "token-personal"));
        Assert.Empty(NewRegistry().List());
    }

    /// <summary>A port is an address this client has to reach: ADR-0005's integration instance is one.</summary>
    [Fact]
    public void Add_AcceptsAnInstanceOnAPort()
    {
        NewRegistry().Add("local", Pointing("localhost:3000"), "token-local");

        Assert.Equal("localhost:3000", NewRegistry().Resolve(null).Instance);
    }

    /// <summary>
    ///     The config file is meant to be hand-edited (ADR-0003), so a URL can arrive in it without ever passing
    ///     through <c>Add</c>. Said here, it names the file to go and fix rather than failing later as a network error.
    /// </summary>
    [Fact]
    public void Resolve_ReportsAHandEditedInstanceThatIsNotABareDomainAgainstTheConfigFile()
    {
        NewRegistry().Add("personal", Pointing("mastodon.social"), "token-personal");

        var configFile = Path.Combine(_directory.Path, "config.toml");
        File.WriteAllText(
            configFile,
            File.ReadAllText(configFile).Replace("mastodon.social", "https://mastodon.social"));

        var exception = Assert.Throws<ConfigurationException>(() => NewRegistry().Resolve(null));

        Assert.Contains("config.toml", exception.Message);
        Assert.Contains("personal", exception.Message);
    }

    private static ProfileConfig Pointing(string instance, string? account = null) =>
        new() { Instance = instance, Account = account };

    /// <summary>
    ///     A registry over the scratch directory. Built fresh per call so that a test can prove something reached the
    ///     disk rather than a field.
    /// </summary>
    private ProfileRegistry NewRegistry()
    {
        var paths = new WoolyPaths(_directory.Path);

        return new ProfileRegistry(new TomlConfigStore(paths), NewCredentialStore(), paths);
    }

    private PlaintextFileCredentialStore NewCredentialStore() => new(new WoolyPaths(_directory.Path));
}
