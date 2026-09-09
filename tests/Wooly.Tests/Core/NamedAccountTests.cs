using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     How a caller names the account a read is about: the address always, and the id where somebody has already paid
///     to learn one. The whole point of the type is what it cannot say, so most of what is asserted here is the shape
///     rather than the behaviour.
/// </summary>
public class NamedAccountTests
{
    /// <summary>An address alone is what a user typed, and nobody has looked anybody up yet.</summary>
    [Fact]
    public void Addressed_CarriesTheAddressAndNoId()
    {
        var named = NamedAccount.Addressed(AccountAddress.Parse("alice@hachyderm.io"));

        Assert.Equal("alice@hachyderm.io", named.Address.Text);
        Assert.Null(named.Id);
    }

    /// <summary>An account already read supplies both halves, which is the only way an id ever gets on to one.</summary>
    [Fact]
    public void Resolved_TakesBothHalvesOffTheOneAccount()
    {
        var named = NamedAccount.Resolved(AnAccount.With(address: "ben@hachyderm.io", id: "7"));

        Assert.Equal("ben@hachyderm.io", named.Address.Text);
        Assert.Equal("7", named.Id);
    }

    /// <summary>
    ///     The failure this value exists to make unrepresentable: there is no route to an address naming one account
    ///     beside an id naming another, so neither construction path takes the two separately.
    /// </summary>
    [Fact]
    public void NamedAccount_CannotBeBuiltFromAnAddressAndAnIdThatCouldDisagree()
    {
        var taken = typeof(NamedAccount)
            .GetMethods()
            .Where(way => way.IsStatic && way.ReturnType == typeof(NamedAccount))
            .Select(way => Assert.Single(way.GetParameters()).ParameterType)
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.Empty(typeof(NamedAccount).GetConstructors());
        Assert.Equal([typeof(Account), typeof(AccountAddress)], taken);
    }

    /// <summary>Read back it is the address: nothing this client shows a user names an id.</summary>
    [Fact]
    public void ToString_SaysTheAddressTheWayAUserWouldReadItBack() =>
        Assert.Equal(
            "alice@hachyderm.io",
            NamedAccount.Addressed(AccountAddress.Parse("@alice@hachyderm.io")).ToString());
}
