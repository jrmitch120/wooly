namespace Wooly.Tui.Clipboard;

/// <summary>
///     The clipboard of the machine Wooly runs on, as far as <c>ctrl-v</c> on a compose screen needs it (#380): whether
///     it holds a picture or copied files, which are attached, or anything else, which the field's own paste takes.
/// </summary>
/// <remarks>
///     Not one of the shell's ports, for the reason a browser is not (ADR-0020): it is on this machine, not on an
///     instance. So over SSH it is the far machine's clipboard, which holds nothing a terminal pasted — and a paste
///     there falls through to the text the terminal sends, as expected rather than as a fault.
///     <para>
///         Asked on the drawing thread and answered there, because a paste the clipboard holds no picture or file for
///         has to be left to the field the key was pressed in before that field moves on. An adapter bounds how long it
///         waits.
///     </para>
/// </remarks>
public interface IClipboard
{
    /// <summary>What the clipboard holds now.</summary>
    Clipped Read();
}

/// <summary>What the clipboard was found to hold (#380).</summary>
public abstract record Clipped
{
    private Clipped()
    {
    }

    /// <summary>A picture — a screenshot, or one copied out of an app — as the bytes of a PNG.</summary>
    public sealed record Picture(byte[] Png) : Clipped;

    /// <summary>Files copied in Finder, Explorer or a file manager, by their whole paths, in the order copied.</summary>
    public sealed record Files(IReadOnlyList<string> Paths) : Clipped;

    /// <summary>
    ///     Text, or nothing at all: what the field's own paste takes as it always has, so what it says is not read here.
    /// </summary>
    public sealed record Text : Clipped;

    /// <summary>
    ///     Nothing on this machine to read the clipboard with — on Linux, neither <c>wl-paste</c> nor <c>xclip</c> —
    ///     saying <paramref name="Why" /> in words the status row can show.
    /// </summary>
    public sealed record NoTool(string Why) : Clipped;
}
