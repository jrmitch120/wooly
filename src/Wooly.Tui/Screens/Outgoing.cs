using Wooly.Core.Posts;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a compose screen sends when <c>ctrl-s</c> is pressed: a post to publish, or a change to one already
///     published. The whole of what the author wrote, in one value, said by the screen that holds the fields it was
///     written in (<see cref="ComposeScreen.Outgoing" />, #146).
/// </summary>
/// <remarks>
///     A value rather than a call: a screen reaches no port and knows about no instance
///     (<see cref="Screen" />), so what goes out is something it answers and the shell is what puts it. Which of the
///     two this is, is the same question the port asks — <see cref="IPostAuthor.Publish" /> or
///     <see cref="IPostAuthor.Edit" /> — so the shell branches on it once and assembles nothing.
///     <para>
///         Here rather than in <c>Wooly.Core</c> for the reason <see cref="Result" /> gives: the CLI composes a draft
///         out of options it was invoked with and knows at the parse which of the two commands it is running, so a
///         draft-or-edit is a shape one front end needs rather than one the domain has.
///     </para>
/// </remarks>
public abstract record Outgoing
{
    /// <remarks>Closed, so that what a compose screen can send is the two things a post author can be asked for.</remarks>
    private Outgoing()
    {
    }

    /// <summary>A post going out for the first time, which a reply is too — a draft that names what it answers.</summary>
    public sealed record Publishing(PostDraft Draft) : Outgoing;

    /// <summary>A change to the post <paramref name="PostId" /> names.</summary>
    public sealed record Saving(string PostId, PostEdit Edit) : Outgoing;
}
