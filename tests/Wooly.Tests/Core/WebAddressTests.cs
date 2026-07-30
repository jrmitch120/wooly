using Wooly.Core;

namespace Wooly.Tests.Core;

/// <summary>
///     One invariant, in the one place a URL leaves this client as text: what is written down keeps every escape the URL
///     was made with. Worth a test of its own because the obvious way to write a <see cref="Uri" /> down —
///     <see cref="Uri.ToString" /> — does not hold it.
/// </summary>
public class WebAddressTests
{
    /// <summary>
    ///     An authorization request carries a URL inside a URL: the redirect URI arrives escaped, and the instance
    ///     compares it against the one registered with it. An address that gave those escapes back would be re-escaped
    ///     by whoever parsed it next, and reach the instance encoded twice.
    /// </summary>
    [Fact]
    public void Of_WritesAnAddressWithItsEscapesIntact()
    {
        var url = new Uri(
            "https://mastodon.social/oauth/authorize?scope=read%20write&redirect_uri=http%3A%2F%2F127.0.0.1%3A54321%2F");

        var address = WebAddress.Of(url);

        Assert.Contains("scope=read%20write", address);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A54321%2F", address);

        // Not asserted by re-parsing the address: the Uri constructor escapes a bare space back to %20, so a round trip
        // repairs exactly the damage this test is here to catch and would pass either way.
        Assert.DoesNotContain("read write", address);
    }
}
