using Wooly.Core.Posts;

namespace Wooly.Cli.Commands;

/// <summary>
///     Who a post being composed should reach, and where that answer came from.
/// </summary>
/// <param name="Visibility">
///     The audience, or <see langword="null" /> to leave the choice to the account's own setting on the instance.
/// </param>
/// <param name="Chosen">
///     Whether this invocation settled it, rather than inheriting a standing preference from the config file. The
///     difference is what lets a reply narrow a preference that is too wide for the post it answers while refusing a
///     <c>--visibility</c> that is — see <see cref="PostDraft.VisibilityChosen" />.
/// </param>
internal sealed record ComposedVisibility(PostVisibility? Visibility, bool Chosen);
