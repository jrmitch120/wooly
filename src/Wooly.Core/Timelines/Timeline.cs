using Wooly.Core.Accounts;

namespace Wooly.Core.Timelines;

/// <summary>
///     Which timeline to read. They are reached through factories rather than a constructor, so that the ones carrying
///     a hashtag or an account cannot be built without one — a tag timeline with no tag is not a thing to be
///     represented, let alone sent to an instance.
/// </summary>
public sealed record Timeline
{
    private Timeline(TimelineScope scope, string? hashtag = null, AccountAddress? account = null)
    {
        Scope = scope;
        Hashtag = hashtag;
        Account = account;
    }

    /// <summary>Which of them this is.</summary>
    public TimelineScope Scope { get; }

    /// <summary>
    ///     The hashtag being read, without its leading <c>#</c>, or <see langword="null" /> for the other three. Always
    ///     one word, because <see cref="Tag" /> is the only way to set it — which is what makes it safe to put in a
    ///     request path (see <see cref="Timelines.Hashtag" />).
    /// </summary>
    public string? Hashtag { get; }

    /// <summary>
    ///     Whose posts are being read, or <see langword="null" /> for the four that belong to nobody in particular. An
    ///     address rather than an id for the reason <see cref="Accounts.AccountAddress" /> gives: an id means nothing
    ///     on any other instance, and turning one into the other costs a call the adapter makes.
    /// </summary>
    public AccountAddress? Account { get; }

    /// <summary>The posts of the accounts this profile follows.</summary>
    public static Timeline Home { get; } = new(TimelineScope.Home);

    /// <summary>The public posts of accounts on this profile's own instance.</summary>
    public static Timeline Local { get; } = new(TimelineScope.Local);

    /// <summary>The public posts reaching this instance from everywhere it federates with.</summary>
    public static Timeline Federated { get; } = new(TimelineScope.Federated);

    /// <summary>The public posts carrying <paramref name="hashtag" />.</summary>
    /// <param name="hashtag">The tag to read, with or without its leading <c>#</c>.</param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="hashtag" /> is not one word (<see cref="Timelines.Hashtag" />). A caller is expected to have
    ///     rejected that against the value the user gave; reaching here with one is a defect, not user error.
    /// </exception>
    public static Timeline Tag(string hashtag)
    {
        if (!Timelines.Hashtag.IsWellFormed(hashtag))
        {
            throw new ArgumentException(Timelines.Hashtag.Rejection(hashtag), nameof(hashtag));
        }

        return new Timeline(TimelineScope.Tag, Timelines.Hashtag.Bare(hashtag));
    }

    /// <summary>The posts of the account <paramref name="account" /> names.</summary>
    public static Timeline By(AccountAddress account) => new(TimelineScope.Account, account: account);

    /// <summary>
    ///     What to call this timeline in a sentence, e.g. "No posts in <em>the federated timeline</em>." A hashtag is
    ///     the user's own text, so anything rendering this has to treat it as text rather than markup.
    /// </summary>
    public string Description => Scope switch
    {
        TimelineScope.Home => "your home timeline",
        TimelineScope.Local => "your instance's local timeline",
        TimelineScope.Federated => "the federated timeline",
        TimelineScope.Tag => $"the #{Hashtag} timeline",
        TimelineScope.Account => $"the posts of @{Account}",

        // Unreachable, and said so rather than answered with a vague phrase: a timeline this client cannot name is one
        // somebody added to the enum without coming here, which is a defect to read about, not prose to show a user.
        _ => throw new ArgumentOutOfRangeException(nameof(Scope), Scope, "Not a timeline this client reads."),
    };
}
