using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
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

    /// <summary>
    ///     What the TUI's account screen opens onto: who they are, and where this profile stands with them. Two calls,
    ///     because a search answers with accounts and never with a standing.
    /// </summary>
    [Fact]
    public async Task Show_ReadsTheAccountAndWhereTheProfileStandsWithIt()
    {
        var network = Answering(
            Accounts(AccountJson("alice@hachyderm.io", id: "42")),
            Accounts(Relationship("42", following: true, followedBy: true)));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/search?q=alice%40hachyderm.io&limit=10&resolve=true",
            network.Requests[0].RequestUri?.ToString());

        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/relationships?id[]=42",
            network.Requests[1].RequestUri?.ToString());

        Assert.Equal("alice@hachyderm.io", account.Address);
        Assert.Equal(1203, account.Followers);
        Assert.True(account.Standing?.Following);
        Assert.True(account.Standing?.FollowedBy);
        Assert.False(account.Standing?.Blocking);
    }

    /// <summary>
    ///     The six facts an account says about itself, off one full payload (ADR-0019). The bio arrives as the same
    ///     HTML a post's content does and comes out as the same plain text; the joining is a day rather than an
    ///     instant, since nothing on a profile measures an age in hours.
    /// </summary>
    [Fact]
    public async Task Show_ReadsWhatTheAccountSaysAboutItself()
    {
        var network = Answering(
            Accounts(AccountJson(
                "alice@hachyderm.io",
                id: "42",
                note: "<p>Cat photographer.</p><p>She/her</p>",
                avatar: "https://hachyderm.io/avatars/alice.png",
                locked: true,
                bot: true,
                fields: [
                    FieldJson("Site", """<a href="https://alice.test">alice.test</a>""", verifiedAt: "2024-03-04T05:06:07.000+00:00"),
                    FieldJson("Pronouns", "she/her"),
                ])),
            Accounts(Relationship("42")));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Equal("Cat photographer.\n\nShe/her", account.Bio);
        Assert.Equal(new DateOnly(2020, 1, 1), account.Joined);
        Assert.True(account.IsLocked);
        Assert.True(account.IsBot);
        Assert.Equal("https://hachyderm.io/avatars/alice.png", account.AvatarUrl);

        Assert.Collection(
            account.Fields,
            site =>
            {
                Assert.Equal("Site", site.Label);
                Assert.Equal("alice.test", site.Said);
                Assert.Equal(new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero), site.Verified);
            },
            pronouns =>
            {
                Assert.Equal("Pronouns", pronouns.Label);
                Assert.Equal("she/her", pronouns.Said);
                Assert.Null(pronouns.Verified);
            });
    }

    /// <summary>
    ///     An account that filled none of it in. Empty is empty rather than absent on all of these: Mastodon sends
    ///     every one of them on every account entity it serves, so there is no state in which the question went
    ///     unasked — which is what tells them apart from a <see cref="AccountStanding" />.
    /// </summary>
    [Fact]
    public async Task Show_ReadsAnAccountThatFilledNoneOfItInAsEmptyRatherThanAbsent()
    {
        var network = Answering(
            Accounts(AccountJson("alice@hachyderm.io", id: "42", note: "", avatar: "", fields: [])),
            Accounts(Relationship("42")));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, account.Bio);
        Assert.Empty(account.Fields);
        Assert.Null(account.AvatarUrl);
        Assert.False(account.IsLocked);
        Assert.False(account.IsBot);
    }

    /// <summary>
    ///     An instance that leaves the lot out entirely — which is not a payload Mastodon sends, but is what a
    ///     client library hands back for a field it did not find, and reading a missing list as null is how a
    ///     header comes to throw on an account nobody decorated.
    /// </summary>
    [Fact]
    public async Task Show_ReadsAnAccountTheInstanceSentTheBareMinimumForWithoutFailing()
    {
        var network = Answering(
            Accounts("""{"id":"42","username":"alice","acct":"alice@hachyderm.io","display_name":"Alice"}"""),
            Accounts(Relationship("42")));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, account.Bio);
        Assert.Empty(account.Fields);
        Assert.Null(account.AvatarUrl);
        Assert.False(account.IsLocked);
        Assert.False(account.IsBot);
    }

    /// <summary>
    ///     The note the profile wrote about the account, which rides on every relationship this client reads and was
    ///     being dropped. Read-only: writing one is out of scope (ADR-0012).
    /// </summary>
    [Fact]
    public async Task Show_ReadsThePrivateNoteTheProfileWroteAboutTheAccount()
    {
        var network = Answering(
            Accounts(AccountJson("alice@hachyderm.io", id: "42")),
            Accounts(Relationship("42", note: "Met at FOSDEM")));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Equal("Met at FOSDEM", account.Standing?.Note);
    }

    /// <summary>A profile that wrote no note gets an empty string from the wire, which is nothing rather than "".</summary>
    [Fact]
    public async Task Show_ReadsAnUnwrittenNoteAsNothing()
    {
        var network = Answering(
            Accounts(AccountJson("alice@hachyderm.io", id: "42")),
            Accounts(Relationship("42")));

        var account = await Relationships(network)
            .Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), TestContext.Current.CancellationToken);

        Assert.Null(account.Standing?.Note);
    }

    /// <summary>
    ///     Only an exact match is taken, the same way <see cref="IAccountRelationships.Set" /> takes one: showing the
    ///     wrong account is how somebody comes to follow, block or mute the wrong account from the screen it opens.
    /// </summary>
    [Fact]
    public async Task Show_RefusesAnAccountThatOnlyResemblesTheOneAskedFor()
    {
        var network = Answering(Accounts(AccountJson("alicia@hachyderm.io", id: "43")));
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<UnknownAccountException>(
            () => Relationships(network).Show(Profile, AccountAddress.Parse("alice@hachyderm.io"), cancellationToken));

        Assert.Single(network.Requests);
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
        Assert.Equal("alice@hachyderm.io", Assert.Single(fetch.Items).Address);
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
        Assert.Equal("bob@mastodon.social", Assert.Single(fetch.Items).Address);
    }

    /// <summary>
    ///     The list is paged by the loop a timeline and an inbox are read down, so more than a page's worth is asked
    ///     for a page at a time rather than not at all — and the next page starts where the instance said it does.
    /// </summary>
    [Fact]
    public async Task List_AsksForFurtherPagesUntilItHasWhatWasAskedFor()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AccountJson("jeff", id: "1")),
            Page(FullPage(), nextPageAt: "4801"),
            Page(Accounts(AccountJson("last", id: "999")), nextPageAt: null));

        var fetch = await Relationships(network).List(Profile, FollowSide.Following, account: null, 100, TestContext.Current.CancellationToken);

        Assert.Equal(81, fetch.Items.Count);
        Assert.Contains("limit=80", network.Requests[1].RequestUri?.Query);
        Assert.Contains("max_id=4801", network.Requests[2].RequestUri?.Query);
    }

    /// <summary>
    ///     Mastodon pages these lists by the id of the follow, not of the account followed, so an instance that names
    ///     no next page has ended the list. Guessing one out of the last account's id would ask for a page starting
    ///     somewhere in another id space altogether, and silently skip or repeat accounts.
    /// </summary>
    [Fact]
    public async Task List_StopsWhereTheInstanceNamedNoFurtherPageRatherThanGuessingWhereOneWouldStart()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Json(AccountJson("jeff", id: "1")),
            Page(FullPage(), nextPageAt: null),
            Page(Accounts(AccountJson("never-read", id: "999")), nextPageAt: null));

        var fetch = await Relationships(network).List(Profile, FollowSide.Followers, account: null, 100, TestContext.Current.CancellationToken);

        Assert.Equal(80, fetch.Items.Count);
        Assert.True(fetch.IsComplete);
        Assert.Equal(2, network.Requests.Count);
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
            Page(FullPage(), nextPageAt: "4801"),
            ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var fetch = await Relationships(network).List(Profile, FollowSide.Followers, account: null, 100, TestContext.Current.CancellationToken);

        Assert.Equal(80, fetch.Items.Count);
        Assert.False(fetch.IsComplete);
        Assert.Equal("mastodon.social", fetch.StoppedBy!.Instance);
    }

    [Fact]
    public async Task PendingRequests_ReadsTheAccountsWaitingToBeLetIn()
    {
        var network = Answering(Accounts(AccountJson("alice@hachyderm.io", id: "42")));

        var fetch = await Relationships(network).PendingRequests(Profile, 20, TestContext.Current.CancellationToken);

        Assert.StartsWith("https://mastodon.social/api/v1/follow_requests", network.Requests[0].RequestUri?.ToString());

        var waiting = Assert.Single(fetch.Items);
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

    /// <summary>
    ///     The fifth call on this port, and the only one Mastonet 3.1.3 cannot make: a raw <c>GET</c> over the same
    ///     named client, carrying the same token, sending the account in the repeated-array form the endpoint takes —
    ///     the form <c>/accounts/relationships</c> is already asked in.
    /// </summary>
    [Fact]
    public async Task FamiliarFollowers_AsksTheEndpointWhoTheProfileAndTheAccountBothFollow()
    {
        var network = Answering(FamiliarJson("42", AccountJson("jon@hachyderm.io", id: "7"), AccountJson("sam", id: "8")));

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://mastodon.social/api/v1/accounts/familiar_followers?id[]=42",
            network.Requests[0].RequestUri?.ToString());

        Assert.Equal(HttpMethod.Get, network.Requests[0].Method);
        Assert.Equal("Bearer token-personal", network.Requests[0].Headers.Authorization?.ToString());

        Assert.Collection(
            common!,
            jon => Assert.Equal("jon@hachyderm.io", jon.Address),
            sam => Assert.Equal("sam@mastodon.social", sam.Address));
    }

    /// <summary>
    ///     Nobody in common is an answer. It is the empty list rather than nothing, because nothing is reserved for the
    ///     question never having been put — ADR-0012's absent-versus-empty distinction, one level further out.
    /// </summary>
    [Fact]
    public async Task FamiliarFollowers_ReadsNobodyInCommonAsEmptyRatherThanAsNothing()
    {
        var network = Answering(FamiliarJson("42"));

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.NotNull(common);
        Assert.Empty(common);
    }

    /// <summary>
    ///     An instance that names no entry at all for the account has still answered, and what it answered is nobody.
    ///     Reading that as "not asked" would leave the screen saying a call it made was never made.
    /// </summary>
    [Fact]
    public async Task FamiliarFollowers_ReadsAnInstanceThatNamedNoEntryAsNobodyInCommon()
    {
        var network = Answering("[]");

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.NotNull(common);
        Assert.Empty(common);
    }

    /// <summary>
    ///     A body that is nothing at all is not a list of nobody. It is not a payload this endpoint documents, but it
    ///     is one an instance can send, and reading it as "nobody in common" would put a claim on screen that the
    ///     instance never made.
    /// </summary>
    [Fact]
    public async Task FamiliarFollowers_AnswersNothingWhereTheInstanceSentNoBodyToRead()
    {
        var network = Answering("null");

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Null(common);
    }

    /// <summary>
    ///     The call that must not take a screen down with it. It is asked last of the four an account screen makes, so
    ///     a rate limit here costs one decorative row — which is only true if it is answered rather than thrown.
    /// </summary>
    [Fact]
    public async Task FamiliarFollowers_AnswersNothingWhereARateLimitStoppedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.TooManyRequests));

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Null(common);
    }

    /// <summary>An instance that turned the call down said nothing about who is in common, which is not nobody.</summary>
    [Fact]
    public async Task FamiliarFollowers_AnswersNothingWhereTheInstanceRefusedTheCall()
    {
        var network = new ScriptedHttpMessageHandler(
            ScriptedHttpMessageHandler.Refusal(HttpStatusCode.NotFound, "Record not found"));

        var common = await Relationships(network).FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Null(common);
    }

    /// <summary>A network the retries could not ride out is the same news as a refusal: the question went unput.</summary>
    [Fact]
    public async Task FamiliarFollowers_AnswersNothingWhereTheInstanceCouldNotBeReached()
    {
        var common = await Relationships(ScriptedHttpMessageHandler.AlwaysUnreachable())
            .FamiliarFollowers(Profile, "42", TestContext.Current.CancellationToken);

        Assert.Null(common);
    }

    /// <summary>Resolved from the container the app builds, so the wiring is under test alongside the behavior.</summary>
    private static IAccountRelationships Relationships(HttpMessageHandler network)
    {
        var services = new ServiceCollection();
        services.AddWoolyCore();

        // The one thing swapped out of the real container: a test that fakes an unreachable instance would otherwise
        // spend the retry policy's backoff waiting it out for real.
        services.AddSingleton<IRetryDelay>(new RecordingRetryDelay());
        services.AddHttpClient(WoolyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);

        return services.BuildServiceProvider().GetRequiredService<IAccountRelationships>();
    }

    /// <summary>A network answering each call in turn with the JSON given, repeating the last once it runs out.</summary>
    private static ScriptedHttpMessageHandler Answering(params string[] payloads) =>
        new(payloads.Select(ScriptedHttpMessageHandler.Json).ToArray());

    private static string Accounts(params string[] accounts) => $"[{string.Join(",", accounts)}]";

    /// <summary>A page as full as the endpoint serves, which is what makes the loop ask whether there is another.</summary>
    private static string FullPage() =>
        Accounts(Enumerable.Range(1, 80).Select(id => AccountJson($"a{id}", $"{id}")).ToArray());

    /// <summary>
    ///     One page of a list, with the link header an instance names the next page in.
    /// </summary>
    /// <param name="nextPageAt">
    ///     Where the next page starts, in the id space the endpoint pages by — a follow's id, which is nothing like any
    ///     account id on the page — or <see langword="null" /> for an instance saying this is the last page.
    /// </param>
    private static Func<HttpRequestMessage, HttpResponseMessage> Page(string json, string? nextPageAt) =>
        _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            if (nextPageAt is not null)
            {
                response.Headers.TryAddWithoutValidation(
                    "Link",
                    $"""<https://mastodon.social/api/v1/accounts/1/followers?max_id={nextPageAt}>; rel="next" """);
            }

            return response;
        };

    /// <param name="account">
    ///     The wire's <c>acct</c>: bare for an account on the instance being read, <c>username@instance</c> for one
    ///     anywhere else.
    /// </param>
    private static string AccountJson(
        string account,
        string id,
        string note = "<p>Hello</p>",
        string avatar = "https://hachyderm.io/avatars/default.png",
        bool locked = false,
        bool bot = false,
        string[]? fields = null) =>
        $$"""
          {
            "id": "{{id}}",
            "username": "{{account.Split('@')[0]}}",
            "acct": "{{account}}",
            "display_name": "Alice",
            "url": "https://{{(account.Contains('@') ? account.Split('@')[1] : "mastodon.social")}}/@{{account.Split('@')[0]}}",
            "note": "{{note.Replace("\"", "\\\"")}}",
            "avatar": "{{avatar}}",
            "locked": {{Json(locked)}},
            "bot": {{Json(bot)}},
            "fields": [{{string.Join(",", fields ?? [])}}],
            "created_at": "2020-01-01T00:00:00.000Z",
            "followers_count": 1203,
            "following_count": 187,
            "statuses_count": 4210
          }
          """;

    /// <param name="verifiedAt">When the instance proved the row's link belongs to the account, or nothing for a row nobody proved.</param>
    private static string FieldJson(string label, string said, string? verifiedAt = null) =>
        $$"""
          {
            "name": "{{label}}",
            "value": "{{said.Replace("\"", "\\\"")}}",
            "verified_at": {{(verifiedAt is null ? "null" : $"\"{verifiedAt}\"")}}
          }
          """;

    private static string Json(bool flag) => flag.ToString().ToLowerInvariant();

    /// <summary>
    ///     What <c>/accounts/familiar_followers</c> answers with: one entry per id it was asked about, each naming the
    ///     id and the accounts in common. Written out here because Mastonet has no entity for this endpoint.
    /// </summary>
    private static string FamiliarJson(string id, params string[] accounts) =>
        $$"""[{"id":"{{id}}","accounts":[{{string.Join(",", accounts)}}]}]""";

    /// <param name="note">
    ///     The wire's <c>note</c>, which on a relationship is the profile's own private note about the account — not
    ///     the account's bio, whatever Mastonet's doc comment says. Empty is what an instance sends for a note nobody
    ///     wrote.
    /// </param>
    private static string Relationship(
        string id,
        bool following = false,
        bool requested = false,
        bool followedBy = false,
        bool blocking = false,
        bool muting = false,
        string note = "") =>
        $$"""
          {
            "id": "{{id}}",
            "following": {{Json(following)}},
            "requested": {{Json(requested)}},
            "followed_by": {{Json(followedBy)}},
            "blocking": {{Json(blocking)}},
            "muting": {{Json(muting)}},
            "note": "{{note}}"
          }
          """;
}
