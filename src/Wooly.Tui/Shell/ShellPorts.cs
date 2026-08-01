using Wooly.Core.Conversations;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
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
/// <param name="Accounts">Reads an account and where the profile stands with it.</param>
/// <param name="Notifications">What is waiting, for the rail's count.</param>
/// <param name="Messages">The conversations this profile is in, for the rail's count.</param>
/// <param name="RateLimit">What the instance last said is left of the budget, for the rail's foot.</param>
public sealed record ShellPorts(
    ITimelineReader Timelines,
    IPostAuthor Author,
    IPostEngagement Engagement,
    IAccountRelationships Accounts,
    INotificationInbox Notifications,
    IDirectMessages Messages,
    IRateLimitReport RateLimit);
