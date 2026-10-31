using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The keymap swept for ways out of a touched draft that skip the discard question (#426): from every draft screen
///     and every screen over one, every key the keymap gives a verb is asked of the shell, and none may leave the draft
///     off the stack without the question open.
/// </summary>
/// <remarks>
///     Through <see cref="Pressing" /> rather than a window, so that a key a field would take as a letter is still
///     asked: what is swept is what the shell does with each verb, wherever the keymap would hand it over.
/// </remarks>
public class DraftResetSweepTests : IDisposable
{
    /// <summary>The account the profile signs in as, so that <b>You</b> is its own and the form opens there.</summary>
    private static readonly Account Me = AnAccount.With(address: "jeff@mastodon.social", author: "Jeff", id: "7");

    /// <summary>Short enough that a compose's attachment rows fold into the Media header's line (story 58).</summary>
    private const int Short = 15;

    private readonly TemporaryDirectory _files = new();

    public void Dispose() => _files.Dispose();

    /// <summary>Where the sweep stands: each draft screen, and each screen that goes over one.</summary>
    public static TheoryData<string> Standing => new(
        "compose",
        "compose opened on Search",
        "file browser",
        "description editor",
        "attachments screen",
        "help over a draft",
        "public profile form",
        "file browser over the form");

    /// <summary>
    ///     Every key, pressed on <paramref name="standing" /> over a touched draft, either leaves the draft on the stack
    ///     or asks first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Standing))]
    public async Task NoKeyTakesATouchedDraftOffWithoutAsking(string standing)
    {
        var skipped = new List<string>();

        foreach (var key in Enum.GetValues<ShellKey>())
        {
            var (shell, built, draft) = await Over(standing);
            var verb = Keymap.Means(key, shell.Screen);

            if (verb is Verb.None || CarriesOut(verb))
            {
                continue;
            }

            shell.Do(verb, Keymap.Answer(key));
            built.Host.SettleAll();
            built.Host.Drain();

            if (shell.Asking is null && !shell.Crumbs.Contains(draft.Crumb))
            {
                skipped.Add($"{key} ({verb})");
            }
        }

        Assert.Empty(skipped);
    }

    /// <summary>
    ///     The verbs that take a draft off by carrying it out rather than throwing it away, which is no discarding:
    ///     sending a post (which the window carries, needing a terminal) and saving the public profile.
    /// </summary>
    private static bool CarriesOut(Verb verb) => verb is Verb.Send or Verb.SavePublicProfile;

    /// <summary>A shell standing on <paramref name="standing" />, the touched draft under it, and the draft itself.</summary>
    private async Task<(Shell Shell, AShell Built, Screen Draft)> Over(string standing)
    {
        switch (standing)
        {
            case "compose":
            {
                var (shell, built, compose) = await Composing(1);

                return (shell, built, compose);
            }

            case "compose opened on Search":
            {
                var (shell, built, compose) = await Composing(1, DestinationKind.Search);

                return (shell, built, compose);
            }

            case "file browser":
            {
                var (shell, built, compose) = await Composing(1);

                shell.Press(ShellKey.CtrlO);

                Assert.IsType<FileBrowserScreen>(shell.Screen);

                return (shell, built, compose);
            }

            case "description editor":
            {
                var (shell, built, compose) = await Composing(1);

                _ = AShell.Drawn(compose);
                shell.Press(ShellKey.Up);
                shell.Press(ShellKey.Enter);

                Assert.IsType<DescriptionScreen>(shell.Screen);

                return (shell, built, compose);
            }

            case "attachments screen":
            {
                var (shell, built, compose) = await Listing();

                return (shell, built, compose);
            }

            case "help over a draft":
            {
                var (shell, built, compose) = await Listing();

                shell.Press(ShellKey.Question);

                Assert.IsType<HelpScreen>(shell.Screen);

                return (shell, built, compose);
            }

            case "public profile form":
            {
                var (shell, built, form) = await Editing();

                return (shell, built, form);
            }

            case "file browser over the form":
            {
                var (shell, built, form) = await Editing();

                // Up from the toggle the form was touched on, since the walk stops at either end rather than wrapping.
                while (form.Typing.Part != PublicProfilePart.Avatar)
                {
                    Assert.True(shell.Press(ShellKey.Up));
                }

                shell.Press(ShellKey.Enter);

                Assert.IsType<FileBrowserScreen>(shell.Screen);

                return (shell, built, form);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(standing), standing, null);
        }
    }

    /// <summary>
    ///     A compose opened with <c>c</c> on <paramref name="on" />, with <paramref name="files" /> files attached through
    ///     the file browser. On Search, <c>/</c> is a fresh prompt rather than a way to it, which is a reset of its own.
    /// </summary>
    private async Task<(Shell Shell, AShell Built, ComposeScreen Compose)> Composing(
        int files,
        DestinationKind on = DestinationKind.Home)
    {
        var built = new AShell { LaunchedFrom = _files.Path };
        var shell = await built.Opened();

        shell.Rail.GoTo(shell.Rail.Of(on));
        built.Host.Settle();

        shell.Press(ShellKey.C);

        var compose = Assert.IsType<ComposeScreen>(shell.Screen);

        for (var file = 0; file < files; file++)
        {
            _files.WriteFile($"{file}.png");
        }

        shell.Press(ShellKey.CtrlO);

        var browser = Assert.IsType<FileBrowserScreen>(shell.Screen);

        for (var file = 0; file < files; file++)
        {
            browser.Pick(file + 1);
            shell.Press(ShellKey.Space);
        }

        shell.Press(ShellKey.Enter);
        built.Host.Drain();

        Assert.Same(compose, shell.Screen);
        Assert.Equal(files, compose.Media.Attachments.Count);
        Assert.True(compose.Touched);

        return (shell, built, compose);
    }

    /// <summary>A compose with three files attached, their rows folded, and the attachments screen listing them.</summary>
    private async Task<(Shell Shell, AShell Built, ComposeScreen Compose)> Listing()
    {
        var (shell, built, compose) = await Composing(3);

        _ = compose.Lines(new Drawing(78, AShell.Now, Height: Short)).ToList();
        shell.Press(ShellKey.Up);
        shell.Press(ShellKey.Enter);

        Assert.IsType<AttachmentsScreen>(shell.Screen);

        return (shell, built, compose);
    }

    /// <summary>The edit public profile form, opened with <c>E</c> on <b>You</b>, a toggle on it flipped.</summary>
    private static async Task<(Shell Shell, AShell Built, PublicProfileScreen Form)> Editing()
    {
        var built = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(Me),
            PublicProfile = FakePublicProfileEditor.Holding(APublicProfile.With(isBot: false, account: Me)),
        };
        var shell = await built.Opened();

        shell.Rail.GoTo(shell.Rail.Of(DestinationKind.Profile));
        built.Host.Settle();
        shell.Press(ShellKey.CapitalE);
        built.Host.Drain();

        var form = Assert.IsType<PublicProfileScreen>(shell.Screen);

        while (form.Typing.Part != PublicProfilePart.Bot)
        {
            Assert.True(shell.Press(ShellKey.Down));
        }

        shell.Press(ShellKey.Space);

        Assert.True(form.Touched);

        return (shell, built, form);
    }
}
