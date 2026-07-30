using Spectre.Console;

namespace Wooly.Cli.Commands;

/// <summary>
///     Whether to go ahead with something that cannot be undone. One rule, shared by every command that has one, so that
///     two of them cannot come to strike a different bargain with the same user.
/// </summary>
internal static class Consent
{
    /// <summary>Asks <paramref name="question" />, unless it has already been answered or there is nobody to ask.</summary>
    /// <param name="alreadySaidYes">
    ///     What <c>--yes</c> was given as: a person saying on the command line what they would otherwise say at the
    ///     prompt.
    /// </param>
    /// <remarks>
    ///     A person at a terminal is asked, because these are the commands whose effect nothing else undoes. A script is
    ///     not: there is nothing to prompt at and nobody to read the prompt, and stopping to ask would make the command
    ///     unusable in the automation the CLI exists for. Typing the command is that invocation's consent.
    /// </remarks>
    public static bool Given(IAnsiConsole console, bool alreadySaidYes, string question)
    {
        if (alreadySaidYes || !console.Profile.Capabilities.Interactive)
        {
            return true;
        }

        return console.Confirm(question, defaultValue: false);
    }
}
