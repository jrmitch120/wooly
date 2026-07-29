using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core;

namespace Wooly.Cli.Commands;

/// <summary>
///     Prints the running client's name and version, and — given <c>--instance</c> — the Mastodon version the named
///     instance is running. That second half needs no credentials, which makes it the one command able to prove the
///     whole call path (retry, rate limiting, failure reporting) works before authentication exists.
/// </summary>
internal sealed class VersionCommand(IAnsiConsole console, IClientInfo clientInfo, IMastodonClientFactory clientFactory)
    : AsyncCommand<VersionCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandOption("--instance <DOMAIN>")]
        [Description("Also report the Mastodon version the given instance is running.")]
        public string? Instance { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        console.WriteLine($"{clientInfo.Name} {clientInfo.Version}");

        if (!string.IsNullOrWhiteSpace(settings.Instance))
        {
            var instance = await clientFactory.CreateAnonymousClient(settings.Instance).GetInstanceV2();

            console.WriteLine($"{settings.Instance} (Mastodon {instance.Version})");
        }

        return (int)ExitCode.Success;
    }
}
