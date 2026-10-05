using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a person says in the list of people to mention (#318): their name, then their handle, with the letters the
///     @-word matched picked out. The list round them is <see cref="PickLines" />'s.
/// </summary>
public static class MentionLines
{
    /// <summary>The columns between a name and the handle after it.</summary>
    private const string NameGap = "  ";

    /// <summary>
    ///     One person, given whether they are the picked one and what the @-word typed so far is: their name, the gap and
    ///     their <c>@</c>-handle.
    /// </summary>
    public static IEnumerable<Span> Row(Mentionable person, bool picked, string query)
    {
        var name = picked ? Role.ReferencePicked : Role.Body;
        var handle = picked ? Role.BylineHandle : Role.Muted;

        return
        [
            .. Lit(person.Name, NameMatch(person.Name, query), query.Length, name),
            new(person.Name.Length > 0 ? NameGap : string.Empty, Role.Body),
            new("@", handle),
            .. Lit(person.Address, AddressMatch(person.Address, query), query.Length, handle),
        ];
    }

    /// <summary>What a person's name, the gap and their <c>@</c>-handle take.</summary>
    public static int Columns(Mentionable person) =>
        (person.Name.Length > 0 ? Glyphs.Columns(person.Name) + NameGap.Length : 0) + 1 + Glyphs.Columns(person.Address);

    /// <summary>Where in a name <paramref name="query" /> matched: the start of the first word it starts.</summary>
    private static int NameMatch(string name, string query)
    {
        if (query.Length == 0)
        {
            return -1;
        }

        for (var at = 0; at < name.Length; at++)
        {
            if ((at == 0 || name[at - 1] == ' ')
                && string.Compare(name, at, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0
                && at + query.Length <= name.Length)
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>Where in an address <paramref name="query" /> matched, its start first.</summary>
    private static int AddressMatch(string address, string query) =>
        query.Length == 0 ? -1 : address.IndexOf(query, StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="text" /> in <paramref name="role" />, with the match at <paramref name="at" /> as a mention.</summary>
    private static IEnumerable<Span> Lit(string text, int at, int length, Role role) =>
        at < 0
            ? [new Span(text, role)]
            :
            [
                new Span(text[..at], role),
                new Span(text.Substring(at, length), Role.Mention),
                new Span(text[(at + length)..], role),
            ];
}
