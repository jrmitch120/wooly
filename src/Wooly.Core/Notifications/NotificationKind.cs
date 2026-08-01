namespace Wooly.Core.Notifications;

/// <summary>
///     What somebody did that the instance thought worth telling this account about. Four of them this client has its
///     own word for — a mention, a follow, a boost, a favorite (CONTEXT.md) — and those four are what #24 asks for.
///     <para>
///         A kind is a word rather than an enum member because Mastodon keeps adding kinds, and an instance may send one
///         this client has never heard of: a poll that ended, an edited post, a moderation warning. Dropping those would
///         hide notifications the account really has, from a list whose whole job is to say what is waiting. So an
///         unrecognized kind is kept under the instance's own word, through <see cref="Reported" />, and a caller that
///         has words for only these four can still say something true about the rest.
///     </para>
/// </summary>
public sealed record NotificationKind
{
    private static readonly Dictionary<string, string> Sentences = new(StringComparer.Ordinal)
    {
        ["mention"] = "mentioned you",
        ["follow"] = "followed you",
        ["boost"] = "boosted your post",
        ["favorite"] = "favorited your post",
    };

    private NotificationKind(string name) => Name = name;

    /// <summary>
    ///     What to call this kind: one of the four below, or — for anything else — whatever the instance called it.
    ///     Machine-readable output writes this, so a script sees a word for every notification rather than a hole.
    /// </summary>
    public string Name { get; }

    /// <summary>Somebody named this account in a post.</summary>
    public static NotificationKind Mention { get; } = new("mention");

    /// <summary>Somebody started following this account.</summary>
    public static NotificationKind Follow { get; } = new("follow");

    /// <summary>Somebody re-shared one of this account's posts. The wire calls this a <c>reblog</c>.</summary>
    public static NotificationKind Boost { get; } = new("boost");

    /// <summary>Somebody marked one of this account's posts as liked. The wire spells this <c>favourite</c>.</summary>
    public static NotificationKind Favorite { get; } = new("favorite");

    /// <summary>
    ///     A kind this client does not name, kept under the instance's own word for it. Not translated into this
    ///     project's vocabulary, because there is nothing to translate it from — a word this client has never seen is a
    ///     word it has no term for, and inventing one would be guessing at what an instance meant.
    /// </summary>
    /// <param name="reported">What the instance called it, e.g. <c>poll</c> or <c>admin.report</c>.</param>
    public static NotificationKind Reported(string reported) => new(reported);

    /// <summary>
    ///     What the account did, said the way this project says it, for a front end writing a sentence about it —
    ///     <c>Alice mentioned you</c>.
    /// </summary>
    /// <remarks>
    ///     One table, here rather than one per front end, so that the four kinds this client names cannot come to be
    ///     described in more than four ways — and so that a kind it has no word for is described the same way in the
    ///     CLI's report and on the TUI's inbox, rather than each inventing a phrasing for it.
    /// </remarks>
    public string Does => Sentences.GetValueOrDefault(Name, $"notified you ({Name})");
}
