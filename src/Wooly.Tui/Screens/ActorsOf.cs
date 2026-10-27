using Wooly.Core.Notifications;
using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     What an actors screen lists the <b>Actors</b> of: a notification group (#455), or one side of a post as the
///     reader's instance knows it (#489). A closed pair, so that the screen holds one thing that is either rather than
///     three that could each be half of one — and so that what differs between them is said by each, once.
/// </summary>
public abstract record ActorsOf
{
    private ActorsOf()
    {
    }

    /// <summary>What the people listed are called on the screen's crumb: what they did.</summary>
    public abstract string Crumb { get; }

    /// <summary>What is said where the instance listed nobody.</summary>
    public abstract string Nobody { get; }

    /// <summary>
    ///     Why a list read to its end holding <paramref name="known" /> people is shorter than its count — or nothing
    ///     where it is not, or where there is no count to fall short of.
    /// </summary>
    public virtual ActorShortfall? ShortBy(int known) => null;

    /// <summary>
    ///     A notification group's actors: exactly the group the row counted, read from the group's own accounts.
    /// </summary>
    /// <param name="Of">The group.</param>
    public sealed record Group(NotificationGroup Of) : ActorsOf
    {
        /// <inheritdoc />
        /// <remarks>
        ///     As the kind says it: "Favorited by", "Boosted by", "Followed you", or for a kind this client has no words
        ///     for, the instance's own word.
        /// </remarks>
        public override string Crumb => KindLook.Of(Of.Kind).Crumb;

        /// <inheritdoc />
        /// <remarks>Every notification in it dismissed since the row was read.</remarks>
        public override string Nobody => "Nobody is in this group any more.";
    }

    /// <summary>One side of a post's actors: who favorited it, or who boosted it.</summary>
    /// <param name="Post">
    ///     The post, never the boost carrying it, with the counts it carried when it was asked about.
    /// </param>
    /// <param name="Side">Which of its two lists.</param>
    /// <param name="Reader">
    ///     The reader's instance, which is what listed them — and so what a list shorter than the post's count is short
    ///     on (#491).
    /// </param>
    public sealed record PostSide(Post Post, ActorSide Side, string Reader) : ActorsOf
    {
        /// <inheritdoc />
        public override string Crumb => Side.Crumb();

        /// <inheritdoc />
        /// <remarks>
        ///     A side counted above zero that reads back empty is short like any other, and says why rather than that
        ///     nobody did it — which only a side counted at zero says.
        /// </remarks>
        public override string Nobody => ShortBy(known: 0)?.Says ?? Side.Nobody();

        /// <summary>
        ///     The post's home instance, where its favorites and boosts are delivered — or nothing where it is the
        ///     reader's own.
        /// </summary>
        public string? Home => ActorShortfall.HomeOf(Post, Reader);

        /// <summary>
        ///     The post's page on its home instance, which <c>o</c> opens — where the whole of a list the reader's
        ///     instance knows only part of is (#491). Nothing on a post of the reader's own instance, there being
        ///     nowhere else to go.
        /// </summary>
        public string? Page => Home is not null ? Post.Url : null;

        /// <inheritdoc />
        public override ActorShortfall? ShortBy(int known) => ActorShortfall.Of(Post, Side, known, Reader);
    }
}
