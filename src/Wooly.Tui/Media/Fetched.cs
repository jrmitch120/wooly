namespace Wooly.Tui.Media;

/// <summary>
///     What sending for a picture's file came to, as whatever does the fetching for <see cref="Pictures" /> says it:
///     the file, nothing, or a file refused for being larger than it may be (ADR-0034).
/// </summary>
/// <remarks>
///     Too large is told apart from nothing so that the Picture screen can say which of the two its original was
///     (<see cref="Unshowable" />); anything else that goes wrong — a refusal, a socket, a server that never answers —
///     is nothing, or is thrown, and either way is <see cref="Unshowable.Failed" />.
/// </remarks>
public sealed record Fetched
{
    private Fetched()
    {
    }

    /// <summary>Nothing came: there was no file to be had.</summary>
    public static Fetched None { get; } = new();

    /// <summary>A file was there, and refused for being larger than the count of bytes the fetch was handed.</summary>
    public static Fetched TooLarge { get; } = new() { IsTooLarge = true };

    /// <summary>The file, where one came.</summary>
    public byte[]? Bytes { get; private init; }

    /// <summary>Whether no file came because the one there was refused as too large.</summary>
    public bool IsTooLarge { get; private init; }

    /// <summary><paramref name="bytes" />, or <see cref="None" /> where there are none.</summary>
    public static Fetched Of(byte[]? bytes) => bytes is null ? None : new Fetched { Bytes = bytes };
}
