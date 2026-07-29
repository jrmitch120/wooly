using Spectre.Console.Cli;
using Wooly.Cli;
using Wooly.Cli.Commands;

namespace Wooly.Tests.Cli;

/// <summary>
///     Spectre.Console.Cli has no global options, so <c>--profile</c> reaches a command only by that command's settings
///     deriving from <see cref="ProfileScopedSettings" />. That makes the flag a convention, and a convention nothing
///     enforces is one a later command silently omits — the flag would simply not be there, with nothing failing and
///     no error to read. These tests make that a build failure instead: a new command either takes <c>--profile</c>,
///     or says below why it acts as no account.
/// </summary>
public class CommandProfileScopeTests
{
    /// <summary>
    ///     The commands that legitimately act as no account, and why. Every one of these would be advertising a flag it
    ///     has no use for. A command in neither this list nor <see cref="ProfileScopedSettings" /> is the omission
    ///     these tests exist to catch.
    /// </summary>
    private static readonly Dictionary<Type, string> ActsAsNoAccount = new()
    {
        [typeof(VersionCommand)] =
            "reports this client's version, and an instance's — neither of them read as an account",
        [typeof(ProfileAddCommand)] =
            "names the profile it creates, so there is nothing for an override to override",
        [typeof(ProfileListCommand)] =
            "lists every profile rather than acting as one",
        [typeof(ProfileSwitchCommand)] =
            "names the profile every later invocation acts as, which is the opposite of this invocation only",
    };

    [Fact]
    public void EveryCommandThatActsAsAnAccountTakesTheProfileOverride()
    {
        var missing = CommandTypes()
                      .Where(command => !ActsAsNoAccount.ContainsKey(command))
                      .Where(command => !typeof(ProfileScopedSettings).IsAssignableFrom(SettingsTypeOf(command)))
                      .Select(command => command.Name)
                      .Order(StringComparer.Ordinal)
                      .ToList();

        Assert.True(
            missing.Count == 0,
            $"These commands cannot be given --profile: {string.Join(", ", missing)}. Derive their settings from "
            + $"{nameof(ProfileScopedSettings)}, or record in {nameof(ActsAsNoAccount)} why they act as no account.");
    }

    /// <summary>
    ///     Keeps the exemptions honest in the other direction: one left behind by a renamed or deleted command would
    ///     quietly go on excusing nothing.
    /// </summary>
    [Fact]
    public void NothingIsExemptedThatIsNoLongerACommand()
    {
        var stale = ActsAsNoAccount.Keys
                                   .Except(CommandTypes())
                                   .Select(type => type.Name)
                                   .Order(StringComparer.Ordinal)
                                   .ToList();

        Assert.True(
            stale.Count == 0,
            $"{nameof(ActsAsNoAccount)} exempts types that are not commands: {string.Join(", ", stale)}.");
    }

    private static IReadOnlyList<Type> CommandTypes() =>
        typeof(WoolyCommandApp).Assembly
                               .GetTypes()
                               .Where(type => type is { IsClass: true, IsAbstract: false })
                               .Where(typeof(ICommand).IsAssignableFrom)
                               .ToList();

    /// <summary>
    ///     The <c>TSettings</c> a command was closed over, or <see langword="null" /> for one taking no settings at all
    ///     — which is no more able to carry <c>--profile</c> than settings of the wrong type.
    /// </summary>
    private static Type? SettingsTypeOf(Type command)
    {
        for (var type = command; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && typeof(ICommand).IsAssignableFrom(type))
            {
                return type.GetGenericArguments().Single();
            }
        }

        return null;
    }
}
