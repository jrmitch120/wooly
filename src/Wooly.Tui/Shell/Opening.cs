using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Tui.Shell;

/// <summary>
///     Who the shell opens acting as — or, where there is nobody it can act as, what it opens onto instead: adding a
///     profile, filled in for one whose token has to be signed in again (ADR-0020, #247).
/// </summary>
/// <remarks>
///     Settled before a screen exists, because the failures that are still said on stderr have to be said while there
///     is a console to say them on. Which failures those are is decided here rather than in the launch host, so that it
///     is decided somewhere a test can reach.
/// </remarks>
public sealed record Opening
{
    private Opening(ActiveProfile? profile, ProfileSummary? again, string? why)
    {
        Profile = profile;
        Again = again;
        Why = why;
    }

    /// <summary>The profile to act as, or <see langword="null" /> where there is nobody to act as.</summary>
    public ActiveProfile? Profile { get; }

    /// <summary>
    ///     The profile whose token is missing or refused, which the add screen is filled in for so that signing in
    ///     replaces its token rather than adding a second profile — or <see langword="null" /> where there is none.
    /// </summary>
    public ProfileSummary? Again { get; }

    /// <summary>
    ///     What was wrong with <see cref="Again" />'s token, said on the add screen — or <see langword="null" />.
    /// </summary>
    public string? Why { get; }

    /// <summary>Opens acting as <paramref name="profile" />.</summary>
    public static Opening As(ActiveProfile profile) => new(profile, again: null, why: null);

    /// <summary>
    ///     Opens on adding a profile — filled in for <paramref name="again" /> where there is one, saying
    ///     <paramref name="why" />.
    /// </summary>
    public static Opening SigningIn(ProfileSummary? again, string? why) => new(profile: null, again, why);

    /// <summary>
    ///     Resolves the profile a launch acts as — <paramref name="named" />, or the default — the way a command's scope
    ///     resolves one.
    /// </summary>
    /// <remarks>
    ///     Nobody to act as opens on adding a profile: none set up, none the default, or a token missing from the
    ///     store, which are all an <see cref="AuthenticationException" /> and all fixed by signing in. A config file
    ///     that cannot be read, one naming a default that is not there, and a <c>--profile</c> naming nothing are
    ///     thrown on, because each is a mistake the reader has just made where they made it, and a form is not the
    ///     place to fix it.
    /// </remarks>
    /// <exception cref="WoolyException">The config cannot be read, or names a profile that is not there.</exception>
    public static Opening Of(IProfileRegistry registry, string? named)
    {
        try
        {
            return As(registry.Resolve(named));
        }
        catch (AuthenticationException failure)
        {
            var profiles = registry.List();
            var again = named is null
                ? profiles.SingleOrDefault(profile => profile.IsCurrent)
                : profiles.SingleOrDefault(profile => profile.Name == named);

            // Why is said only over a profile being signed in again. With none it would be somebody new told they had
            // failed at something, or the CLI's advice about profile switch on a screen that is adding a profile.
            return SigningIn(again, again is null ? null : failure.Message);
        }
    }
}
