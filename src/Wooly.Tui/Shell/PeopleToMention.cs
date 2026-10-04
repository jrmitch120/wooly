namespace Wooly.Tui.Shell;

/// <summary>
///     Who a post can mention, for one profile, for the session (#318): every account the shell has already read, fed
///     to it as screens arrive, so suggesting somebody costs no request. Never written to disk, and dropped with the
///     profile it was gathered as.
/// </summary>
/// <param name="profile">The profile this is gathered as — whose own account is nobody to mention.</param>
public sealed class PeopleToMention(Core.Profiles.ActiveProfile profile)
{
    /// <summary>The most a query is answered with, so the list never buries what is being written.</summary>
    public const int Most = 5;

    /// <summary>Everybody seen, by address, with when they were last seen.</summary>
    private readonly Dictionary<string, (Mentionable Person, long Seen)> _people = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Counts up with every sighting, so a higher one was seen more recently.</summary>
    private long _clock;

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
    ///     The best few for <paramref name="query" />, best first: a handle starting with it, then a word of the name
    ///     starting with it, then a handle merely containing it — and within each, the most recently seen. Nothing for
    ///     a query that is already a whole address somebody here answers to, since there is nothing left to suggest.
    /// </summary>
    /// <param name="query">What follows the <c>@</c>, as typed.</param>
    public IReadOnlyList<Mentionable> Matching(string query)
    {
        if (_people.ContainsKey(query))
        {
            return [];
        }

        return
        [
            .. _people.Values
                      .Select(seen => (seen.Person, seen.Seen, Tier: Tier(seen.Person, query)))
                      .Where(match => match.Tier is not null)
                      .OrderBy(match => match.Tier)
                      .ThenByDescending(match => match.Seen)
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
