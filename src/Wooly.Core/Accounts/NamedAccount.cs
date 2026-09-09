namespace Wooly.Core.Accounts;

/// <summary>
///     How a caller names the account it means: the <see cref="AccountAddress" /> always, and the instance's own id for
///     it where whoever is asking has already paid the call to learn one. One value rather than an address and an id
///     side by side, because two fields can disagree, and an address naming one account beside an id naming another is
///     not a thing to be represented.
///     <para>
///         So it is built either from an address alone (<see cref="Addressed" />) or from an
///         <see cref="Account" /> already read (<see cref="Resolved" />), which supplies both halves from the one
///         record. There is no constructor taking the two separately: that is the failure this value exists to make
///         unrepresentable.
///     </para>
/// </summary>
/// <remarks>
///     Only a <em>read</em> is ever handed one. Every write resolves the address itself, however well the caller thinks
///     it knows the answer — an id cached across enquiries is a way to act on the wrong account after somebody moves
///     instances, which ADR-0012 called out and its second amendment keeps ruled out.
/// </remarks>
public sealed record NamedAccount
{
    private NamedAccount(AccountAddress address, string? id)
    {
        Address = address;
        Id = id;
    }

    /// <summary>Where the account is addressed, which every named account carries and no lookup is needed to know.</summary>
    public AccountAddress Address { get; }

    /// <summary>
    ///     The instance's own id for the account, or <see langword="null" /> where nobody has paid to learn one yet. An
    ///     adapter handed one skips the resolving search it would otherwise make; handed nothing, it makes it.
    /// </summary>
    public string? Id { get; }

    /// <summary>The account <paramref name="address" /> names, with nothing yet known of the id an instance has for it.</summary>
    public static NamedAccount Addressed(AccountAddress address) => new(address, id: null);

    /// <summary>
    ///     The account <paramref name="account" /> is, both halves off the one record — which is what makes them
    ///     incapable of naming two different people.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="account" /> is addressed in a way no instance could be asked about
    ///     (<see cref="AccountAddress.Parse" />). Every account here was read off an instance, so reaching this is a
    ///     defect rather than user error.
    /// </exception>
    public static NamedAccount Resolved(Account account) =>
        new(AccountAddress.Parse(account.Address), account.Id);

    /// <summary>The account as the user would read it back, which is the address: nothing shows an id.</summary>
    public override string ToString() => Address.Text;
}
