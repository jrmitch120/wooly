using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Accounts;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the two timeline commands about a person take: whose posts to read, on top of everything every timeline
///     takes. Declared once rather than nested in each command, because <c>account</c> and <c>pinned</c> name the same
///     account the same way — two copies would be two chances for one of them to accept an address the other turns down.
/// </summary>
internal class TimelineAccountSettings : TimelineSettings
{
    [CommandArgument(0, "<ACCOUNT>")]
    [Description("Whose posts to read, as user@instance — or a bare username for somebody on your own instance.")]
    public string Account { get; init; } = string.Empty;

    /// <summary>
    ///     Whose posts these are, named as an instance would name somebody on another server: a bare username is
    ///     resolved against <paramref name="instance" /> here, before the <see cref="Core.Timelines.Timeline" /> is
    ///     built, so that what is read, what is reported and what <c>--json</c> writes are one full
    ///     <c>user@host</c> — an <c>@maria</c> in a saved file says nothing about which server it came from.
    /// </summary>
    /// <param name="instance">The instance being asked, which is the one a bare username belongs to.</param>
    /// <remarks>
    ///     Addressed and never resolved: a command line is an address a user typed and nothing has been looked up yet,
    ///     so the read pays for the crossing itself exactly as every other CLI read of a named account does (ADR-0012).
    /// </remarks>
    public NamedAccount Whose(string instance) =>
        NamedAccount.Addressed(AccountAddress.Parse(Address.On(instance)));

    /// <summary>
    ///     The account named, which <see cref="Validate" /> has already established is one — an address that is not
    ///     cannot reach here.
    /// </summary>
    private AccountAddress Address => AccountAddress.Parse(Account);

    public override ValidationResult Validate()
    {
        var shared = base.Validate();

        if (!shared.Successful)
        {
            return shared;
        }

        // Asked here as well as in the domain, the way the tag command asks about a hashtag: a name no instance could be
        // looked up by is a usage error against the value the user just typed, rather than a defect further in.
        return AccountAddress.IsWellFormed(Account)
            ? ValidationResult.Success()
            : ValidationResult.Error(AccountAddress.Rejection(Account));
    }
}
