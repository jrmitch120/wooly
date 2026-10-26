using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Lists the accounts that favorited a post.</summary>
internal sealed class PostFavoritedByCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostActorsCommand(console, profiles, posts, ActorSide.Favorited);
