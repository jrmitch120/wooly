using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     Relationships are the adapter behind <see cref="IAccountRelationships" />, and ADR-0005 puts an adapter's tests
///     at the <see cref="HttpMessageHandler" /> seam. Two things are only observable here: which endpoints a tie
///     reaches, and the lookup that turns the address a user types into the id every one of those endpoints takes.
///     Commands above this fake <see cref="IAccountRelationships" /> instead.
/// </summary>
public class AccountRelationshipsTests
{
    private static readonly ActiveProfile Profile = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>
    ///     The crossing this adapter exists for: a user names an account the way Mastodon shows one, and every endpoint
    ///     underneath takes an id.
    /// </summary>
    [Fact]
    public async Task Set_LooksTheAddressUpAndActsOnTheAccountItFound()
    {
        var network = Answering(Accounts(AccountJson("alice@hachyderm.io", id: "42")), Relationship("42", following: true));

        var account = await Relationships(network)
            .Set(Profile, AccountAddress.Parse("alice@hachyderm.io"), AccountTie.Follow, wanted: true, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/search?q=alice%40hachyderm.io&limit=10&resolve=true",
            network.Requests[0].RequestUri?.ToString());

        Assert.Equal("https://mastodon.social/api/v1/accounts/42/follow", network.Requests[1].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, network.Requests[1].Method);
        Assert.Equal("42", account.Id);
        Assert.Equal("alice@hachyderm.io", account.Address);
    }

    [Theory]
    [InlineData(AccountTie.Follow, true, "follow")]
    [InlineData(AccountTie.Follow, false, "unfollow")]
    [InlineData(AccountTie.Block, true, "block")]
    [InlineData(AccountTie.Block, false, "unblock")]
    [InlineData(AccountTie.Mute, true, "mute")]
    [InlineData(AccountTie.Mute, false, "unmute")]
    public async Task Set_ReachesTheEndpointForTheTieAndWhetherItIsWanted(AccountTie tie, bool wanted, string endpoint)
    {
        var network = Answering(Accounts(AccountJson("alice@hachyderm.io", id: "42")), Relationship("42"));

        await Relationships(network)
            .Set(Profile, AccountAddress.Parse("alice@hachyderm.io"), tie, wanted, TestContext.Current.CancellationToken);

        Assert.Equal($"https://mastodon.social/api/v1/accounts/42/{endpoint}", network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>
    ///     Following a locked account leaves a request behind rather than a follow, and only the standing the instance
    ///     answered with says which happened.
    /// </summary>
    [Fact]
    public async Task Set_ReportsWhereTheProfileNowStandsWithTheAccount()
    {
        var network = Answering(
            Accounts(AccountJson("alice@hachyderm.io", id: "42")),
            Relationship("42", requested: true, followedBy: true));

        var account = await Relationships(network)
            .Set(Profile, AccountAddress.Parse("alice@hachyderm.io"), AccountTie.Follow, wanted: true, TestContext.Current.CancellationToken);

        Assert.False(account.Standing!.Following);
        Assert.True(account.Standing.FollowRequested);
        Assert.True(account.Standing.FollowedBy);
        Assert.False(account.Standing.Blocking);
        Assert.False(account.Standing.Muting);
    }

    /// <summary>
    ///     An instance answers a lookup with everything resembling the query, so only an exact match is taken — blocking
    ///     the wrong account on a near miss is not a mistake worth being helpful about.
    /// </summary>
    [Fact]
    public async Task Set_RefusesToActOnAnAccountThatIsMerelySimilar()
    {
        var network = Answering(Accounts(AccountJson("alicia@hachyderm.io", id: "43")), Relationship("43"));

        var refusal = await Assert.ThrowsAsync<UnknownAccountException>(
            () => Relationships(network).Set(
                Profile,
                AccountAddress.Parse("alice@hachyderm.io"),
                AccountTie.Block,
                wanted: true,
                TestContext.Current.CancellationToken));

        Assert.Contains("alice@hachyderm.io", refusal.Message);
        Assert.Single(network.Requests);
    }

    /// <summary>A bare username means somebody on the profile's own instance, which is how that instance lists them.</summary>
    [Fact]
    public async Task Set_TakesABareUsernameAsAnAccountOnTheProfilesOwnInstance()
    {
        var network = Answering(Accounts(AccountJson("bob", id: "7")), Relationship("7", muting: true));

        var account = await Relationships(network)
            .Set(Profile, AccountAddress.Parse("bob"), AccountTie.Mute, wanted: true, TestContext.Current.CancellationToken);

        Assert.Equal("bob@mastodon.social", account.Address);
        Assert.Equal("https://mastodon.social/api/v1/accounts/7/mute", network.Requests[1].RequestUri?.ToString());
    }

    /// <summary>Nobody named means the profile's own account, which is the one account a user never has to name.</summary>
    [Theory]
    [InlineData(FollowSide.Followers, "followers")]
    [InlineData(FollowSide.Following, "following")]
    public async Task List_ReadsTheProfilesOwnListWhenNobodyIsNamed(FollowSide side, string endpoint)
    {
        var network = Answering(AccountJson("jeff", id: "1"), Accounts(AccountJson("alice@hachyderm.io", id: "42")));

        var fetch = await Relationships(network).List(Profile, side, account: null, 20, TestContext.Current.CancellationToken);

        Assert.Equal("https://mastodon.social/api/v1/accounts/verify_credentials", network.Requests[0].RequestUri?.ToString());
        Assert.StartsWith($"https://mastodon.social/api/v1/accounts/1/{endpoint}", network.Requests[1].RequestUri?.ToString());
        Assert.Equal("alice@hachyderm.io", Assert.Single(fetch.Accounts).Address);
        Assert.True(fetch.IsComplete);
    }

    /// <summary>An account that was named is looked up first, exactly as a tie's is.</summary>
    [Fact]
    public async Task List_ReadsTheListOfTheAccountThatWasNamed()
    {
        var network = Answering(Accounts(AccountJson("alice@hachyderm.io", id: "42")), Accounts(AccountJson("bob", id: "7")));

        var fetch = await Relationships(network).List(
            Profile,
            FollowSide.Followers,
            AccountAddress.Parse("alice@hachyderm.io"),
            20,
            TestContext.Current.CancellationToken);

        Assert.StartsWith("https://mastodon.social/api/v1/accounts/42/followers", network.Requests[1].RequestUri?.ToString());
        Assert.Equal("bob@mastodon.social", Assert.Single(fetch.Accounts).Address);
    }

    /// <summary>
    ///     The list is paged by the loop a timeline and an inbox are read down, so more than a page's worth is asked
    ///     for a page at a time rather than not at all.
    /// </summary>
    [Fact]
    public async Task List_AsksForFurtherPagesUntilItHasWhatWasAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AccountJson("jeff", id: "1")),
            ScriptedHttpMessageHandler.Json(Accounts(Enumerable.Range(1, 80).Select(id => AccountJson($"a{id}", id: $"{id}")).ToArray())),
            ScriptedHttpMessageHandler.Json(Accounts(AccountJson("last", id: "999"))));

        var fetch = await Relationships(network).List(Profile, FollowSide.Following, account: null, 100, TestContext.Current.CancellationToken);

        Assert.Equal(81, fetch.Accounts.Count);
        Assert.Contains("limit=80", network.Requests[1].RequestUri?.Query);
        Assert.Contains("max_id=80", network.Requests[2].RequestUri?.Query);
    }

    /// <summary>
    ///     What already arrived is worth having, so a rate limit stops the reading rather than throwing it away — the
    ///     same bargain a timeline read makes (ADR-0007).
    /// </summary>
    [Fact]
    public async Task List_ReportsWhatArrivedBeforeTheRateLimitStoppedIt()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AccountJson("jeff", id: "1")),
            ScriptedHttpMessageHandler.Json(Accounts(Enumerable.Range(1, 80).Select(id => AccountJson($"a{id}", id: $"{id}")).ToArray())),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await Relationships(network).List(Profile, FollowSide.Followers, account: null, 100, TestContext.Current.CancellationToken);

        Assert.Equal(80, fetch.Accounts.Count);
        Assert.False(fetch.IsComplete);
        Assert.Equal("mastodon.social", fetch.StoppedBy!.Instance);
    }

    [Fact]
    public async Task PendingRequests_ReadsTheAccountsWaitingToBeLetIn()
    {
        var network = Answering(Accounts(AccountJson("alice@hachyderm.io", id: "42")));

        var fetch = await Relationships(network).PendingRequests(Profile, 20, TestContext.Current.CancellationToken);

        Assert.StartsWith("https://mastodon.social/api/v1/follow_requests", network.Requests[0].RequestUri?.ToString());

        var waiting = Assert.Single(fetch.Accounts);
        Assert.Equal("42", waiting.Id);
        Assert.Equal("alice@hachyderm.io", waiting.Address);
    }

    /// <summary>
    ///     Who asked is read before the request is answered: Mastonet's authorize and reject hand back nothing at all,
    ///     so a request accepted first would have nobody left to name.
    /// </summary>
    [Theory]
    [InlineData(true, "authorize")]
    [InlineData(false, "reject")]
    public async Task Answer_NamesWhoWasLetInOrTurnedAway(bool accepted, string endpoint)
    {
        var network = Answering(AccountJson("alice@hachyderm.io", id: "42"), "{}");

        var account = await Relationships(network).Answer(Profile, "42", accepted, TestContext.Current.CancellationToken);

        Assert.Equal("https://mastodon.social/api/v1/accounts/42", network.Requests[0].RequestUri?.ToString());
        Assert.Equal($"https://mastodon.social/api/v1/follow_requests/42/{endpoint}", network.Requests[1].RequestUri?.ToString());
        Assert.Equal("alice@hachyderm.io", account.Address);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IAccountRelationships Relationships(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IAccountRelationships>();
    }

    /// <summary>A network answering each call in turn with the JSON given, repeating the last once it runs out.</summary>
    private static ScriptedHttpMessageHandler Answering(params string[] payloads) =>
        new(payloads.Select(ScriptedHttpMessageHandler.Json).ToArray());

    private static string Accounts(params string[] accounts) => $"[{string.Join(",", accounts)}]";

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being read, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    private static string AccountJson(string account, string id) =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{account.Split('@')[0]}}",
            "acct": "{{account}}",
            "display_name": "Alice",
            "url": "https://{{(account.Contains('@') ? account.Split('@')[1] : "mastodon.social")}}/@{{account.Split('@')[0]}}",
            "created_at": "2020-01-01T00:00:00.000Z",
            "followers_count": 1203,
            "following_count": 187,
            "statuses_count": 4210
          }
          """;

    private static string Relationship(
        string id,
        bool following = false,
        bool requested = false,
        bool followedBy = false,
        bool blocking = false,
        bool muting = false) =>
        $$"""
          {
            "id": "{{id}}",
            "following": {{following.ToString().ToLowerInvariant()}},
            "requested": {{requested.ToString().ToLowerInvariant()}},
            "followed_by": {{followedBy.ToString().ToLowerInvariant()}},
            "blocking": {{blocking.ToString().ToLowerInvariant()}},
            "muting": {{muting.ToString().ToLowerInvariant()}}
          }
          """;
}
