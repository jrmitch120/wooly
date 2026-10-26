using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Drafting;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Drafting built over the shell builder's fakes and a list for a stack, without a shell (ADR-0029) — so a test of
///     drafting sets up only what drafting uses, and asserts on what is on the stack, what the status row says and what
///     reached the instance's fakes.
/// </summary>
/// <remarks>
///     The reach drafting reads is a real one over fake ports, built afresh by <see cref="ActAs" /> as a switch builds the
///     shell's — so a test of what follows a switch is asserting that drafting reads whoever is acted as now.
/// </remarks>
internal sealed class ADrafting
{
    private Reach? _reach;

    /// <summary>The fakes, which are the shell builder's own: set them before <see cref="Build" />.</summary>
    public AShell Fakes { get; } = new();

    /// <summary>The screen at the bottom of the stack: Home, holding one of the profile's own posts, unless said.</summary>
    public Screen? Bottom { get; set; }

    /// <summary>The stack drafting pushes onto and takes its screens off, once built.</summary>
    public ListStack Stack { get; private set; } = null!;

    /// <summary>Drafting itself, once built.</summary>
    public Wooly.Tui.Drafting.Drafting Drafting { get; private set; } = null!;

    /// <summary>The status row drafting says things on through its reach, once built (#436).</summary>
    public StatusRow StatusRow { get; private set; } = null!;

    /// <summary>
    ///     What the status row says, or nothing — set by a test where something said before stands for what drafting
    ///     should leave alone or replace.
    /// </summary>
    public string? Notice
    {
        get => StatusRow.Notice;
        set => StatusRow.Say(value, isError: false);
    }

    /// <summary>Whether what the status row says is a refusal or a failure.</summary>
    public bool NoticeIsError => StatusRow.NoticeIsError;

    /// <summary>How many times drafting has said something on screen has changed.</summary>
    public int Changes { get; private set; }

    /// <summary>How many times the status row has changed — told what to say, nothing among it, or asked.</summary>
    public int Said { get; private set; }

    /// <summary>What a screen can reach as whoever is acted as now, for a test to answer a screen's own verb with.</summary>
    public Reach Reach => _reach!;

    /// <summary>Who is acted as: the shell builder's profile, until <see cref="ActAs" /> says otherwise.</summary>
    public ActiveProfile Profile { get; private set; } = null!;

    /// <summary>
    ///     Builds drafting over the fakes as they are now, acting as the shell builder's profile — with what it asked as
    ///     it started answered, as a shell that has opened has had it answered.
    /// </summary>
    public ADrafting Build()
    {
        StatusRow = new StatusRow(Fakes.Host.After, Fakes.Timing.WidthSaidFor, () => Said++);
        Stack = new ListStack(Bottom ?? Home(APost.With()), () => StatusRow.Clear());
        Drafting = new Wooly.Tui.Drafting.Drafting(
            Stack,
            () => _reach!,
            Fakes.Ports,
            Fakes.Host,
            Fakes.Clipboard,
            Fakes.Pasted,
            Fakes.Preferred,
            Fakes.LaunchedFrom ?? Environment.CurrentDirectory);
        Stack.Hooked = Drafting;

        ActAs(Fakes.Profile);
        Fakes.Host.Drain();

        return this;
    }

    /// <summary>
    ///     Acts as <paramref name="profile" /> from now on, as a switch does: a fresh reach in place of the old one, and
    ///     drafting told whom the session acts as.
    /// </summary>
    public void ActAs(ActiveProfile profile)
    {
        Profile = profile;

        var enquiry = new Enquiry(
            Fakes.Host,
            Fakes.Clock,
            Fakes.Timing.CountdownStep,
            Fakes.Timing.MarkStep,
            () => Stack.Top);

        enquiry.Said += StatusRow.Say;

        var arrival = new Arrival(
            profile,
            Fakes.Ports,
            enquiry,
            new SubjectCache(Fakes.Clock, Fakes.Timing.CacheFor),
            showing: () => DestinationKind.Home,
            new SessionMarkers());

        _reach = new Reach(
            profile,
            Fakes.Ports,
            enquiry,
            arrival,
            say: StatusRow.Say,
            confirm: StatusRow.Ask,
            changed: () => Changes++,
            browse: _ => { });

        Drafting.ActingAs(profile);
    }

    /// <summary>
    ///     Presses <paramref name="key" /> at drafting, as the shell hands it over: what <see cref="Keymap" /> says it means
    ///     on the screen on top.
    /// </summary>
    /// <returns>What drafting made of it.</returns>
    public Drafted Press(ShellKey key) => Drafting.Do(Keymap.Means(key, Stack.Top));

    /// <summary>Home, holding <paramref name="posts" />, the first picked.</summary>
    public static FeedScreen Home(params Post[] posts) => new(new Destination(DestinationKind.Home, "Home"), posts);

    /// <summary>The compose screen on top, which a test that opened one goes on to read.</summary>
    public ComposeScreen Composing => Assert.IsType<ComposeScreen>(Stack.Top);
}

/// <summary>What the description editor's view does with what is typed into it, for tests with no view in the room.</summary>
internal static class Writing
{
    /// <summary>
    ///     <paramref name="description" /> written into the description editor in front, as its view writes it: through
    ///     drafting.
    /// </summary>
    public static DraftChange Write(this Wooly.Tui.Drafting.Drafting drafting, string description) =>
        drafting.Edit<DescriptionScreen>(describing => describing.Rewrite(description));
}
