using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     What the two commands listing a post's <b>Actors</b> read, which is one side of them and differs only in which
///     side — the one difference a subclass supplies, as <see cref="AccountListCommand" />'s do for a follow list. The
///     rest is <see cref="PagedListCommand{TSettings,TItem}" />'s (#488).
/// </summary>
internal abstract class PostActorsCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IPostEngagement posts,
    ActorSide side) : PagedListCommand<PostActorsCommand.Settings, Account>(profiles)
{
    internal sealed class Settings : PagedListSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The id of the post, as shown by a timeline.")]
        public string PostId { get; init; } = string.Empty;

        /// <inheritdoc />
        protected override string Counted => "account";
    }

    protected override Listing<Account> ListingFor(ActiveProfile profile, Settings settings) =>
        new(
            token => posts.ReadActors(profile, settings.PostId, side, settings.Limit, token),
            fetch => AccountJson.WriteActors(console, side, settings.PostId, fetch),
            fetch => AccountReport.WriteActors(console, side, settings.PostId, fetch));
}
