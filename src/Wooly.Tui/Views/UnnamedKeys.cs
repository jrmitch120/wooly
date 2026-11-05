using System.Diagnostics;
using System.Reflection;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Wooly.Tui.Views;

/// <summary>
///     A key the terminal sent in words Terminal.Gui takes for a key but cannot name, let go of rather than allowed to
///     end the session: the main loop is told to carry on past it (<see cref="CarryOn" />), and everything else thrown
///     there still ends the run as it did.
/// </summary>
/// <remarks>
///     <para>
///         Terminal.Gui 2.5.0 picks the pattern a sequence is read by on its shape alone — <c>ESC [ n ~</c>, <c>ESC O x</c>,
///         <c>ESC [ n u</c> — and only then looks the key up. Where the lookup finds nothing, its
///         <c>AnsiKeyboardParserPattern.GetKey</c> copies the key it did not find, which throws a
///         <see cref="NullReferenceException" /> out of the input loop. The Menu key (<c>ESC[29~</c>, as Windows Terminal
///         sends it), F13 and above (<c>ESC[25~</c>), rxvt's Home and End (<c>ESC[7~</c>, <c>ESC[8~</c>), a bracketed
///         paste's end with no start before it (<c>ESC[201~</c>), <c>ESC O O</c> and a kitty key report whose code is no
///         character all ended wooly with that.
///     </para>
///     <para>
///         Carrying on is safe there: Terminal.Gui has already forgotten the sequence before it tries to name it, and
///         takes what the terminal sent one character at a time off a queue it has already taken the failing one from,
///         so the next pass of the loop goes on reading at the character after it. The key is lost, which it would have
///         been anyway: nothing in the TUI is bound to one Terminal.Gui cannot name.
///     </para>
///     <para>
///         Recognised by where it was thrown rather than by its type alone, so that a <see cref="NullReferenceException" />
///         of wooly's own — one thrown by a view handling a key Terminal.Gui did name, which is reached through the same
///         <c>RaiseKeyboardEvent</c> — still ends the session and is seen.
///     </para>
/// </remarks>
internal static class UnnamedKeys
{
    private const string Parser = "Terminal.Gui.Drivers.AnsiResponseParserBase";

    private const string Raising = "RaiseKeyboardEvent";

    /// <summary>
    ///     Whether the main loop is to carry on past <paramref name="thrown" />: only where it is Terminal.Gui failing to
    ///     name a key, thrown from naming it.
    /// </summary>
    public static bool CarryOn(Exception thrown) => thrown is NullReferenceException && ThrownNamingAKey(thrown);

    /// <summary>
    ///     Whether every frame from the one <paramref name="thrown" /> was thrown in up to Terminal.Gui raising a key is
    ///     Terminal.Gui naming that key: raising it, its pattern's <c>GetKey</c> or the copy that makes. The ones between
    ///     can be inlined away in a release build, as they were in the crash this is for, where the throw was reported
    ///     from <c>RaiseKeyboardEvent</c> itself.
    /// </summary>
    private static bool ThrownNamingAKey(Exception thrown)
    {
        foreach (var frame in new StackTrace(thrown, false).GetFrames())
        {
            var method = frame.GetMethod();

            if (method is { Name: Raising, DeclaringType.FullName: Parser })
            {
                return true;
            }

            if (!Naming(method))
            {
                return false;
            }
        }

        return false;
    }

    private static bool Naming(MethodBase? method) =>
        method switch
        {
            { Name: nameof(AnsiKeyboardParserPattern.GetKey) } => method.DeclaringType == typeof(AnsiKeyboardParserPattern),
            ConstructorInfo constructor => constructor.DeclaringType == typeof(Key),
            _ => false,
        };
}
