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
///     Connects a profile to a Mastodon account. ADR-0004's two ways in meet here and part company for one step only —
///     how the access token is come by, through the browser or from the user's own hands. Everything after that is the
///     same for both: the token is checked against the instance before anything is written, so the profile that lands
///     is one that works, and it is stored the one way profiles are stored.
/// </summary>
internal sealed class ProfileAddCommand(
    IAnsiConsole console,
    IProfileRegistry profiles,
    IAccessTokenVerifier verifier,
    IBrowserAuthorizer authorizer,
    IWebBrowser browser,
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
        [Description("Connect with an access token you already have, instead of through the browser.")]
        public string? AccessToken { get; init; }

        [CommandOption("--manual")]
        [Description("Ask for an access token to paste, for a machine with no browser to open.")]
        public bool Manual { get; init; }

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

            // Both name the same fallback and only one of them can be honoured, so a user who passed both meant
            // something this command cannot do. Letting the token quietly win would be the silence ADR-0006 turned
            // strict parsing on to stop.
            if (Manual && !string.IsNullOrWhiteSpace(AccessToken))
            {
                return ValidationResult.Error(
                    "Pass --token <TOKEN> to give a token outright, or --manual to be asked for one — not both.");
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var accessToken = await ObtainAccessToken(settings, cancellationToken);

        // Before the profile is written, not after: a profile that cannot authenticate is worse than no profile,
        // because every later command would fail as though the client were broken. A token from the browser goes
        // through this too — it is how the account to record the profile under is learned, and a token that has just
        // been issued costs one call to confirm.
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

    /// <summary>
    ///     Which of ADR-0004's two paths this invocation takes. The browser is the default because it is the one that
    ///     asks nothing of the user that they have to go and find first; the other two are what they chose instead.
    /// </summary>
    private Task<string> ObtainAccessToken(Settings settings, CancellationToken cancellationToken)
    {
        // Trimmed because a token is pasted rather than typed, and a paste carries whatever whitespace came with it.
        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            return Task.FromResult(settings.AccessToken.Trim());
        }

        if (settings.Manual)
        {
            return Task.FromResult(AskForAccessToken());
        }

        // A browser sign-in is a conversation: the address to go to is printed here, and the answer comes back here.
        // With no terminal for either half — under a pipe, or in CI — nobody would see the address, and the wait
        // would end minutes later in a failure the user was never given a way to avoid. ADR-0004's fallback is the
        // whole answer to a machine like that, so it is named rather than merely available.
        if (!CanAskTheUser)
        {
            throw new UsageException(
                "Connecting through the browser needs a terminal, and there is none here. Pass --token <TOKEN>.");
        }

        return AuthorizeInBrowser(settings.Instance, cancellationToken);
    }

    private string AskForAccessToken()
    {
        if (!CanAskTheUser)
        {
            throw new UsageException(
                "No access token given, and there is no terminal to ask for one. Pass --token <TOKEN>.");
        }

        return console.Prompt(new TextPrompt<string>("Access token:").Secret()).Trim();
    }

    /// <summary>
    ///     Sends the user to the instance's own authorization page and waits for their answer to come back. The
    ///     address is printed either way: a browser that opened is no guarantee the right one did, and one that did not
    ///     open leaves the address as the only way on.
    /// </summary>
    private async Task<string> AuthorizeInBrowser(string instance, CancellationToken cancellationToken)
    {
        using var authorization = await authorizer.Begin(instance, cancellationToken);

        if (browser.TryOpen(authorization.AuthorizationUrl))
        {
            console.MarkupLineInterpolated(
                $"Opening [bold]{instance}[/] in your browser to authorize {WoolyClient.Name}. If it does not open, go to:");
        }
        else
        {
            console.MarkupLineInterpolated(
                $"No browser could be opened here. To authorize {WoolyClient.Name}, go to [bold]{instance}[/] at:");
        }

        // Written without markup: an address is not this client's text to interpret, and a stray bracket in one would
        // be read as formatting. Through WebAddress for the same reason it is printed at all — this is an address to be
        // pasted into a browser, and one whose escapes have been given back is no longer the address that was asked for.
        console.WriteLine(WebAddress.Of(authorization.AuthorizationUrl));
        console.WriteLine("Waiting for the browser to come back...");

        return await authorization.AwaitAccessToken(cancellationToken);
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
