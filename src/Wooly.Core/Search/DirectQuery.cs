using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using HashtagRule = Wooly.Core.Timelines.Hashtag;

namespace Wooly.Core.Search;

/// <summary>
///     What is typed into search when it names one thing rather than asks after something, and so goes straight to
///     the thing it names instead of listing results (<c>CONTEXT.md</c>).
/// </summary>
/// <remarks>
///     A front end's rule rather than the search's: what is typed still searches for exactly what it says wherever a
///     caller asks it to, which is why the CLI's <c>search</c> never consults this.
/// </remarks>
public static class DirectQuery
{
    /// <summary>
    ///     The tag <paramref name="typed" /> names, where it is a <c>#</c> followed by one well-formed tag word — or
    ///     <see langword="null" /> where it is anything else, and is searched for as typed.
    /// </summary>
    /// <remarks>
    ///     Well-formed by the rule a tag timeline already takes a tag by (<see cref="HashtagRule" />), so that
    ///     what opens directly is exactly what could have been opened at all. Exactly one <c>#</c>: that rule would
    ///     take <c>##cats</c> for <c>cats</c>, and a reader who typed two meant something other than the tag.
    /// </remarks>
    public static string? Tag(string typed)
    {
        var text = typed.Trim();

        return text.Length > 1 && text[0] == '#' && text[1] != '#' && HashtagRule.IsWellFormed(text)
            ? HashtagRule.Bare(text)
            : null;
    }

    /// <summary>
    ///     The account <paramref name="typed" /> names, where it is a handle — <c>@alice</c>, <c>@alice@host</c> or
    ///     <c>alice@host</c> — or <see langword="null" /> where it is anything else.
    /// </summary>
    /// <remarks>
    ///     An <c>@</c> somewhere is what makes it one: a bare <c>alice</c> is how a reader asks for everything that
    ///     resembles it. Well-formed by the rule a tie is put on an account by (<see cref="AccountAddress" />), so one
    ///     or two parts and no whitespace — and no <c>/</c> or <c>:</c>, which a handle never holds and an address
    ///     with an <c>@</c> in its path always does.
    /// </remarks>
    public static AccountAddress? Handle(string typed)
    {
        var text = typed.Trim();

        return text.Contains('@')
               && text[0] != '#'
               && !text.Contains('/')
               && !text.Contains(':')
               && AccountAddress.IsWellFormed(text)
            ? AccountAddress.Parse(text)
            : null;
    }

    /// <summary>
    ///     The page <paramref name="typed" /> names, where it is a single <c>http</c> or <c>https</c> address and
    ///     nothing else — or <see langword="null" /> where it is anything else.
    /// </summary>
    /// <remarks>
    ///     Read the way opening a link reads one (<see cref="BrowserLaunch.Address" />), so the elided form an instance
    ///     writes an address in counts too and is read as <c>https</c>. Elided, it has to look like a page and not a
    ///     word with a dot in it: a dotted host with a path after it and nobody's name before it. <c>node.js</c> and
    ///     <c>3.14</c> are searched for, and lose nothing by it — a post or a profile always has a path, so a bare
    ///     host could never have been opened anyway.
    /// </remarks>
    public static Uri? Address(string typed)
    {
        var text = typed.Trim();

        if (text.Length == 0 || text.Any(char.IsWhiteSpace) || text[0] is '#' or '@')
        {
            return null;
        }

        if (BrowserLaunch.Address(text) is not { } address)
        {
            return null;
        }

        var schemed = text.StartsWith($"{Uri.UriSchemeHttp}://", StringComparison.OrdinalIgnoreCase)
                      || text.StartsWith($"{Uri.UriSchemeHttps}://", StringComparison.OrdinalIgnoreCase);

        return schemed || (address.UserInfo.Length == 0 && address.Host.Contains('.') && address.AbsolutePath != "/")
            ? address
            : null;
    }

    /// <summary>
    ///     What is asked of the instance for <paramref name="typed" />: an address in full, so that an elided one is
    ///     resolved as the page it names rather than searched for as words — and anything else as it was typed.
    /// </summary>
    public static string Asking(string typed) => Address(typed) is { } address ? WebAddress.Of(address) : typed.Trim();

    /// <summary>
    ///     What <paramref name="typed" /> names among what its search <paramref name="found" />, where it is a handle
    ///     or an address and the search turned up exactly that — or <see langword="null" />, and the results are
    ///     listed like any other.
    /// </summary>
    /// <remarks>
    ///     Only ever an exact match, since opening somebody else on a near miss is not a mistake worth being helpful
    ///     about. A handle opens the account whose full address it is, by the rule a tie is put on one — a bare handle
    ///     read against <paramref name="instance" />, and case not counting. An address opens the one thing it
    ///     resolved to: an instance asked to resolve an address answers with what it names and nothing else, or with
    ///     nothing at all, so an answer holding one post or one account is the page, and one holding anything more was
    ///     not resolved from it. Matching the address against the post's own instead would turn away every post read
    ///     through another instance's address for it, which is the page a reader most often has open.
    /// </remarks>
    /// <param name="instance">The profile's own instance, which is the one a bare handle belongs to.</param>
    public static Named? Among(string typed, SearchResults found, string instance)
    {
        if (Handle(typed) is { } handle)
        {
            var wanted = handle.On(instance);
            var named = (found.Accounts ?? []).FirstOrDefault(
                account => string.Equals(account.Address, wanted, StringComparison.OrdinalIgnoreCase));

            return named is null ? null : new Named.OfAccount(named);
        }

        if (Address(typed) is null)
        {
            return null;
        }

        IReadOnlyList<Account> accounts = found.Accounts ?? [];
        IReadOnlyList<Post> posts = found.Posts ?? [];

        if (accounts.Count + posts.Count + (found.Hashtags ?? []).Count != 1)
        {
            return null;
        }

        return accounts.Count == 1 ? new Named.OfAccount(accounts[0])
            : posts.Count == 1 ? new Named.OfPost(posts[0])
            : null;
    }

    /// <summary>The one thing a handle or an address named, which is what is opened instead of listing results.</summary>
    public abstract record Named
    {
        /// <remarks>Closed, so that what a direct query can open is what a front end knows how to open.</remarks>
        private Named()
        {
        }

        /// <summary>An account, named by its handle or by its profile's address.</summary>
        public sealed record OfAccount(Account Account) : Named;

        /// <summary>A post, named by its address.</summary>
        public sealed record OfPost(Post Post) : Named;
    }
}
