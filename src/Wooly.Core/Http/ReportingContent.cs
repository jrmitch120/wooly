using System.Net;

namespace Wooly.Core.Http;

/// <summary>
///     A file's bytes as a request body, saying how much of it has been written as it goes (#375) — which is what an
///     attachment's row shows while it goes up, and what <see cref="StreamContent" /> keeps to itself.
/// </summary>
/// <remarks>
///     Written from the start every time it is asked for, so that a send <see cref="TransientFaultRetryHandler" />
///     tries again sends the whole file again rather than whatever the first try left unread.
/// </remarks>
/// <param name="file">The file, open for reading and able to seek.</param>
/// <param name="sent">Told the share of the file written so far, from nought to one, whenever it moves a hundredth.</param>
internal sealed class ReportingContent(Stream file, Action<double> sent) : HttpContent
{
    /// <summary>How much is written at a time: small enough that a picture's progress moves, large enough to be quick.</summary>
    private const int Chunk = 64 * 1024;

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        file.Seek(0, SeekOrigin.Begin);

        var buffer = new byte[Chunk];
        var total = Math.Max(1, file.Length);
        var written = 0L;
        var said = -1L;

        sent(0);

        int read;

        while ((read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            written += read;

            var hundredths = written * 100 / total;

            if (hundredths != said)
            {
                said = hundredths;
                sent((double)written / total);
            }
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = file.Length;

        return true;
    }
}
