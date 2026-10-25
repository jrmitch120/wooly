using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Every <b>Actor</b> in one notification group: everyone who favorited the post, boosted it or followed the reader,
///     where the row on Notifications names two and counts the rest (#455). Drawn as Account blocks the way Followers
///     is, and an ordinary screen on the stack, so opening somebody and walking back out works as it does there.
/// </summary>
/// <remarks>
///     Read from the group's own accounts (<see cref="Subject.Actors" />), so it lists exactly the group the row
///     counted rather than everyone who has ever favorited the post. Nothing here fetches: what is read arrives
///     through <see cref="Arrived" />, and the rest through <see cref="ReadOn" /> as the page nears the end of what is
///     in hand (#467).
/// </remarks>
/// <param name="group">The group whose actors these are.</param>
public sealed class ActorsScreen(NotificationGroup group) : Screen
{
    private readonly Picked<Account> _people = new([]);

    /// <summary>The group whose actors these are.</summary>
    public NotificationGroup Group { get; } = group;

    /// <summary>Everyone read so far, in the order the instance listed them, newest first.</summary>
    public IReadOnlyList<Account> People => _people.All;

    /// <summary>How many have been read.</summary>
    public int Read => _people.Count;

    /// <summary>
    ///     What the shell has to say about the list rather than anybody on it — that nobody is on it, or that a rate
    ///     limit cut the reading short — drawn on the screen, as Followers draws it.
    /// </summary>
    public string? Notice { get; private set; }

    /// <summary>Whoever is picked out, or nobody. What <c>⏎</c> opens.</summary>
    public Account? PickedPerson => _people.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     What they did, as the kind says it: "Favorited by", "Boosted by", "Followed you", and for a kind this client
    ///     has no words for, the instance's own word.
    /// </remarks>
    public override string Crumb => KindLook.Of(Group.Kind).Crumb;

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => People.Select(Mentionable.Of);

    /// <summary>What is said where the instance listed nobody in the group — every notification in it dismissed since.</summary>
    public static string Nobody => "Nobody is in this group any more.";

    /// <inheritdoc />
    /// <remarks>A row is a person rather than a post, so none of the keys that act on one are offered.</remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new KeyHint("j/k", "person"),
        new KeyHint("⏎", "open", NeedsAPick: true),
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
        ReadOn(people);
    }

    /// <summary>
    ///     Puts <paramref name="people" /> under everyone already read, as reading on brought them: below everybody,
    ///     with the pick left on whoever it was on and nothing above it moved (#467).
    /// </summary>
    public void ReadOn(IReadOnlyList<Account> people)
    {
        foreach (var person in people)
        {
            _people.Add(person);
        }
    }

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
    /// <remarks><c>⏎</c> opens whoever is picked out as the same account screen <c>a</c> opens from a feed.</remarks>
    public override Task Answer(Verb verb, Reach reach) =>
        verb == Verb.OpenPerson && PickedPerson is { } person
            ? reach.Open(Subject.Account.Of(person))
            : Task.CompletedTask;

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        // And under them all, the foot of the list: what reading on is doing, or that the group is whole.
        var rows = _people.Rows(width, Draw, Line.Blank);

        lines.AddRange(ReadingOn?.Under(rows, holding: Read > 0) ?? rows);

        return lines;

        IReadOnlyList<Line> Draw(Account person, int _, int room) =>
            AccountLines.Block(person, drawing.In(room), AccountLines.Compact(person.Standing, Implied.Nothing));
    }
}
