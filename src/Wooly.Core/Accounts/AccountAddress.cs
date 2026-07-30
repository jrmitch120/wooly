namespace Wooly.Core.Accounts;

/// <summary>
///     How a user names somebody else's account: <c>username@instance</c>, or a bare <c>username</c> for somebody on
///     the profile's own instance, with or without the leading <c>@</c> Mastodon shows a handle with. Reached through
///     <see cref="Parse" /> rather than a constructor so that a name an instance could not look up cannot be built —
///     the same shape <see cref="Search.SearchQuery" /> has, and for the same reason.
/// </summary>
/// <remarks>
///     What this is not is an account id. Mastodon's relationship endpoints all take an id, and the crossing from the
///     address a user types to the id an instance knows costs a call — which is the adapter's business, not this
///     value's.
/// </remarks>
public sealed record AccountAddress
{
    private AccountAddress(string text) => Text = text;

    /// <summary>The address as the user meant it: trimmed, and without the leading <c>@</c> if they typed one.</summary>
    public string Text { get; }

    /// <summary>
    ///     How a name that is not an address is described, shared so that a command turning one down and the domain
    ///     turning one down cannot say different things about the same word.
    /// </summary>
    public static string Rejection(string? typed) =>
        $"'{typed}' is not an account. Name one as user@instance, e.g. alice@mastodon.social.";

    /// <summary>Whether <paramref name="typed" /> names an account an instance could be asked about.</summary>
    public static bool IsWellFormed(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return false;
        }

        var parts = Handle(typed).Split('@');

        // A handle has to survive being put in a query string and compared against what an instance answers with, so
        // the two ways it can fail to are refused here: whitespace inside it, and any number of @ but one.
        return parts.Length <= 2
               && parts.All(part => part.Length > 0 && !part.Any(char.IsWhiteSpace));
    }

    /// <summary>The account <paramref name="typed" /> names.</summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="typed" /> does not name one. A caller is expected to have rejected that against the value the
    ///     user gave; reaching here with one is a defect, not user error.
    /// </exception>
    public static AccountAddress Parse(string typed)
    {
        if (!IsWellFormed(typed))
        {
            throw new ArgumentException(Rejection(typed), nameof(typed));
        }

        return new AccountAddress(Handle(typed));
    }

    /// <summary>
    ///     The address in full, as an instance names accounts that are not its own — which is what a bare username has
    ///     to become before it can be matched against one.
    /// </summary>
    /// <param name="instance">The instance being asked, which is the one a bare username belongs to.</param>
    public string On(string instance) => Text.Contains('@') ? Text : $"{Text}@{instance}";

    /// <summary>The address as the user would read it back, which is what every report of one shows.</summary>
    public override string ToString() => Text;

    private static string Handle(string typed) => typed.Trim().TrimStart('@');
}
