using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The edit public profile form's bio counted against the instance's own limit on a bio, as compose's post is
///     (#319, #406): asked of drafting, which opens the form and hears every limit (ADR-0029) — and which hands an
///     arriving limit to the form wherever it is on the stack, as to a compose (#422, #424).
/// </summary>
public class PublicProfileLimitTests
{
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    private static readonly PostLimits Roomy = new(500, 23) { Bio = 1000 };

    /// <summary>
    ///     The bio's count is out of the instance's own limit on a bio once the instance has said it, and is counted as
    ///     a post is — an address as what the instance says one counts.
    /// </summary>
    [Fact]
    public void TheBiosCountIsOutOfTheInstancesOwnLimit()
    {
        var drafting = Over(
            FakeInstanceLimits.Setting(Roomy),
            APublicProfile.With(bio: "See https://wooly.example/a/very/long/address/indeed", account: Me));

        var form = Editing(drafting);

        drafting.Fakes.Host.Drain();

        Assert.Equal("27 / 1000", Counted(form));
    }

    /// <summary>
    ///     A limit that arrives while the file browser is open over the form for one of its pictures reaches the form all
    ///     the same, so that coming back finds the bio measured against it (#422, #424).
    /// </summary>
    [Fact]
    public void LimitsArrivingUnderTheFileBrowserReachTheForm()
    {
        var drafting = Over(FakeInstanceLimits.Setting(Roomy));
        var form = Editing(drafting);
        var browser = FileBrowserScreen.ForAPicture(
            new FolderListing(Path.GetTempPath(), "/tmp", Up: null, []),
            PublicProfilePicture.Avatar);

        drafting.Stack.Push(browser);
        drafting.Fakes.Host.Drain();
        drafting.Stack.TakeOff(browser);

        Assert.Same(form, drafting.Stack.Top);
        Assert.Equal("13 / 1000", Counted(form));
    }

    /// <summary>
    ///     An instance's limits reach the form saved on it and no other: a form opened as a profile on one instance keeps
    ///     that instance's limit while a compose written above it as a profile on another hears its own.
    /// </summary>
    [Fact]
    public void LimitsReachTheFormOnItsOwnInstanceAlone()
    {
        var drafting = Over(FakeInstanceLimits.Each(new Dictionary<string, PostLimits>
        {
            ["mastodon.social"] = Roomy,
            ["hachyderm.io"] = new(2000, 23) { Bio = 3000 },
        }));
        var form = Editing(drafting);

        drafting.ActAs(new ActiveProfile
        {
            Name = "work",
            Instance = "hachyderm.io",
            Account = "jeff@hachyderm.io",
            AccessToken = "token-work",
        });
        drafting.Drafting.Do(Verb.Compose);
        drafting.Fakes.Host.Drain();

        Assert.Equal("13 / 1000", Counted(form));
    }

    /// <summary>
    ///     Drafting over the own account's screen, whose public profile reads as <paramref name="read" /> and whose
    ///     instance says <paramref name="limits" />.
    /// </summary>
    private static ADrafting Over(FakeInstanceLimits limits, PublicProfile? read = null) => new ADrafting
    {
        Bottom = new AccountScreen(Me, [], pinned: null, isOwnAccount: true),
        Fakes =
        {
            Limits = limits,
            PublicProfile = FakePublicProfileEditor.Holding(read ?? APublicProfile.With(account: Me)),
        },
    }.Build();

    /// <summary>
    ///     The form, opened with <c>E</c> and read — and only that: what the instance says of its limits, asked as the
    ///     form went up, is left still to land.
    /// </summary>
    private static PublicProfileScreen Editing(ADrafting drafting)
    {
        drafting.Press(ShellKey.CapitalE);
        drafting.Fakes.Host.Step();

        return Assert.IsType<PublicProfileScreen>(drafting.Stack.Top);
    }

    private static string Counted(PublicProfileScreen form) => AShell.Drawn(form)[^1].Trim();
}
