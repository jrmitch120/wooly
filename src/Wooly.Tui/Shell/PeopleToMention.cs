using Wooly.Core.Accounts;

namespace Wooly.Tui.Shell;

/// <summary>
///     Who a post can mention, for one profile, for the session (#318): every account the shell has already read, fed
///     to it as screens arrive, so suggesting somebody costs no request — and the accounts the profile follows, read
///     once the first time anybody is asked for (#321). Never written to disk, and dropped with the profile it was
///     gathered as.
/// </summary>
/// <param name="profile">The profile this is gathered as — whose own account is nobody to mention.</param>
public sealed class PeopleToMention(Core.Profiles.ActiveProfile profile)
{
    /// <summary>The most a query is answered with, so the list never buries what is being written.</summary>
    public const int Most = 5;

    /// <summary>
    ///     The most of the profile's follows read, five pages of 80: enough for most, and never a large share of the
    ///     rate limit spent on autocomplete for somebody following thousands.
    /// </summary>
    public const int FollowsRead = 400;

    /// <summary>Everybody seen, by address, with when they were last seen.</summary>
    private readonly Dictionary<string, (Mentionable Person, long Seen)> _people = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Everybody the profile follows, as far as is known, by address.</summary>
    private readonly Dictionary<string, Mentionable> _follows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether each account a tie went on or came off this session is followed now, by address — which outlasts a
    ///     page of follows read before the tie changed.
    /// </summary>
    private readonly Dictionary<string, bool> _tied = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts up with every sighting, so a higher one was seen more recently.</summary>
    private long _clock;

    /// <summary>Whether anybody has been asked for yet.</summary>
    private bool _asked;

    /// <summary>
    ///     Takes in everybody <paramref name="people" /> names, the first of them the most recently seen — a screen's top
    ///     row being the one a reader met last. Somebody already known is seen again, and keeps the name they had where
    ///     this sighting knew only their address.
    /// </summary>
    public void Saw(IEnumerable<Mentionable> people)
    {
        foreach (var person in people.Reverse())
        {
            if (profile.SignsInAs(person.Address))
            {
                continue;
            }

            var named = person.Name.Length == 0 && _people.TryGetValue(person.Address, out var known)
                ? known.Person
                : person;

            _people[person.Address] = (named, ++_clock);
        }
    }

    /// <summary>
    ///     Whether this is the first time anybody has been asked for — the first <c>@</c> of the session, which is when
    ///     the profile's follows are worth reading, and not before (#321).
    /// </summary>
    public bool FirstAsked()
    {
        var first = !_asked;

        _asked = true;

        return first;
    }

    /// <summary>
    ///     Takes in a page of the accounts the profile follows, as it arrives — leaving out anybody a tie has come off
    ///     since, whom the page was read too early to know about.
    /// </summary>
    public void Followed(IEnumerable<Account> page)
    {
        foreach (var account in page)
        {
            if (!_tied.ContainsKey(account.Address) && !profile.SignsInAs(account.Address))
            {
                _follows[account.Address] = Mentionable.Of(account);
            }
        }
    }

    /// <summary>
    ///     A tie went on or came off <paramref name="account" />, which the instance answered with where the profile
    ///     now stands: followed, they are somebody to mention at once; no longer, they are dropped from the follows. A
    ///     follow waiting on a locked account is not a follow yet.
    /// </summary>
    public void Tied(Account account)
    {
        if (account.Standing is not { } standing)
        {
            return;
        }

        _tied[account.Address] = standing.Following;

        if (standing.Following)
        {
            _follows[account.Address] = Mentionable.Of(account);
        }
        else
        {
            _follows.Remove(account.Address);
        }
    }

    /// <summary>
    ///     The best few for <paramref name="query" />, best first: a handle starting with it, then a word of the name
    ///     starting with it, then a handle merely containing it. Within each, the people seen come first, the most
    ///     recently seen first, and then the follows in order of name. Nothing for a query that is already a whole
    ///     address somebody here answers to, since there is nothing left to suggest.
    /// </summary>
    /// <param name="query">What follows the <c>@</c>, as typed.</param>
    public IReadOnlyList<Mentionable> Matching(string query)
    {
        if (_people.ContainsKey(query) || _follows.ContainsKey(query))
        {
            return [];
        }

        var seen = _people.Values.Select(seen => (seen.Person, Seen: (long?)seen.Seen));
        var followed = _follows.Values
                               .Where(person => !_people.ContainsKey(person.Address))
                               .Select(person => (Person: person, Seen: (long?)null));

        return
        [
            .. seen.Concat(followed)
                   .Select(known => (known.Person, known.Seen, Tier: Tier(known.Person, query)))
                   .Where(match => match.Tier is not null)
                   .OrderBy(match => match.Tier)
                   .ThenBy(match => match.Seen is null)
                   .ThenByDescending(match => match.Seen)
                   .ThenBy(match => match.Person.Name, StringComparer.InvariantCultureIgnoreCase)
                   .ThenBy(match => match.Person.Address, StringComparer.OrdinalIgnoreCase)
                   .Take(Most)
                   .Select(match => match.Person),
        ];
    }

    /// <summary>
    ///     What mentioning <paramref name="person" /> writes into the post: the short <c>@user</c> for somebody on the
    ///     profile's own instance, as Mastodon's own client writes it, and the full address for anybody else.
    /// </summary>
    public string MentionOf(Mentionable person)
    {
        var at = person.Address.IndexOf('@', StringComparison.Ordinal);

        return at >= 0 && string.Equals(person.Address[(at + 1)..], profile.Instance, StringComparison.OrdinalIgnoreCase)
            ? $"@{person.Address[..at]}"
            : $"@{person.Address}";
    }

    private static int? Tier(Mentionable person, string query)
    {
        if (person.Address.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (person.Name.Split(' ').Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        return person.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : null;
    }
}
