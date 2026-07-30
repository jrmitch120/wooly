using Spectre.Console;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>Takes back a boost, so the post stops being re-shared to the profile's own followers.</summary>
internal sealed class PostUnboostCommand(IAnsiConsole console, IProfileRegistry profiles, IPostEngagement posts)
    : PostMarkCommand(console, profiles, posts, PostMark.Boost, wanted: false);
