using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Cli.Infrastructure;
using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Connects a profile to a Mastodon account with an access token the user obtained from the instance themselves —
///     ADR-0004's headless path, and the one that needs no browser. The token is checked against the instance before
///     anything is written, so the profile that lands is one that works.
/// </summary>
internal sealed class ProfileAddCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccessTokenVerifier verifier,
    WoolyPaths paths) : AsyncCommand<ProfileAddCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("What to call this profile locally, e.g. work.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--instance <DOMAIN>")]
        [Description("The instance the account is on, e.g. mastodon.social.")]
        public string Instance { get; init; } = string.Empty;

        [CommandOption("--token <TOKEN>")]
        [Description("An access token for the account. Asked for instead if omitted, keeping it out of shell history.")]
        public string? AccessToken { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return ValidationResult.Error("Give the profile a name to be known by, e.g. work.");
            }

            if (string.IsNullOrWhiteSpace(Instance))
            {
                return ValidationResult.Error("Say which instance the account is on with --instance <DOMAIN>.");
            }

            // Checked here as well as in the registry so that the commonest typo is answered by the argument parser,
            // as a usage error against the value the user just typed, rather than as a defect further in.
            if (!InstanceDomain.IsWellFormed(Instance))
            {
                return ValidationResult.Error(InstanceDomain.Rejection(Instance));
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var accessToken = ReadAccessToken(settings);

        // Before the profile is written, not after: a profile that cannot authenticate is worse than no profile,
        // because every later command would fail as though the client were broken.
        var account = await verifier.VerifyAccount(settings.Instance, accessToken);

        var addition = profiles.Add(
            settings.Name,
            new ProfileConfig { Instance = settings.Instance, Account = account },
            accessToken);

        var what = addition.ReplacedExisting ? "Replaced" : "Added";
        console.MarkupLineInterpolated($"{what} profile [bold]{settings.Name}[/] ({account}).");

        if (addition.IsCurrent)
        {
            CurrentProfileNotice.Write(console, settings.Name);
        }

        WarnIfTheTokenIsInTheClear();

        return (int)ExitCode.Success;
    }

    private string ReadAccessToken(Settings settings)
    {
        // Trimmed because a token is pasted rather than typed, and a paste carries whatever whitespace came with it.
        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            return settings.AccessToken.Trim();
        }

        if (!CanAskTheUser)
        {
            throw new UsageException(
                "No access token given, and there is no terminal to ask for one. Pass --token <TOKEN>.");
        }

        return console.Prompt(new TextPrompt<string>("Access token:").Secret()).Trim();
    }

    /// <summary>
    ///     Whether there is a terminal to prompt at — false under a pipe or in CI. Spectre's <c>Profile</c> here is the
    ///     console's own, nothing to do with this client's profiles; asking it through a named property keeps the two
    ///     senses of the word apart in the file that most needs them apart.
    /// </summary>
    private bool CanAskTheUser => console.Profile.Capabilities.Interactive;

    /// <summary>
    ///     ADR-0003 keeps the plaintext fallback rather than refusing to run, on condition that the tradeoff is never
    ///     silent. This is the moment it matters: the token has just been handed over, and the user can still decide
    ///     against it.
    /// </summary>
    private void WarnIfTheTokenIsInTheClear()
    {
        if (profiles.TokenStorage is not CredentialStorage.PlaintextFile)
        {
            return;
        }

        var where = TokenStorageDescription.For(profiles.TokenStorage, paths);

        console.MarkupLineInterpolated(
            $"[yellow]warning:[/] no OS keyring answered on this machine, so the access token is {where}.");
    }
}
