using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Every <b>Actor</b> in one notification group — everyone who favorited the post, boosted it or followed the
///     reader, where the row on Notifications names two and counts the rest (#455) — or one side of a post's actors:
///     everyone who favorited it, or everyone who boosted it (#489). Drawn as Account blocks the way Followers is, and
///     an ordinary screen on the stack, so opening somebody and walking back out works as it does there.
/// </summary>
/// <remarks>
///     One screen for both rather than two, because what is on it is the same: people, opened by <c>⏎</c>, read on as
///     the page nears the end and read again by <c>g</c>. What differs is what it is about — which settles the crumb,
///     what an empty list says, and whether <c>s</c> has another side to swap to, which only a post's actors have.
///     <para>
///         Read from the group's own accounts (<see cref="Subject.Actors" />) or the post's own side
///         (<see cref="Subject.PostActors" />). Nothing here fetches: what is read arrives through
///         <see cref="Arrived" />, and the rest through <see cref="ReadOn" /> as the page nears the end of what is in
///         hand (#467).
///     </para>
/// </remarks>
public sealed class ActorsScreen : Screen, IReadsOn<Account>
{
    private readonly Picked<Account> _people = new([]);

    /// <summary>The actors of <paramref name="of" />: a notification group, or one side of a post.</summary>
    public ActorsScreen(ActorsOf of) => Of = of;

    /// <summary>The actors of the notification group <paramref name="group" />.</summary>
    public ActorsScreen(NotificationGroup group)
        : this(new ActorsOf.Group(group))
    {
    }

    /// <summary>
    ///     The actors on <paramref name="side" /> of <paramref name="post" />, as <paramref name="reader" /> knows them.
    /// </summary>
    /// <inheritdoc cref="ActorsOf.PostSide" />
    public ActorsScreen(Post post, ActorSide side, string reader)
        : this(new ActorsOf.PostSide(post, side, reader))
    {
    }

    /// <summary>What these are the actors of.</summary>
    public ActorsOf Of { get; }

    /// <summary>Everyone read so far, in the order the instance listed them, newest first.</summary>
    public IReadOnlyList<Account> People => _people.All;

    /// <summary>How many have been read.</summary>
    public int Read => _people.Count;

    /// <summary>Whoever is picked out, or nobody. What <c>⏎</c> opens.</summary>
    public Account? PickedPerson => _people.Out;

    /// <inheritdoc />
    /// <remarks>What they did, as <see cref="Of" /> says it: "Favorited by", "Followed you".</remarks>
    public override string Crumb => Of.Crumb;

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => People.Select(Mentionable.Of);

    /// <summary>What is said where the instance listed nobody, as <see cref="Of" /> says it.</summary>
    public string Nobody => Of.Nobody;

    /// <inheritdoc />
    /// <remarks>A row is a person rather than a post, so none of the keys that act on one are offered.</remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new KeyHint("j/k", "person"),
        new KeyHint("⏎", "open", NeedsAPick: true),
        .. Of is ActorsOf.PostSide side
            ? [new KeyHint("s", side.Side.Other().Crumb().ToLowerInvariant())]
            : Array.Empty<KeyHint>(),
        .. Of is ActorsOf.PostSide { Page: not null, Home: { } home }
            ? [new KeyHint("o", $"on {home}")]
            : Array.Empty<KeyHint>(),
        Refreshing,
        .. PostKeys.Leaving(new KeyHint("esc", "back")),
    ];

    /// <inheritdoc />
    protected override IPicked Walking => _people;

    /// <summary>Puts what the first read of the group came back with on the screen, in the order the instance listed it.</summary>
    /// <param name="people">Everyone the read came back with.</param>
    /// <param name="notice">What the shell has to say about the read, or nothing.</param>
    public void Arrived(IReadOnlyList<Account> people, string? notice = null)
    {
        Notice = notice;
        _people.PutUnder(people);
    }

    /// <inheritdoc />
    IReadOnlyList<Account> IReadsOn<Account>.Items => People;

    /// <inheritdoc />
    /// <remarks>The group's actors behind the last one read (#467).</remarks>
    public void ReadOn(IReadOnlyList<Account> older) => _people.PutUnder(older);

    /// <inheritdoc />
    /// <remarks>A tie put on somebody opened from here rewrites their row, so <c>esc</c> lands on what is now true.</remarks>
    public override bool Heard(Change change)
    {
        if (change is Change.Tied(var account))
        {
            _people.Rewrite(person => person.Id == account.Id ? account : person);
        }

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <c>⏎</c> opens whoever is picked out as the same account screen <c>a</c> opens from a feed. <c>s</c> on a
    ///     post's actors stands the other side in this one's place, as a follow list's swap does, so flipping back and
    ///     forth never grows the stack.
    /// </remarks>
    public override Task Answer(Verb verb, Reach reach) => verb switch
    {
        Verb.OpenPerson when PickedPerson is { } person => reach.Open(Subject.Account.Of(person)),
        Verb.SwapSide when Of is ActorsOf.PostSide side =>
            reach.Swap(new Subject.PostActors(side.Post, side.Side.Other())),
        Verb.OpenPostPage when Of is ActorsOf.PostSide { Page: { } page } => Browsed(reach, page),
        _ => Task.CompletedTask,
    };

    /// <summary>
    ///     Why the list, read to its end, holds fewer than the post counts — or nothing, while there is more to read,
    ///     where it holds as many, or where it holds nobody, which <see cref="Nobody" /> already says.
    /// </summary>
    private ActorShortfall? Shortfall =>
        ReadingOn is { IsWhole: true, InFlight: false } && Read > 0 ? Of.ShortBy(Read) : null;

    private static Task Browsed(Reach reach, string page)
    {
        reach.Browse(page);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var lines = new List<Line>();

        // Wrapped rather than clipped, since why a post's list is short is a sentence that names two instances, and
        // cut at the edge it would name neither.
        if ((Notice ?? Shortfall?.Says) is { } notice)
        {
            lines.AddRange(TextWrap.Wrap(notice, width).Select(row => Line.Of(row, Role.Muted)));
            lines.Add(Line.Blank);
        }

        // And under them all, the foot of the list: what reading on is doing, or that the group is whole.
        lines.AddRange(WithFoot(_people.Rows(width, Draw, Line.Blank), apart: Line.Blank));

        return lines;

        IReadOnlyList<Line> Draw(Account person, int _, int room) =>
            AccountLines.Block(person, drawing.In(room), AccountLines.Compact(person.Standing, Implied.Nothing));
    }
}
