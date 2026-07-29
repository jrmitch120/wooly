using System.Text.Json;
using Mastonet;
using Spectre.Console;
using Spectre.Console.Cli;
using Wooly.Core.Errors;

namespace Wooly.Cli.Infrastructure;

/// <summary>
///     The one place a command failure becomes visible output and a process exit code. Commands never print their own
///     errors — they throw, and everything below happens to them identically (ADR-0006).
/// </summary>
internal static class CommandFailure
{
    /// <summary>Writes <paramref name="exception" /> to <paramref name="errorConsole" /> and returns its exit code.</summary>
    /// <param name="errorConsole">
    ///     A console backed by stderr. Nothing here may touch stdout: a command's own output has to stay pipeable.
    /// </param>
    public static int Report(Exception exception, IAnsiConsole errorConsole)
    {
        var message = Describe(exception);

        if (message is null)
        {
            // Nothing anticipated this, so it is a defect in this client rather than something the user did — and
            // here the stack trace is the useful part.
            errorConsole.WriteException(exception, ExceptionFormats.ShortenPaths);
        }
        else
        {
            errorConsole.MarkupLineInterpolated($"[red]error:[/] {message}");
        }

        return (int)ExitCodeFor(exception);
    }

    /// <summary>
    ///     The line to show for a failure this client anticipated, or <see langword="null" /> for one it did not.
    /// </summary>
    private static string? Describe(Exception exception) => exception switch
    {
        // Written for the person reading them, so the message alone is the whole report.
        WoolyException or CommandAppException => exception.Message,

        // The instance turned the request down and said why. Its own wording is the clearest thing available.
        ServerErrorException => exception.Message,

        // Something answered, but not with Mastodon's API — most often a domain that isn't an instance at all. The
        // parser's complaint about the byte it choked on would tell the user nothing.
        JsonException => "The instance did not answer with Mastodon API data. Check the instance domain.",

        // A cancellation reaching here means HttpClient's timeout elapsed; its own message says only "A task was
        // canceled", which tells the user nothing.
        OperationCanceledException => "The instance took too long to respond.",

        _ => null,
    };

    private static ExitCode ExitCodeFor(Exception exception) => exception switch
    {
        RateLimitedException => ExitCode.RateLimited,
        TransientNetworkException or OperationCanceledException => ExitCode.NetworkError,

        // No profile to act as, or one whose token the instance will not take. All of them are fixed by
        // authenticating a profile, which is what makes them one code rather than three.
        AuthenticationException => ExitCode.AuthenticationError,

        // How the command line was written, or what it asked for, was wrong. A profile named that does not exist
        // belongs here too: it is a value that is wrong, not a client that cannot authenticate.
        UnknownProfileException or UsageException or CommandParseException or CommandRuntimeException =>
            ExitCode.UsageError,

        _ => ExitCode.Error,
    };
}
