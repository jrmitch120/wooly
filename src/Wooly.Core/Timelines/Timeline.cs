namespace Wooly.Core.Timelines;

/// <summary>
///     Which timeline to read. The four Mastodon offers are reached through factories rather than a constructor, so
///     that the one carrying a hashtag cannot be built without one — a tag timeline with no tag is not a thing to be
///     represented, let alone sent to an instance.
/// </summary>
public sealed record Timeline
{
    private Timeline(TimelineScope scope, string? hashtag)
    {
        Scope = scope;
        Hashtag = hashtag;
    }

    /// <summary>Which of the four this is.</summary>
    public TimelineScope Scope { get; }

    /// <summary>
    ///     The hashtag being read, without its leading <c>#</c>, or <see langword="null" /> for the other three. Always
    ///     one word, because <see cref="Tag" /> is the only way to set it — which is what makes it safe to put in a
    ///     request path (see <see cref="Timelines.Hashtag" />).
    /// </summary>
    public string? Hashtag { get; }

    /// <summary>The posts of the accounts this profile follows.</summary>
    public static Timeline Home { get; } = new(TimelineScope.Home, hashtag: null);

    /// <summary>The public posts of accounts on this profile's own instance.</summary>
    public static Timeline Local { get; } = new(TimelineScope.Local, hashtag: null);

    /// <summary>The public posts reaching this instance from everywhere it federates with.</summary>
    public static Timeline Federated { get; } = new(TimelineScope.Federated, hashtag: null);

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

        // Unreachable, and said so rather than answered with a vague phrase: a timeline this client cannot name is one
        // somebody added to the enum without coming here, which is a defect to read about, not prose to show a user.
        _ => throw new ArgumentOutOfRangeException(nameof(Scope), Scope, "Not a timeline this client reads."),
    };
}
