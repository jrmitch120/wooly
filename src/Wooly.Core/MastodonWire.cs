using Mastonet.Entities;

namespace Wooly.Core;

/// <summary>
///     The crossings from Mastodon's wire shapes that more than one mapper needs. They started inside
///     <see cref="Posts.PostWire" /> and moved here when notifications turned out to name an account and stamp a time the
///     same way a post does — an account written one way on a timeline and another in a notification would look like two
///     accounts.
/// </summary>
internal static class MastodonWire
{
    /// <summary>
    ///     An instance names its own accounts by bare username and everyone else's by <c>username@instance</c>. Any
    ///     list mixes the two, so the bare ones are qualified here — otherwise two lines side by side would say who
    ///     they are about in two different ways.
    /// </summary>
    /// <param name="instance">The instance being read, which is the one whose accounts arrive unqualified.</param>
    public static string Qualify(Account account, string instance) => Qualify(account.AccountName, instance);

    /// <summary>
    ///     The same qualifying <see cref="Qualify(Account,string)" /> does, for an <c>acct</c> read off anything else
    ///     that names an account — a mention, not just a status's own.
    /// </summary>
    public static string Qualify(string acct, string instance) => acct.Contains('@') ? acct : $"{acct}@{instance}";

    /// <summary>The name an account chose to be shown as, falling back to its username where it chose none.</summary>
    public static string DisplayName(Account account) =>
        string.IsNullOrWhiteSpace(account.DisplayName) ? account.UserName : account.DisplayName;

    /// <summary>
    ///     Mastodon timestamps everything in UTC. A parser that hands one back as <see cref="DateTimeKind.Unspecified" />
    ///     is still handing back UTC, so it is read as such rather than as this machine's local time.
    /// </summary>
    public static DateTimeOffset AsUtc(DateTime moment) => moment.Kind switch
    {
        DateTimeKind.Unspecified => new DateTimeOffset(moment, TimeSpan.Zero),
        _ => new DateTimeOffset(moment.ToUniversalTime(), TimeSpan.Zero),
    };
}
