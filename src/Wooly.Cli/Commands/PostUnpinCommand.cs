using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Releases a pinned post back to where it falls by date.</summary>
internal sealed class PostUnpinCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostMarkCommand(console, profiles, posts, PostMark.Pin, wanted: false);
