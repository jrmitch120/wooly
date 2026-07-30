using Wooly.Core.Accounts;

namespace Wooly.Core.Errors;

/// <summary>
///     An account was named that the instance could not find, even after being asked to go and look for it. Reported
///     like an unknown profile: a value on the command line that is wrong, rather than a client that could not do its
///     job.
/// </summary>
public sealed class UnknownAccountException(AccountAddress account, string instance)
    : WoolyException(
        $"{instance} could not find an account called {account.On(instance)}. "
        + "Check the spelling, and that the instance it is on is still federating.")
{
    /// <summary>The address that was named.</summary>
    public AccountAddress Account { get; } = account;
}
