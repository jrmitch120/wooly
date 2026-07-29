using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core;
using Wooly.Core.Profiles;

namespace Wooly.Cli.Commands;

/// <summary>
///     Reports the profile this invocation would act as — the current one, or whatever <c>--profile</c> names — and
///     where its token is kept. The one command whose whole job is to answer "which account am I about to post as?"
///     before a command that posts is run.
/// </summary>
internal sealed class ProfileShowCommand(IAnsiConsole console, IProfileRegistry profiles, WoolyPaths paths)
    : Command<ProfileScopedSettings>
{
    protected override int Execute(CommandContext context, ProfileScopedSettings settings, CancellationToken cancellationToken)
    {
        // Resolving reads the access token too, so a profile that is set up but cannot authenticate is reported here
        // rather than by the first command that tries to use it. The token itself is never printed.
        var profile = profiles.Resolve(settings.Profile);

        console.MarkupLineInterpolated($"Acting as profile [bold]{profile.Name}[/].");
        console.WriteLine($"  instance  {profile.Instance}");
        console.WriteLine($"  account   {profile.Account ?? "not established"}");
        console.WriteLine($"  token     {TokenStorageDescription.For(profiles.TokenStorage, paths)}");

        return (int)ExitCode.Success;
    }
}
