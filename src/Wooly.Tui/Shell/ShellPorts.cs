using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Discovery;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Search;
using Wooly.Core.Timelines;

namespace Wooly.Tui.Shell;

/// <summary>
///     Everything the shell reaches an instance through, which is the same set of ports the CLI's commands use
///     (ADR-0005). The TUI is a second front end over them and not a wrapper around the CLI, so if a screen needs
///     something none of these offers, the port is widened rather than reached past.
/// </summary>
/// <param name="Timelines">Reads a timeline, including an account's own posts.</param>
/// <param name="Author">Publishes, changes and takes down the profile's own posts.</param>
/// <param name="Engagement">Puts the three marks on a post, reads one, and reads what answered it.</param>
/// <param name="Accounts">
///     Reads an account and where the profile stands with it, changes a tie, and lists and answers the follows waiting.
/// </param>
/// <param name="Notifications">What is waiting: the rail's count, and the screen that clears it.</param>
/// <param name="Messages">
///     The conversations this profile is in: the rail's count, the list, the thread one of them opens onto, and the
///     mark a conversation carries. Nothing here sends one — a direct message is a post that went out direct, so it is
///     written through <see cref="Author" /> like anything else (ADR-0013).
/// </param>
/// <param name="Search">Finding accounts, hashtags and posts, for the screen <c>/</c> opens.</param>
/// <param name="Suggestions">
///     Who the instance offers this profile to follow, and telling it to stop offering one — the Discover
///     destination's whole fetch. A port of its own rather than a sixth call on <see cref="Accounts" />, whose own
///     doc scopes it to ties reached through one family of endpoints (ADR-0019).
/// </param>
/// <param name="RateLimit">What the instance last said is left of the budget, for the rail's foot.</param>
public sealed record ShellPorts(
    ITimelineReader Timelines,
    IPostAuthor Author,
    IPostEngagement Engagement,
    IAccountRelationships Accounts,
    INotificationInbox Notifications,
    IDirectMessages Messages,
    IInstanceSearch Search,
    IFollowSuggestions Suggestions,
    IRateLimitReport RateLimit)
{
    /// <summary>
    ///     Where the profile stands with <paramref name="people" />, or them exactly as they came where the instance
    ///     never answered — which is what draws a silent row. The silence rule, in the one place every screen that
    ///     decorates a list of people reads it from (#204).
    /// </summary>
    /// <remarks>
    ///     Named apart from <see cref="IAccountRelationships.Standing" /> rather than wrapping it under the same word:
    ///     the two are one hop apart and their null contracts are opposites, so the name says which one this is — it
    ///     never answers nothing, and what nothing became is a silent row.
    ///     <para>
    ///         Written here rather than at each of the three call sites, because the <c>?? people</c> is the half that
    ///         is easy to leave out: a caller letting the null through would draw rows saying there is no tie
    ///         because the asking failed, which is the one dishonest thing a list of people can say.
    ///     </para>
    ///     <para>
    ///         No non-empty guard, and none wanted at a call site either:
    ///         <see cref="IAccountRelationships.Standing" /> answers an empty ask with its own input before it
    ///         reaches an instance, so a list with nobody on it costs nothing by the port's own rule rather than by
    ///         one each caller remembers.
    ///     </para>
    /// </remarks>
    /// <param name="profile">Whose instance is being asked.</param>
    /// <param name="people">Whoever is to be decorated, in the order they are drawn.</param>
    /// <param name="cancellationToken">The enquiry's own token.</param>
    public async Task<IReadOnlyList<Account>> StoodOrSilent(
        ActiveProfile profile,
        IReadOnlyList<Account> people,
        CancellationToken cancellationToken) =>
        await Accounts.Standing(profile, people, cancellationToken) ?? people;
}
