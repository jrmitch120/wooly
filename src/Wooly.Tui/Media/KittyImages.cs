using System.Text;

namespace Wooly.Tui.Media;

/// <summary>
///     The Kitty graphics protocol's escape sequences for <see cref="ITerminalImages" />, written to the terminal as
///     they are said (ADR-0022).
/// </summary>
/// <remarks>
///     Every command carries <c>q=2</c>, so the terminal answers none of them: an answer would arrive on the input loop
///     as keys nobody pressed.
/// </remarks>
/// <param name="write">
///     Where the sequences go. Called on the UI thread, before the frame whose cells draw what was sent.
/// </param>
internal sealed class KittyImages(Action<string> write) : ITerminalImages
{
    /// <summary>
    ///     How much of a PNG's base64 goes in one command. The protocol's own limit, which a larger chunk breaks.
    /// </summary>
    private const int Chunk = 4096;

    private const string Start = "\u001b_G";

    private const string End = "\u001b\\";

    /// <inheritdoc />
    public void Transmit(int id, byte[] png, int columns, int rows)
    {
        var payload = Convert.ToBase64String(png);
        var sequence = new StringBuilder(payload.Length + ((payload.Length / Chunk) + 2) * 32);

        // Sent with t=d, the data itself, as a PNG (f=100) the terminal decodes. m=1 on every chunk but the last.
        for (var at = 0; at < payload.Length; at += Chunk)
        {
            var more = at + Chunk < payload.Length ? 1 : 0;

            sequence
                .Append(Start)
                .Append(at == 0 ? $"a=t,f=100,t=d,i={id},q=2,m={more};" : $"m={more};")
                .Append(payload, at, Math.Min(Chunk, payload.Length - at))
                .Append(End);
        }

        // The virtual placement: U=1 is "draw me wherever my placeholder cells are", over this many of them.
        sequence.Append(Start).Append($"a=p,U=1,i={id},c={columns},r={rows},q=2").Append(End);

        write(sequence.ToString());
    }

    /// <inheritdoc />
    /// <remarks>An upper-case <c>I</c>, which frees the image's data as well as taking it off the screen.</remarks>
    public void Forget(int id) => write($"{Start}a=d,d=I,i={id},q=2{End}");

    /// <inheritdoc />
    public void ForgetAll() => write($"{Start}a=d,d=A,q=2{End}");
}
