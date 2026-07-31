using System.ComponentModel;
using Spectre.Console.Cli;
using Wooly.Core.Posts;

namespace Wooly.Cli.Commands;

/// <summary>
///     Everything <see cref="PostComposeSettings" /> holds, plus the choice of who can see the post. Split out from it
///     so that <c>dm send</c> can inherit the composing without inheriting the choice: a direct message is direct, and
///     an option offered where only one value is possible is an option somebody will pass another value to.
/// </summary>
internal abstract class PostPublishSettings : PostComposeSettings
{
    [CommandOption("--visibility <WHO>")]
    [Description(
        "Who can see the post: public (anyone), unlisted (anyone with the link), private (your followers) or "
        + "direct (only the accounts mentioned). Defaults to your own setting on the instance.")]
    public string? Visibility { get; init; }

    /// <inheritdoc />
    /// <remarks>
    ///     A visibility typed here counts as chosen, and a preference read out of the config file does not. Only the
    ///     first is refused for being wider than a post it answers; the second is narrowed to fit, so that a profile
    ///     whose <c>default_visibility</c> is public can still answer a direct message.
    /// </remarks>
    protected override ComposedVisibility Reaching(PostVisibility? whenUnsaid, out string? problem)
    {
        problem = null;

        if (Visibility is null)
        {
            return new ComposedVisibility(whenUnsaid, Chosen: false);
        }

        if (PostVisibilityName.Parse(Visibility) is not { } named)
        {
            problem = PostVisibilityName.Rejection(Visibility);

            return new ComposedVisibility(null, Chosen: false);
        }

        return new ComposedVisibility(named, Chosen: true);
    }
}
