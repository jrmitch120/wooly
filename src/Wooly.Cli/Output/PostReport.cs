using Spectre.Console;
using Wooly.Core.Posts;

namespace Wooly.Cli.Output;

/// <summary>
///     Writes what became of a post, for a person to read. Short on purpose — an author who has just published knows
///     what they wrote, and what they do not know is the two things printed here: the id, which is how every later
///     command names this post, and that it went out as narrowly as they asked.
/// </summary>
internal static class PostReport
{
    /// <summary>Reports the post that has just been published.</summary>
    public static void Published(IAnsiConsole console, Post post)
    {
        // The visibility comes off the published post rather than off the draft, so this reports what the instance
        // actually did — which for a draft that left the choice to the account is the only place it is knowable.
        console.MarkupLineInterpolated($"Posted [bold]{post.Id}[/] ({PostVisibilityName.Of(post.Visibility)}).");

        WriteAddress(console, post);
    }

    /// <summary>Reports the post that has just been changed.</summary>
    public static void Edited(IAnsiConsole console, Post post)
    {
        console.MarkupLineInterpolated($"Edited [bold]{post.Id}[/].");

        WriteAddress(console, post);
    }

    /// <summary>Reports the post that has just been taken down, which there is nothing left to link to.</summary>
    public static void Deleted(IAnsiConsole console, string postId) =>
        console.MarkupLineInterpolated($"Deleted post [bold]{postId}[/].");

    /// <summary>
    ///     Written without markup: an address is not this client's text to interpret, and a stray bracket in one would be
    ///     read as formatting rather than printed.
    /// </summary>
    private static void WriteAddress(IAnsiConsole console, Post post)
    {
        if (post.Url is not null)
        {
            console.WriteLine(post.Url);
        }
    }
}
