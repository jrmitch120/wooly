using Wooly.Tui.Clipboard;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A clipboard that holds whatever a test puts on it — a picture, copied files, text, or no tool to read it with —
///     and counts how often it was read (#380). Text, unless said.
/// </summary>
internal sealed class FakeClipboard : IClipboard
{
    /// <summary>What the clipboard holds, which a test may change between pastes.</summary>
    public Clipped Holding { get; set; } = new Clipped.Text();

    /// <summary>How many times the clipboard was read.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public Clipped Read()
    {
        Reads++;

        return Holding;
    }
}
