using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Commands;
using Wooly.Cli.Infrastructure;
using Wooly.Core;

namespace Wooly.Cli;

/// <summary>
///     Composes the CLI: the command tree, the container the commands are resolved from, the console they render to,
///     and the failure handling every command inherits. <c>Program</c> is a one-liner over this so tests can drive the
///     exact same pipeline against in-memory consoles.
/// </summary>
public static class WoolyCommandApp
{
    /// <summary>Builds the configured command app.</summary>
    /// <param name="console">
    ///     Where command output is rendered. Defaults to the real terminal's stdout; tests pass an in-memory console.
    /// </param>
    /// <param name="errorConsole">
    ///     Where failures are rendered. Defaults to the real terminal's stderr, keeping error text out of anything
    ///     piping stdout (ADR-0006).
    /// </param>
    /// <param name="configureServices">
    ///     Applied last to the container, so a caller can replace part of the core layer — the seam tests use to fake
    ///     the network instead of reaching a live instance.
    /// </param>
    public static CommandApp Create(
        IAnsiConsole? console = null,
        IAnsiConsole? errorConsole = null,
        Action<IServiceCollection>? configureServices = null)
    {
        console ??= AnsiConsole.Console;
        errorConsole ??= CreateStandardErrorConsole();

        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddSingleton(console);
        configureServices?.Invoke(services);

        var app = new CommandApp(new TypeRegistrar(services));

        app.Configure(config =>
        {
            config.SetApplicationName(WoolyClient.Name);
            config.ConfigureConsole(console);

            // An option this client does not have is a user expecting something of it that it does not do — most
            // pointedly --password, which ADR-0004 rules out. Left relaxed, Spectre collects such an option as a
            // leftover argument and carries on, so the user is answered by silence.
            config.UseStrictParsing();

            // Left to itself, Spectre renders failures to stdout and exits -1 — breaching both the stderr-only rule
            // and the reserved exit codes for every failure the CLI can have.
            config.SetExceptionHandler((exception, _) => CommandFailure.Report(exception, errorConsole));

            config.AddCommand<VersionCommand>("version")
                  .WithDescription("Print the client's version.");

            config.AddBranch("profile", profile =>
            {
                profile.SetDescription("Manage the local profiles this client acts as.");

                profile.AddCommand<ProfileAddCommand>("add")
                       .WithDescription("Connect a profile to a Mastodon account, through your browser by default.");

                profile.AddCommand<ProfileListCommand>("list")
                       .WithDescription("List the profiles set up on this machine.");

                profile.AddCommand<ProfileShowCommand>("show")
                       .WithDescription("Report the profile this client would act as.");

                profile.AddCommand<ProfileSwitchCommand>("switch")
                       .WithDescription("Change the profile commands act as by default.");
            });

            config.AddBranch("post", post =>
            {
                post.SetDescription("Write, read and act on posts as the current profile.");

                post.AddCommand<PostCreateCommand>("create")
                    .WithDescription("Publish a new post, optionally with a content warning, files or a poll.");

                post.AddCommand<PostReplyCommand>("reply")
                    .WithDescription("Publish a post answering another one, composed the same way as any other post.");

                post.AddCommand<PostEditCommand>("edit")
                    .WithDescription("Change what one of your posts says, leaving the rest of it as it was.");

                post.AddCommand<PostDeleteCommand>("delete")
                    .WithDescription("Take one of your posts down. This cannot be undone.");

                post.AddCommand<PostShowCommand>("show")
                    .WithDescription("Show a single post by id, outside any timeline.");

                post.AddCommand<PostBoostCommand>("boost")
                    .WithDescription("Re-share a post to your own followers.");

                post.AddCommand<PostUnboostCommand>("unboost")
                    .WithDescription("Stop re-sharing a post you had boosted.");

                post.AddCommand<PostFavoriteCommand>("favorite")
                    .WithDescription("Mark a post as liked, without boosting it.");

                post.AddCommand<PostUnfavoriteCommand>("unfavorite")
                    .WithDescription("Take a favorite back off a post.");

                post.AddCommand<PostPinCommand>("pin")
                    .WithDescription("Hold one of your own posts at the top of your profile.");

                post.AddCommand<PostUnpinCommand>("unpin")
                    .WithDescription("Release a pinned post back to where it falls by date.");
            });

            config.AddBranch("account", account =>
            {
                account.SetDescription("Manage who you follow, block and mute, and who follows you.");

                account.AddCommand<AccountFollowCommand>("follow")
                       .WithDescription("Follow an account, so its posts reach your home timeline.");

                account.AddCommand<AccountUnfollowCommand>("unfollow")
                       .WithDescription("Stop following an account, or withdraw a follow request.");

                account.AddCommand<AccountFollowersCommand>("followers")
                       .WithDescription("List the accounts following you, or following the account you name.");

                account.AddCommand<AccountFollowingCommand>("following")
                       .WithDescription("List the accounts you follow, or the account you name follows.");

                account.AddCommand<AccountBlockCommand>("block")
                       .WithDescription("Block an account: it is unfollowed, cannot follow you, and neither sees the other.");

                account.AddCommand<AccountUnblockCommand>("unblock")
                       .WithDescription("Lift a block. A follow the block broke has to be made again.");

                account.AddCommand<AccountMuteCommand>("mute")
                       .WithDescription("Hide an account without refusing it: still followed, simply not shown.");

                account.AddCommand<AccountUnmuteCommand>("unmute")
                       .WithDescription("Show a muted account again.");

                // A branch of its own under the noun, because answering a request is a different act on a different
                // thing from following: the account is asking, and what is accepted or rejected is what they asked.
                account.AddBranch("requests", requests =>
                {
                    requests.SetDescription("Answer the follows waiting on a locked account.");

                    requests.AddCommand<AccountRequestListCommand>("list")
                            .WithDescription("List the accounts waiting for you to let them follow you.");

                    requests.AddCommand<AccountRequestAcceptCommand>("accept")
                            .WithDescription("Let a waiting account follow you.");

                    requests.AddCommand<AccountRequestRejectCommand>("reject")
                            .WithDescription("Turn a waiting account away. They are told nothing, and may ask again.");
                });
            });

            config.AddBranch("notification", notification =>
            {
                notification.SetDescription("Read and clear what is waiting for the current profile.");

                notification.AddCommand<NotificationListCommand>("list")
                            .WithDescription("Read the mentions, follows, boosts and favorites waiting for you.");

                notification.AddCommand<NotificationDismissCommand>("dismiss")
                            .WithDescription("Clear a single notification, named by its id.");

                notification.AddCommand<NotificationClearCommand>("clear")
                            .WithDescription("Clear every notification at once. This cannot be undone.");
            });

            config.AddBranch("timeline", timeline =>
            {
                timeline.SetDescription("Read a timeline as the current profile.");

                timeline.AddCommand<TimelineHomeCommand>("home")
                        .WithDescription("Read the posts of the accounts you follow.");

                timeline.AddCommand<TimelineLocalCommand>("local")
                        .WithDescription("Read the public posts of accounts on your own instance.");

                timeline.AddCommand<TimelineFederatedCommand>("federated")
                        .WithDescription("Read the public posts reaching your instance from everywhere it federates with.");

                timeline.AddCommand<TimelineTagCommand>("tag")
                        .WithDescription("Read the public posts carrying a hashtag.");
            });

            // A noun of its own rather than a corner of "post", even though a direct message is a post: what a user
            // wants of their messages is a list of who is talking to them, which no timeline of posts answers. What
            // the branch deliberately does not have is its own way of composing — dm send is post create with the
            // audience settled (ADR-0013), and replying inside a conversation is post reply, which cannot answer a
            // direct message any more widely than it was said.
            config.AddBranch("dm", dm =>
            {
                dm.SetDescription("Read and write the direct conversations the current profile is in.");

                dm.AddCommand<DirectMessageListCommand>("list")
                  .WithDescription("List your direct conversations, and say which of them are unread.");

                dm.AddCommand<DirectMessageShowCommand>("show")
                  .WithDescription("Read one conversation in full, oldest post first.");

                dm.AddCommand<DirectMessageSendCommand>("send")
                  .WithDescription("Write to an account directly, without setting visibility or mentions by hand.");

                dm.AddCommand<DirectMessageReadCommand>("read")
                  .WithDescription("Clear the unread mark on a conversation.");
            });

            // A verb of its own rather than a branch: one command covers accounts, hashtags and posts alike, which is
            // the point of it — a user searching a half-remembered word rarely knows which of the three it will be.
            config.AddCommand<SearchCommand>("search")
                  .WithDescription("Find accounts, hashtags and posts matching what you are looking for.");
        });

        return app;
    }

    private static IAnsiConsole CreateStandardErrorConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
}
