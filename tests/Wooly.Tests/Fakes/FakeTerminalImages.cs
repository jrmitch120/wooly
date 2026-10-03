using Wooly.Tui.Media;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A terminal's image store that writes nothing and remembers what it was told, so that "a scroll transmits
///     nothing" and "a dropped picture is forgotten" are assertions rather than escape sequences read back (#292).
/// </summary>
internal sealed class FakeTerminalImages : ITerminalImages
{
    /// <summary>Every transmission, in order.</summary>
    public List<Transmission> Transmitted { get; } = [];

    /// <summary>Every image forgotten by id, in order.</summary>
    public List<int> Forgotten { get; } = [];

    /// <summary>How many times every image was forgotten at once.</summary>
    public int ForgotAll { get; private set; }

    /// <inheritdoc />
    public void Transmit(int id, byte[] png, int columns, int rows) =>
        Transmitted.Add(new Transmission(id, png, columns, rows));

    /// <inheritdoc />
    public void Forget(int id) => Forgotten.Add(id);

    /// <inheritdoc />
    public void ForgetAll() => ForgotAll++;

    /// <summary>One picture sent to the terminal.</summary>
    public sealed record Transmission(int Id, byte[] Png, int Columns, int Rows);
}
