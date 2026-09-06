using Microsoft.Extensions.DependencyInjection;
using Wooly.Core;
using Wooly.Core.Discovery;

namespace Wooly.Tests.Integration;

/// <summary>
///     Runs <see cref="FollowSuggestions" /> against the live instance, for the one claim the Discover screen is built
///     on and no fake can settle: Mastodon documents its suggestion sources as excluding accounts already followed,
///     dismissed or blocked, and #181 spends that documentation by skipping the batched relationships call entirely.
///     If it is false, the standing suffix comes back on the row — the call does not become this port's business.
///     <para>
///         All three exclusions are walked, each with the person put back on the list in between, because #181 leans on
///         all three and two of them holding would not carry the third.
///     </para>
///     <para>
///         One test rather than three, walking the exclusions in turn over the same person, because they cannot be
///         separated on a small instance: dismissal is one-way, so a test that dismissed somebody would take the only
///         suggestion the other tests had to work with, and which of them ran first would decide which one proved
///         anything.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(LiveInstanceCollection.Name)]
public class FollowSuggestionsIntegrationTests
{
    [Fact(Skip = LiveInstance.SkipReason, SkipType = typeof(LiveInstance), SkipUnless = nameof(LiveInstance.Available))]
    public async Task Read_StopsOfferingSomebodyOnceTheyAreFollowedBlockedOrDismissed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = LiveInstance.NewServices();
        var profile = LiveInstance.Profile;
        var suggestions = services.GetRequiredService<IFollowSuggestions>();

        // The follow goes through Mastonet by id rather than through IAccountRelationships by address, which is not a
        // shortcut but the only way round: that port looks an address up by searching for it, and the seeded instance
        // answers on a port while calling itself by bare host, so an account of its own is addressed one way and found
        // under another. That mismatch belongs to the test harness, not to either port, and dragging it in here would
        // make a suggestions test fail for a reason that has nothing to do with suggestions.
        var mastodon = services.GetRequiredService<IMastodonClientFactory>()
                               .CreateClient(profile.Instance, profile.AccessToken);

        var offered = await suggestions.Read(profile, limit: 40, cancellationToken);

        // A throwaway instance seeded with a couple of accounts often offers nobody at all: friends_of_friends needs a
        // follow graph, and most_followed and most_interactions are read from a view a scheduled job fills. Skipping
        // rather than passing, because a green tick for a claim that was never put to the test is worse than no tick.
        if (offered.Count == 0)
        {
            Assert.Skip("The live instance offered nobody, so there is no suggestion to follow or dismiss.");
        }

        // Every suggestion carries a person whatever the instance called its reason for offering them, which is the
        // part of the shape that holds even where none of the reasons is one this client has a heading for.
        Assert.All(offered, suggestion => Assert.NotEmpty(suggestion.Account.Address));

        var target = offered[0].Account;

        await mastodon.Follow(target.Id, reblogs: true);

        try
        {
            var followed = await suggestions.Read(profile, limit: 40, cancellationToken);

            Assert.DoesNotContain(followed, suggestion => suggestion.Account.Id == target.Id);
        }
        finally
        {
            await mastodon.Unfollow(target.Id);
        }

        // Back on the list once the follow is off it, which is what makes the disappearance above the follow's doing
        // rather than the instance having simply stopped offering anybody. Asserted after each leg below for the same
        // reason: three exclusions each need their own before, or the second and third prove only that the first held.
        await AssertOfferedAgain();

        // The third of the three the screen spends. Blocking is the one leg that is not otherwise reachable from
        // Discover at all — nothing on that screen blocks anybody — so it is checked here precisely because no other
        // test will stumble over it.
        await mastodon.Block(target.Id);

        try
        {
            var blocked = await suggestions.Read(profile, limit: 40, cancellationToken);

            Assert.DoesNotContain(blocked, suggestion => suggestion.Account.Id == target.Id);
        }
        finally
        {
            await mastodon.Unblock(target.Id);
        }

        await AssertOfferedAgain();

        // Left for last, because nothing puts it back: dismissal is one-way by Mastodon's own design, which is why the
        // suite runs against a throwaway instance that is torn down after it. It is also the only way to see a
        // dismissal land at all, Mastonet answering a refused DELETE with a return rather than an exception.
        await suggestions.Dismiss(profile, target.Id, cancellationToken);

        var dismissed = await suggestions.Read(profile, limit: 40, cancellationToken);
        Assert.DoesNotContain(dismissed, suggestion => suggestion.Account.Id == target.Id);

        return;

        async Task AssertOfferedAgain()
        {
            var offeredAgain = await suggestions.Read(profile, limit: 40, cancellationToken);

            Assert.Contains(offeredAgain, suggestion => suggestion.Account.Id == target.Id);
        }
    }
}
