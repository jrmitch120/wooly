using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Re-shares somebody else's post to the profile's own followers.</summary>
internal sealed class PostBoostCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostMarkCommand(console, profiles, posts, PostMark.Boost, wanted: true);
