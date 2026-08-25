using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     An account as rows of spans: who they are, how much of a presence they have, and where this profile stands with
///     them. What <see cref="PostLines" /> is for a post, and here for the same reason — three screens draw an account
///     (its own, a follow request, a search result) and a name that read one way on one of them and another way on the
///     next would be three different ideas of the same thing.
/// </summary>
public static class AccountLines
{
    /// <summary>The name and the handle, which are two things side by side and so two roles rather than one.</summary>
    public static IReadOnlyList<Line> Who(Account account, int width) =>
    [
        Line.Of(TextWrap.Clip(account.Author, width), Role.BylineName),
        Line.Of(TextWrap.Clip($"@{account.Address}", width), Role.BylineHandle),
    ];

    /// <summary>The name and handle on one row, for a list where each account gets as few rows as it can.</summary>
    public static Line Byline(Account account, int width)
    {
        var name = TextWrap.Clip(account.Author, width);
        var handle = TextWrap.Clip($"@{account.Address}", Math.Max(0, width - name.Length - 1));

        return Line.Of([
            new Span(name, Role.BylineName),
            new Span(handle.Length > 0 ? $" {handle}" : string.Empty, Role.BylineHandle),
        ]);
    }

    /// <summary>How much of a presence they have: posts, and the two follow counts.</summary>
    public static Line Presence(Account account, int width) => Line.Of(
        TextWrap.Clip(
            $"{Number.Of(account.Posts)} posts · {Number.Of(account.Following)} following · {Number.Of(account.Followers)} followers",
            width),
        Role.Muted);

    /// <summary>
    ///     Where the profile stands with them, or the fact that the instance was not asked. Absent is not the same as
    ///     nothing (CONTEXT.md), and five silences would say the profile follows nobody.
    /// </summary>
    public static Line Standing(Account account, int width)
    {
        if (account.Standing is not { } standing)
        {
            return Line.Of("Standing not asked for.", Role.Muted);
        }

        var said = new List<string>();

        if (standing.Following)
        {
            said.Add("you follow them");
        }
        else if (standing.FollowRequested)
        {
            said.Add("you have asked to follow them");
        }

        if (standing.FollowedBy)
        {
            said.Add("they follow you");
        }

        if (standing.Blocking)
        {
            said.Add("blocked");
        }

        if (standing.Muting)
        {
            said.Add("muted");
        }

        return said.Count == 0
            ? Line.Of("No ties either way.", Role.Muted)
            : Line.Of(TextWrap.Clip(string.Join(" · ", said), width), Role.Muted);
    }
}
