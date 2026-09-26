using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Tui.Screens;
using Step = Wooly.Tui.Screens.AddProfileScreen.Step;

namespace Wooly.Tui.Shell;

/// <summary>
///     What each step of adding a profile does: the instance checked before anything is sent, the browser sign-in
///     begun and waited on, the token checked, and the profile written through <see cref="IProfileRegistry.Add" /> —
///     ADR-0004 as <c>profile add</c> follows it, in ADR-0020's order (#245).
/// </summary>
/// <remarks>
///     Beside the shell rather than in it, and not a screen's own verb either: it reaches <see cref="ProfilePorts" />,
///     which <see cref="Reach" /> deliberately does not carry, and it ends by standing a fresh list where the add
///     screen was opened from, which is the shell's stack. So the shell hands it the few things it needs and carries
///     the ending out itself.
///     <para>
///         Every call that leaves the machine is put through <see cref="Enquiry" />, so each has the breadcrumb's
///         fetch mark and none blocks the drawing thread. What comes back is said on the add screen rather than on the
///         status row — a refusal is about the field it is drawn under — so each step catches its own failure and the
///         enquiry is left nothing to say.
///     </para>
///     <para>
///         What lands, lands on the add screen while it is still on the stack, rather than only while it is in front:
///         a reader who opened the keymap while the browser was out has not walked away from the sign-in, and a token
///         already issued is not something to throw away for it. Once the screen is off the stack nothing lands at all,
///         and nothing is written.
///     </para>
/// </remarks>
/// <param name="enquiry">How every call that leaves the machine is put.</param>
/// <param name="profiles">The registry, the authorizer and the verifier.</param>
/// <param name="browser">Where the authorization page is opened.</param>
/// <param name="held">Whether a screen is still on the stack.</param>
/// <param name="changed">Says something on screen has changed.</param>
/// <param name="say">Puts a notice on the status row — only ever a failure to write the profile.</param>
/// <param name="confirm">Asks before going ahead, for a name already in use.</param>
/// <param name="added">What the shell does once the profile is written: back to the list, the new row picked.</param>
internal sealed class ProfileAdding(
    Enquiry enquiry,
    ProfilePorts profiles,
    IWebBrowser browser,
    Func<Screen, bool> held,
    Action changed,
    Says say,
    Action<Confirmation> confirm,
    Action<AddProfileScreen, string, ProfileAddition> added)
{
    /// <summary>
    ///     What <c>⏎</c> does on the add screen: on to the next step with what the one in front has.
    ///     Nothing while something is being waited on.
    /// </summary>
    public Task Continue(AddProfileScreen screen) => screen.At switch
    {
        Step.Instance => SignIn(screen),
        Step.Browser when !screen.Waiting => SignIn(screen),
        Step.Token => Check(screen, screen.Token),
        Step.Name => Save(screen),
        _ => Task.CompletedTask,
    };

    /// <summary>
    ///     Gives the browser up for a pasted token, ADR-0004's fallback — the wait called off and the loopback port
    ///     given back. Only from the browser step, which is the only one that offers it.
    /// </summary>
    public void PasteToken(AddProfileScreen screen)
    {
        if (screen.At != Step.Browser)
        {
            return;
        }

        screen.Pasting();
        changed();
    }

    /// <summary>
    ///     Asks the instance to register this client, then sends the browser to it and waits for it to come back — the
    ///     instance checked first, so a typo never reaches the network.
    /// </summary>
    private Task SignIn(AddProfileScreen screen)
    {
        var instance = screen.Instance;

        if (!InstanceDomain.IsWellFormed(instance))
        {
            // The same words profile add turns the value down with, so the two front ends cannot come to say
            // different things about the same typo.
            screen.Refuse(InstanceDomain.Rejection(instance));
            changed();

            return Task.CompletedTask;
        }

        var cancel = screen.Registering();

        changed();

        return enquiry.Put(
            ask => Attempted(() => ask.Of(_ => profiles.Authorizer.Begin(instance, cancel))),
            eitherWay: begun =>
            {
                if (begun is null)
                {
                    return;
                }

                // Registered after the reader walked away: the port it borrowed goes back at once.
                if (!held(screen) || screen.At != Step.Registering)
                {
                    begun.Value?.Dispose();

                    return;
                }

                if (begun.Refusal is { } why)
                {
                    screen.NotRegistered(why);
                    changed();

                    return;
                }

                var authorization = begun.Value!;

                // Drawn whether or not a browser opened, as profile add prints it: one that opened is no guarantee the
                // right one did, and one that did not leaves the address as the only way on.
                var address = authorization.AuthorizationUrl;

                screen.Sent(authorization, address, browser.TryOpen(address));
                changed();

                _ = Await(screen, authorization, cancel);
            });
    }

    /// <summary>Waits for the browser to come back, and checks the token it came back with.</summary>
    private Task Await(AddProfileScreen screen, IBrowserAuthorization authorization, CancellationToken cancel) =>
        enquiry.Put(
            ask => Attempted(() => ask.Of(_ => authorization.AwaitAccessToken(cancel))),
            eitherWay: awaited =>
            {
                // Given up — by esc, by t, or by leaving — which has already given the port back.
                if (awaited is null || !held(screen) || !screen.Waiting)
                {
                    return;
                }

                if (awaited.Refusal is { } why)
                {
                    screen.NotAuthorized(why);
                    changed();

                    return;
                }

                screen.Authorized();

                _ = Check(screen, awaited.Value!);
            });

    /// <summary>
    ///     Asks the instance who <paramref name="token" /> belongs to, before anything is written — the same check
    ///     <c>profile add</c> makes, and how the handle the name defaults to is learned.
    /// </summary>
    private Task Check(AddProfileScreen screen, string token)
    {
        if (token.Length == 0)
        {
            screen.Refuse("Paste the access token first.");
            changed();

            return Task.CompletedTask;
        }

        var instance = screen.Instance;

        screen.Checking();
        changed();

        return enquiry.Put(
            ask => Attempted(() => ask.Of(_ => profiles.Verifier.VerifyAccount(instance, token))),
            eitherWay: verified =>
            {
                if (verified is null || !held(screen) || screen.At != Step.Checking)
                {
                    return;
                }

                if (verified.Refusal is { } why)
                {
                    screen.Refused(why);
                }
                else
                {
                    screen.Verified(token, verified.Value!);
                }

                changed();
            });
    }

    /// <summary>
    ///     Writes the profile under the name typed — asking first where that name is already in use, because adding
    ///     replaces (story 43).
    /// </summary>
    private Task Save(AddProfileScreen screen)
    {
        var name = screen.Name;

        if (name.Length == 0)
        {
            // profile add's words for the same empty value.
            screen.Refuse("Give the profile a name to be known by, e.g. work.");
            changed();

            return Task.CompletedTask;
        }

        if (profiles.Registry.List().Any(profile => profile.Name == name))
        {
            confirm(new Confirmation(
                $"Replace the profile {name}?",
                () =>
                {
                    Store(screen, name);

                    return Task.CompletedTask;
                },
                Going: "replace"));

            return Task.CompletedTask;
        }

        Store(screen, name);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Writes the profile through <see cref="IProfileRegistry.Add" />, still the only writer — which also settles
    ///     whether it becomes current, as it does after <c>profile add</c>.
    /// </summary>
    private void Store(AddProfileScreen screen, string name)
    {
        ProfileAddition addition;

        try
        {
            addition = profiles.Registry.Add(
                name,
                new ProfileConfig { Instance = screen.Instance, Account = screen.Account },
                screen.Token);
        }
        catch (WoolyException failure)
        {
            say(failure.Message, isError: true);

            return;
        }

        added(screen, name, addition);
    }

    /// <summary>
    ///     What <paramref name="call" /> answered, or why it did not — or <see langword="null" /> where it was given
    ///     up, which is nothing to say to anybody.
    /// </summary>
    /// <remarks>
    ///     A failure is caught here rather than left to <see cref="Enquiry" />, whose notice would go on the status
    ///     row: a refused token is about the field on screen, and is said there. A rate limit never arrives here, being
    ///     waited out inside the call.
    /// </remarks>
    private static async Task<Outcome<T>?> Attempted<T>(Func<Task<T>> call)
    {
        try
        {
            return new Outcome<T>(await call(), Refusal: null);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (WoolyException failure)
        {
            return new Outcome<T>(default, failure.Message);
        }
    }

    /// <summary>What a step's call answered with, or the reason it gave instead.</summary>
    private sealed record Outcome<T>(T? Value, string? Refusal);
}
