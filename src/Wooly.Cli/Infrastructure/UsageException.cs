using Wooly.Core.Errors;

namespace Wooly.Cli.Infrastructure;

/// <summary>
///     The command line was wrong in a way the parser cannot see — an option that is only needed in some invocations,
///     or a value that is only wrong in context. Reported like any other anticipated failure, but with the usage exit
///     code, so a script can still tell "you asked wrongly" from "it did not work" (ADR-0006).
/// </summary>
internal sealed class UsageException(string message) : WoolyException(message);
