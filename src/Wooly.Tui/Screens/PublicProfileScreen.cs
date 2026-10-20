using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     The form that edits the own account's public profile (#406, ADR-0028): a screen on the stack, pushed over
///     <b>You</b> by <c>E</c> or its "edit public profile" row, opened on the public profile as the account wrote it.
/// </summary>
/// <remarks>
///     As inert as compose (ADR-0015): it holds what is being written and says what a save would send, and reaches no
///     port. The shell reads the public profile before it opens and sends what it says on <c>ctrl-s</c>.
/// </remarks>
/// <param name="read">The public profile as written, read fresh as the form was opened.</param>
public sealed class PublicProfileScreen(PublicProfile read) : Screen
{
    /// <summary>The public profile as it was read, which is what everything here is compared against.</summary>
    public PublicProfile Read => read;

    /// <inheritdoc />
    /// <remarks>
    ///     ADR-0028's words, in the sentence case every crumb is in (#216): it names what is being done, as compose's
    ///     <c>Edit</c> does.
    /// </remarks>
    public override string Crumb => "Edit public profile";

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys => [new("esc", "back")];

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing) => [];
}
