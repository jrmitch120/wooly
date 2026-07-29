using System.Net;
using System.Net.Http;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Core;

/// <summary>
///     The listener is driven here exactly as a browser drives it — over a real socket on the loopback interface, with
///     <see cref="HttpClient" /> standing in for the browser. Nothing leaves the machine, so this stays a unit test in
///     everything but the byte or two crossing 127.0.0.1, and it is the only way to show that what the listener writes
///     back is something a browser will actually accept.
/// </summary>
public class LoopbackRedirectListenerTests
{
    [Fact]
    public async Task AwaitRedirect_ReportsTheAuthorizationTheBrowserWasSentBackWith()
    {
        using var listener = LoopbackRedirectListener.Start();

        var redirect = listener.AwaitRedirect(TestContext.Current.CancellationToken);
        await SendBrowserTo(listener.RedirectUri, "?code=code-abc&state=state-xyz");

        Assert.Equal("code-abc", (await redirect).Code);
        Assert.Equal("state-xyz", (await redirect).State);
    }

    /// <summary>
    ///     The browser stays on whatever the redirect lands on, so that page is the last thing the user sees of this
    ///     flow. It has to tell them the terminal is where the rest happens.
    /// </summary>
    [Fact]
    public async Task AwaitRedirect_LeavesTheBrowserOnAPageSendingTheUserBackToTheTerminal()
    {
        using var listener = LoopbackRedirectListener.Start();

        var redirect = listener.AwaitRedirect(TestContext.Current.CancellationToken);
        var page = await SendBrowserTo(listener.RedirectUri, "?code=code-abc");
        await redirect;

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("terminal", await ReadPage(page), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AwaitRedirect_ReportsAnInstanceTurningTheRequestDownRatherThanWaitingOn()
    {
        using var listener = LoopbackRedirectListener.Start();

        var redirect = listener.AwaitRedirect(TestContext.Current.CancellationToken);
        await SendBrowserTo(listener.RedirectUri, "?error=access_denied&error_description=The+request+was+denied");

        Assert.Equal("access_denied", (await redirect).Error);
        Assert.Equal("The request was denied", (await redirect).ErrorDescription);
    }

    /// <summary>
    ///     A browser asks for more than the address it was sent to — a favicon most of all. Answering one of those as
    ///     though it were the redirect would end the flow with no authorization at all.
    /// </summary>
    [Fact]
    public async Task AwaitRedirect_KeepsWaitingThroughARequestThatCarriesNoAuthorization()
    {
        using var listener = LoopbackRedirectListener.Start();

        var redirect = listener.AwaitRedirect(TestContext.Current.CancellationToken);
        var favicon = await SendBrowserTo(listener.RedirectUri, "favicon.ico");

        Assert.Equal(HttpStatusCode.NotFound, favicon.StatusCode);
        Assert.False(redirect.IsCompleted);

        await SendBrowserTo(listener.RedirectUri, "?code=code-abc");

        Assert.Equal("code-abc", (await redirect).Code);
    }

    /// <summary>Whoever is waiting has to be able to stop waiting — nothing guarantees the browser ever comes back.</summary>
    [Fact]
    public async Task AwaitRedirect_StopsWaitingWhenItsCallerGivesUp()
    {
        using var listener = LoopbackRedirectListener.Start();
        using var givenUp = new CancellationTokenSource();

        var redirect = listener.AwaitRedirect(givenUp.Token);
        await givenUp.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redirect);
    }

    private static async Task<HttpResponseMessage> SendBrowserTo(Uri redirectUri, string target)
    {
        using var browser = new HttpClient();

        return await browser.GetAsync(new Uri(redirectUri, target), TestContext.Current.CancellationToken);
    }

    private static Task<string> ReadPage(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
