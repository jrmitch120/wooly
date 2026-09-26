using Wooly.Tui.Screens;

namespace Wooly.Tui.Shell;

/// <summary>
///     What one read of a <see cref="Subject" /> came back with, and what that is on screen — which is everything
///     <see cref="Arrival" /> needs to know about an answer without knowing what kind of thing was read (#233).
/// </summary>
/// <remarks>
///     The answer carries its own screen rather than the subject being handed the answer back, so that what a read
///     came back with and what it becomes are one value: a follow list's fill needs how many were asked for as well as
///     who came back, and a destination's list needs which of its five kinds of thing it is.
/// </remarks>
internal abstract record Found
{
    /// <summary>
    ///     The screen this answer becomes: a new one, or <paramref name="standing" /> with the answer read into it —
    ///     which is then already up, and is not put up again.
    /// </summary>
    /// <param name="standing">The screen already up for the subject where one is, or <see langword="null" />.</param>
    public abstract Screen Becomes(Screen? standing);

    /// <summary>What the rail's badge says off this answer, for the one kind of subject that carries a badge.</summary>
    public virtual Badge? Counted => null;

    /// <summary>
    ///     What of this answer is worth handing back the next time the subject is reached, or <see langword="null" />
    ///     where none of it is. Asked only of a subject that is <see cref="Subject.Cached" />.
    /// </summary>
    /// <param name="screen">The screen the answer became.</param>
    public virtual Found? Held(Screen screen) => null;

    /// <summary>
    ///     Whether the rest of the subject is to be read at once, behind the reader, rather than waiting for them to
    ///     walk onto the end of what arrived.
    /// </summary>
    /// <param name="screen">The screen the answer became.</param>
    public virtual bool ReadsOn(Screen screen) => false;

    /// <summary>
    ///     Whether anything in this answer is the post <paramref name="postId" /> names, or a boost of it — which is
    ///     what a post changing or going makes stale (<see cref="Change" />).
    /// </summary>
    public virtual bool HoldsPost(string postId) => false;

    /// <summary>Whether anybody in this answer is the account <paramref name="accountId" /> names.</summary>
    public virtual bool HoldsAccount(string accountId) => false;

    /// <summary>What a rail badge says, and which destination it is on.</summary>
    public sealed record Badge(DestinationKind Kind, int Unread);

    /// <summary>An answer that is a screen and nothing more, which is what most subjects are read into.</summary>
    /// <param name="Build">What the subject builds its screen with, off what came back.</param>
    public sealed record Drawn(Func<Screen> Build) : Found
    {
        /// <inheritdoc />
        public override Screen Becomes(Screen? standing) => Build();
    }
}
