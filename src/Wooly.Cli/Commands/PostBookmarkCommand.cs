using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Keeps a post to come back to, without its author being told.</summary>
internal sealed class PostBookmarkCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostMarkCommand(console, profiles, posts, PostMark.Bookmark, wanted: true);
