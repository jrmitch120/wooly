using System.Text.Json.Serialization;
using Spectre.Console;
using Wooly.Core.Relationships;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes a list of accounts for another program to read. An object rather than a bare array, per ADR-0007: a list
///     cut short by a rate limit and a list with nobody on it would otherwise both be <c>[]</c>, and under a pipe the
///     exit code is gone by the time the JSON is parsed. The accounts themselves are <see cref="AccountDocument" />s,
///     spelled the one way every command spells an account.
/// </summary>
internal static class AccountJson
{
    /// <summary>Writes one side of an account's follows, saying which side and whose it is.</summary>
    public static void Write(IAnsiConsole console, FollowSide side, string? whose, AccountFetch fetch) =>
        Write(console, NameOf(side), whose, fetch);

    /// <summary>Writes the accounts waiting to be let in.</summary>
    /// <remarks>
    ///     The same envelope, named <c>requests</c>, and with no account named: it is a list of accounts read the same
    ///     paged way, only ever the profile's own, and a second shape for it would mean a script reading two lists of
    ///     accounts two ways.
    /// </remarks>
    public static void WriteRequests(IAnsiConsole console, AccountFetch fetch) =>
        Write(console, "requests", whose: null, fetch);

    private static void Write(IAnsiConsole console, string list, string? whose, AccountFetch fetch)
    {
        JsonOutput.Write(
            console,
            new AccountListDocument(
                list,
                whose,
                fetch.IsComplete,
                RateLimitDocument.Of(fetch.StoppedBy),
                fetch.Accounts.Select(AccountDocument.Of).ToList()));
    }

    /// <summary>
    ///     What each side is called in the output. Spelled out for the same reason the field names are: derived from the
    ///     enum's own member names, renaming one would silently change a value somebody is matching on.
    /// </summary>
    private static string NameOf(FollowSide side) => side switch
    {
        FollowSide.Followers => "followers",
        FollowSide.Following => "following",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a side of a follow this client lists."),
    };

    /// <param name="List">Which list this is: <c>followers</c>, <c>following</c>, or the pending <c>requests</c>.</param>
    /// <param name="Account">
    ///     Whose list it is, so that an answer read on its own — out of a file, or a log — still says who it is about.
    ///     Absent for the pending requests, which are only ever the profile's own.
    /// </param>
    /// <param name="Complete">
    ///     Whether every account asked for was read. False says the rest was cut short, which an empty
    ///     <c>accounts</c> otherwise could not be told from a list with nobody on it.
    /// </param>
    private sealed record AccountListDocument(
        [property: JsonPropertyName("list")] string List,
        [property: JsonPropertyName("account")] string? Account,
        [property: JsonPropertyName("complete")] bool Complete,
        [property: JsonPropertyName("rateLimit")] RateLimitDocument? RateLimit,
        [property: JsonPropertyName("accounts")] IReadOnlyList<AccountDocument> Accounts);
}
