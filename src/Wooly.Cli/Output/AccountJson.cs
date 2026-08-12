using Spectre.Console;
using Wooly.Core.Accounts;
using Wooly.Core.Paging;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a list of accounts for another program to read: <see cref="ListDocument" />'s envelope, led by which list
///     this is and whose it is. The accounts themselves are <see cref="AccountDocument" />s, spelled the one way every
///     command spells an account.
/// </summary>
internal static class AccountJson
{
    /// <summary>Writes one side of an account's follows, saying which side and whose it is.</summary>
    /// <param name="whose">
    ///     Whose list it is, so that an answer read on its own — out of a file, or a log — still says who it is about.
    /// </param>
    public static void Write(IAnsiConsole console, FollowSide side, string? whose, Fetch<Account> fetch) =>
        Write(console, side.Either("followers", "following"), whose, fetch);

    /// <summary>Writes the accounts waiting to be let in.</summary>
    /// <remarks>
    ///     The same envelope, named <c>requests</c>, and with no account named: it is a list of accounts read the same
    ///     paged way, only ever the profile's own, and a second shape for it would mean a script reading two lists of
    ///     accounts two ways.
    /// </remarks>
    public static void WriteRequests(IAnsiConsole console, Fetch<Account> fetch) =>
        Write(console, "requests", whose: null, fetch);

    private static void Write(IAnsiConsole console, string list, string? whose, Fetch<Account> fetch) =>
        ListDocument.Write(
            console,
            fetch,
            AccountDocument.Of,
            "accounts",
            ("list", list),
            ("account", whose));
}
