using Wooly.Core;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A browser that opens nothing and only remembers what it was asked to open — or, where a test needs the machine
///     to have no browser at all, one that cannot open even that.
/// </summary>
internal sealed class FakeWebBrowser(bool opens = true) : IWebBrowser
{
    /// <summary>Every address the client asked to have opened, in order.</summary>
    public List<Uri> Opened { get; } = [];

    /// <summary>A machine with no browser to open — a bare server, or a session with no desktop.</summary>
    public static FakeWebBrowser WithNothingToOpen() => new(opens: false);

    /// <inheritdoc />
    public bool TryOpen(Uri url)
    {
        Opened.Add(url);

        return opens;
    }
}
