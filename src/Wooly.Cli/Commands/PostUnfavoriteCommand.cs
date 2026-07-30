using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Takes a favorite back off a post.</summary>
internal sealed class PostUnfavoriteCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostMarkCommand(console, profiles, posts, PostMark.Favorite, wanted: false);
