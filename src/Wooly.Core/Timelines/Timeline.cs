using Wooly.Core.Accounts;

namespace Wooly.Core.Timelines;

/// <summary>
///     Which timeline to read. They are reached through factories rather than a constructor, so that the ones carrying
///     a hashtag or an account cannot be built without one — a tag timeline with no tag is not a thing to be
///     represented, let alone sent to an instance.
/// </summary>
public sealed record Timeline
{
    private Timeline(TimelineScope scope, string? hashtag = null, NamedAccount? account = null)
    {
        Scope = scope;
        Hashtag = hashtag;
        Account = account;
    }

    /// <summary>Which of them this is.</summary>
    public TimelineScope Scope { get; }

    /// <summary>
    ///     The hashtag being read, without its leading <c>#</c>, or <see langword="null" /> for the other five. Always
    ///     one word, because <see cref="Tag" /> is the only way to set it — which is what makes it safe to put in a
    ///     request path (see <see cref="Timelines.Hashtag" />).
    /// </summary>
    public string? Hashtag { get; }

    /// <summary>
    ///     Whose posts are being read, or <see langword="null" /> for the four that belong to nobody in particular.
    ///     A <see cref="NamedAccount" /> rather than a bare id for the reason <see cref="Accounts.AccountAddress" />
    ///     gives — an id means nothing on any other instance — and rather than a bare address because a caller that
    ///     has already paid for the crossing has no way to say so, and pays for it again (ADR-0012's second
    ///     amendment). Which of the two it carries is the adapter's business, not this value's.
    /// </summary>
    public NamedAccount? Account { get; }

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
    /// <param name="account">
    ///     Whose posts to read. One factory rather than an overload per argument type: two ways to say this would
    ///     leave the caller that already holds a resolution free to throw it away and pay for another.
    /// </param>
    public static Timeline By(NamedAccount account) => new(TimelineScope.Account, account: account);

    /// <summary>
    ///     The posts the account <paramref name="account" /> names has pinned to the top of their profile, replies
    ///     included — a separate reading from <see cref="By" /> rather than a filter over it, because an instance
    ///     reports a post's own pin mark only to whoever wrote it.
    /// </summary>
    public static Timeline Pinned(NamedAccount account) => new(TimelineScope.Pinned, account: account);

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
        TimelineScope.Pinned => $"the pinned posts of @{Account}",

        // Unreachable, and said so rather than answered with a vague phrase: a timeline this client cannot name is one
        // somebody added to the enum without coming here, which is a defect to read about, not prose to show a user.
        _ => throw new ArgumentOutOfRangeException(nameof(Scope), Scope, "Not a timeline this client reads."),
    };
}
