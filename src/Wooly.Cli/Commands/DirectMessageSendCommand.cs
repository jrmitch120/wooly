using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Output;
using Wooly.Core.Accounts;
using Wooly.Core.Configuration;
using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Starts a direct conversation with somebody. Everything about composing it is
///     <see cref="PostComposeCommand{TSettings}" />'s, which is the point (ADR-0013): a direct message is a post that
///     went out direct, so it is published by the same call, validated by the same rules, and can carry the same
///     content warning and the same files as anything else this client writes.
///     <para>
///         What the command adds is the two things a user would otherwise have to remember: the visibility, and writing
///         the recipient into the text so the instance knows who to deliver it to.
///     </para>
/// </summary>
internal sealed class DirectMessageSendCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IConfigStore config,
    IPostAuthor posts) : PostComposeCommand<DirectMessageSendCommand.Settings>(console, profiles, config, posts)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Who it went to rather than what visibility it went out at: the visibility is the whole premise of the
    ///     command, and the recipient is what the sender wants confirmed.
    /// </remarks>
    protected override void Report(Settings settings, Post published) =>
        ConversationReport.Sent(Console, settings.Address, published);

    internal sealed class Settings : PostComposeSettings
    {
        [CommandArgument(0, "<ACCOUNT>")]
        [Description("Who to write to, as user@instance — or a bare username for somebody on your own instance.")]
        public string Account { get; init; } = string.Empty;

        [CommandArgument(1, "<TEXT>")]
        [Description("What the message says. May be empty only for a message that is nothing but attached files.")]
        public string MessageText { get; init; } = string.Empty;

        /// <summary>
        ///     The account written to, which <see cref="Validate" /> has already established is one — an address that
        ///     is not cannot reach here.
        /// </summary>
        public AccountAddress Address => AccountAddress.Parse(Account);

        /// <inheritdoc />
        /// <remarks>
        ///     The recipient is part of the text, not a field beside it: Mastodon delivers a direct post to the accounts
        ///     its text mentions and to nobody else, so a message that did not name them would reach nobody at all.
        /// </remarks>
        public override string Text => DirectMessage.To(Address, MessageText);

        /// <inheritdoc />
        /// <remarks>
        ///     Not negotiable, and not offered as an option. Chosen rather than inherited, so that nothing downstream
        ///     mistakes it for a preference it may widen.
        /// </remarks>
        protected override bool TryChooseAudience(
            PostVisibility? whenUnsaid,
            [NotNullWhen(true)] out ComposedVisibility? audience,
            [NotNullWhen(false)] out string? problem)
        {
            audience = new ComposedVisibility(PostVisibility.Direct, Chosen: true);
            problem = null;

            return true;
        }

        public override ValidationResult Validate() =>

            // Asked before anything else, because Text cannot be read until the address parses — and everything the
            // shared rules check is about the text.
            AccountAddress.IsWellFormed(Account)
                ? base.Validate()
                : ValidationResult.Error(AccountAddress.Rejection(Account));
    }
}
